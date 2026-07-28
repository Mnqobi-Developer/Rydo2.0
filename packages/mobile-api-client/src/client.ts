import type {
  AuthenticatedUser,
  OtpRequestResult,
  RefreshTokenRequest,
  RequestOtpRequest,
  TokenPair,
  VerifyOtpRequest,
} from './contracts';
import { ApiError, isApiError } from './errors';
import { isTokenPair, type TokenStore } from './token-store';

type HttpMethod = 'DELETE' | 'GET' | 'HEAD' | 'PATCH' | 'POST' | 'PUT';
type AuthenticationMode = 'none' | 'optional' | 'required';
type RetryMode = 'always' | 'default' | 'never';

export interface ApiRequestOptions<TBody = never> {
  method?: HttpMethod;
  body?: TBody;
  headers?: Record<string, string>;
  signal?: AbortSignal;
  authentication?: AuthenticationMode;
  retry?: RetryMode;
  timeoutMs?: number;
}

export interface ApiClientOptions {
  baseUrl: string;
  tokenStore: TokenStore;
  fetch?: typeof globalThis.fetch;
  requestTimeoutMs?: number;
  maximumRetries?: number;
  onAuthenticationExpired?: () => void | Promise<void>;
  sleep?: (milliseconds: number, signal: AbortSignal) => Promise<void>;
}

export interface ApiClient {
  request<TResponse, TBody = never>(
    path: string,
    options?: ApiRequestOptions<TBody>,
  ): Promise<TResponse>;
  get<TResponse>(
    path: string,
    options?: Omit<ApiRequestOptions, 'body' | 'method'>,
  ): Promise<TResponse>;
  post<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body' | 'method'>,
  ): Promise<TResponse>;
  put<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body' | 'method'>,
  ): Promise<TResponse>;
  patch<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body' | 'method'>,
  ): Promise<TResponse>;
  delete<TResponse>(
    path: string,
    options?: Omit<ApiRequestOptions, 'body' | 'method'>,
  ): Promise<TResponse>;
  setTokens(tokens: TokenPair): Promise<void>;
  clearTokens(): Promise<void>;
  auth: {
    requestOtp(request: RequestOtpRequest, signal?: AbortSignal): Promise<OtpRequestResult>;
    verifyOtp(request: VerifyOtpRequest, signal?: AbortSignal): Promise<TokenPair>;
    me(signal?: AbortSignal): Promise<AuthenticatedUser>;
    revokeSession(signal?: AbortSignal): Promise<void>;
  };
}

const retryableStatuses = new Set([408, 429, 502, 503, 504]);
const safeMethods = new Set<HttpMethod>(['GET', 'HEAD']);

export function createApiClient(options: ApiClientOptions): ApiClient {
  const baseUrl = normalizeBaseUrl(options.baseUrl);
  const fetchImplementation = options.fetch ?? globalThis.fetch;
  const requestTimeoutMs = options.requestTimeoutMs ?? 15_000;
  const maximumRetries = options.maximumRetries ?? 2;
  const sleep = options.sleep ?? defaultSleep;
  let refreshPromise: Promise<TokenPair> | null = null;
  let authenticationRevision = 0;

  async function clearTokens() {
    authenticationRevision += 1;
    await options.tokenStore.clear();
  }

  async function setTokens(tokens: TokenPair) {
    authenticationRevision += 1;
    await options.tokenStore.set(tokens);
  }

  async function expireAuthentication(cause?: unknown): Promise<never> {
    await clearTokens();

    try {
      await options.onAuthenticationExpired?.();
    } catch {
      // Navigation/cache cleanup must not replace the stable authentication error.
    }

    throw new ApiError('Your session has expired. Sign in again.', {
      kind: 'auth-expired',
      status: 401,
      cause,
    });
  }

  async function rotateTokens(): Promise<TokenPair> {
    if (refreshPromise) {
      return refreshPromise;
    }

    const revisionAtStart = authenticationRevision;
    refreshPromise = (async () => {
      const currentTokens = await options.tokenStore.get();

      if (!currentTokens?.refreshToken) {
        return expireAuthentication();
      }

      const request = createRequestSignal(undefined, requestTimeoutMs);

      try {
        const refreshRequest: RefreshTokenRequest = {
          refreshToken: currentTokens.refreshToken,
        };
        const response = await fetchImplementation(`${baseUrl}/api/v1/auth/refresh`, {
          method: 'POST',
          headers: {
            Accept: 'application/json',
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(refreshRequest),
          signal: request.signal,
        });

        if (!response.ok) {
          throw await createHttpError(response);
        }

        const tokens = await readSuccess<TokenPair>(response);

        if (!isTokenPair(tokens)) {
          throw new ApiError('The API returned an invalid token response.', {
            kind: 'decode',
            status: response.status,
          });
        }

        if (revisionAtStart !== authenticationRevision) {
          throw new ApiError('Authentication changed while the session was refreshing.', {
            kind: 'cancelled',
          });
        }

        await options.tokenStore.set(tokens);
        return tokens;
      } catch (error) {
        if (isApiError(error) && error.kind === 'cancelled') {
          throw error;
        }

        return expireAuthentication(error);
      } finally {
        request.cleanup();
      }
    })().finally(() => {
      refreshPromise = null;
    });

    return refreshPromise;
  }

  async function request<TResponse, TBody = never>(
    path: string,
    requestOptions: ApiRequestOptions<TBody> = {},
  ): Promise<TResponse> {
    validatePath(path);
    const method = requestOptions.method ?? 'GET';
    const authentication = requestOptions.authentication ?? 'required';
    const retryMode = requestOptions.retry ?? 'default';
    const requestSignal = createRequestSignal(
      requestOptions.signal,
      requestOptions.timeoutMs ?? requestTimeoutMs,
    );
    let refreshed = false;
    let attempt = 0;

    try {
      while (true) {
        if (requestSignal.signal.aborted) {
          throw createAbortError(requestSignal.timedOut());
        }

        const tokens = authentication === 'none' ? null : await options.tokenStore.get();
        const headers = new Headers({ Accept: 'application/json' });

        for (const [name, value] of Object.entries(requestOptions.headers ?? {})) {
          headers.set(name, value);
        }

        if (requestOptions.body !== undefined) {
          if (!headers.has('Content-Type')) {
            headers.set('Content-Type', 'application/json');
          }
        }

        if (tokens?.accessToken) {
          headers.set('Authorization', `Bearer ${tokens.accessToken}`);
        }

        let response: Response;

        try {
          response = await fetchImplementation(`${baseUrl}${path}`, {
            method,
            headers,
            body:
              requestOptions.body === undefined
                ? undefined
                : JSON.stringify(requestOptions.body),
            signal: requestSignal.signal,
          });
        } catch (error) {
          const apiError = normalizeTransportError(
            error,
            requestSignal.signal,
            requestSignal.timedOut(),
          );

          if (!shouldRetry(apiError, method, retryMode, attempt, maximumRetries)) {
            throw apiError;
          }

          await waitForRetry(sleep, backoffMilliseconds(attempt), requestSignal);
          attempt += 1;
          continue;
        }

        if (response.ok) {
          return readSuccess<TResponse>(response);
        }

        const apiError = await createHttpError(response);

        if (response.status === 401 && authentication !== 'none' && !refreshed) {
          refreshed = true;
          await rotateTokens();
          continue;
        }

        if (response.status === 401 && authentication === 'required') {
          return expireAuthentication(apiError);
        }

        if (!shouldRetry(apiError, method, retryMode, attempt, maximumRetries)) {
          throw apiError;
        }

        await waitForRetry(
          sleep,
          retryDelay(response, attempt),
          requestSignal,
        );
        attempt += 1;
      }
    } finally {
      requestSignal.cleanup();
    }
  }

  const client: ApiClient = {
    request,
    get: (path, requestOptions) =>
      request(path, { ...requestOptions, method: 'GET' }),
    post: (path, body, requestOptions) =>
      request(path, { ...requestOptions, method: 'POST', body }),
    put: (path, body, requestOptions) =>
      request(path, { ...requestOptions, method: 'PUT', body }),
    patch: (path, body, requestOptions) =>
      request(path, { ...requestOptions, method: 'PATCH', body }),
    delete: (path, requestOptions) =>
      request(path, { ...requestOptions, method: 'DELETE' }),
    setTokens,
    clearTokens,
    auth: {
      requestOtp: (body, signal) =>
        request('/api/v1/auth/otp/request', {
          method: 'POST',
          body,
          signal,
          authentication: 'none',
          retry: 'never',
        }),
      async verifyOtp(body, signal) {
        const tokens = await request<TokenPair, VerifyOtpRequest>(
          '/api/v1/auth/otp/verify',
          {
            method: 'POST',
            body,
            signal,
            authentication: 'none',
            retry: 'never',
          },
        );

        if (!isTokenPair(tokens)) {
          throw new ApiError('The API returned an invalid token response.', {
            kind: 'decode',
          });
        }

        await setTokens(tokens);
        return tokens;
      },
      me: (signal) => request('/api/v1/auth/me', { signal }),
      async revokeSession(signal) {
        try {
          await request<void>('/api/v1/auth/sessions/revoke', {
            method: 'POST',
            signal,
            retry: 'never',
          });
        } finally {
          await clearTokens();
        }
      },
    },
  };

  return client;
}

function normalizeBaseUrl(value: string) {
  const url = new URL(value);

  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    throw new Error('The API base URL must use HTTP or HTTPS.');
  }

  url.pathname = url.pathname.replace(/\/$/, '');
  url.search = '';
  url.hash = '';
  return url.toString().replace(/\/$/, '');
}

function validatePath(path: string) {
  if (!path.startsWith('/') || path.startsWith('//')) {
    throw new Error('API request paths must be root-relative.');
  }
}

async function readSuccess<TResponse>(response: Response): Promise<TResponse> {
  if (response.status === 204) {
    return undefined as TResponse;
  }

  const text = await response.text();

  if (!text) {
    return undefined as TResponse;
  }

  if (!response.headers.get('content-type')?.includes('json')) {
    return text as TResponse;
  }

  try {
    return JSON.parse(text) as TResponse;
  } catch (error) {
    throw new ApiError('The API returned invalid JSON.', {
      kind: 'decode',
      status: response.status,
      cause: error,
    });
  }
}

async function createHttpError(response: Response) {
  const requestId = response.headers.get('x-request-id') ?? undefined;
  let problem;

  try {
    problem = await response.json();
  } catch {
    problem = undefined;
  }

  const title =
    problem && typeof problem === 'object' && 'title' in problem
      ? String(problem.title)
      : undefined;
  const detail =
    problem && typeof problem === 'object' && 'detail' in problem
      ? String(problem.detail)
      : undefined;

  return new ApiError(detail ?? title ?? `Request failed with status ${response.status}.`, {
    kind: 'http',
    status: response.status,
    problem: problem && typeof problem === 'object' ? problem : undefined,
    requestId,
    retryable: retryableStatuses.has(response.status),
  });
}

function normalizeTransportError(
  error: unknown,
  signal: AbortSignal,
  timedOut: boolean,
) {
  if (isApiError(error)) {
    return error;
  }

  if (signal.aborted) {
    return createAbortError(timedOut, error);
  }

  return new ApiError('Unable to reach the RYDO API.', {
    kind: 'network',
    retryable: true,
    cause: error,
  });
}

function createAbortError(timedOut: boolean, cause?: unknown) {
  return new ApiError(timedOut ? 'The API request timed out.' : 'The API request was cancelled.', {
    kind: timedOut ? 'timeout' : 'cancelled',
    retryable: false,
    cause,
  });
}

function shouldRetry(
  error: ApiError,
  method: HttpMethod,
  retryMode: RetryMode,
  attempt: number,
  maximumRetries: number,
) {
  if (retryMode === 'never' || attempt >= maximumRetries || !error.retryable) {
    return false;
  }

  return retryMode === 'always' || safeMethods.has(method);
}

function retryDelay(response: Response, attempt: number) {
  const retryAfter = response.headers.get('retry-after');

  if (retryAfter) {
    const seconds = Number(retryAfter);

    if (Number.isFinite(seconds) && seconds >= 0) {
      return Math.min(seconds * 1000, 30_000);
    }

    const date = Date.parse(retryAfter);

    if (!Number.isNaN(date)) {
      return Math.min(Math.max(date - Date.now(), 0), 30_000);
    }
  }

  return backoffMilliseconds(attempt);
}

function backoffMilliseconds(attempt: number) {
  const exponential = 250 * 2 ** attempt;
  return exponential + Math.floor(Math.random() * exponential * 0.25);
}

interface RequestSignal {
  signal: AbortSignal;
  timedOut(): boolean;
  cleanup(): void;
}

function createRequestSignal(source: AbortSignal | undefined, timeoutMs: number): RequestSignal {
  const controller = new AbortController();
  let didTimeOut = false;
  const abortFromSource = () => controller.abort(source?.reason);

  if (source?.aborted) {
    abortFromSource();
  } else {
    source?.addEventListener('abort', abortFromSource, { once: true });
  }

  const timeout = setTimeout(() => {
    didTimeOut = true;
    controller.abort();
  }, timeoutMs);

  return {
    signal: controller.signal,
    timedOut: () => didTimeOut,
    cleanup() {
      clearTimeout(timeout);
      source?.removeEventListener('abort', abortFromSource);
    },
  };
}

async function waitForRetry(
  sleep: (milliseconds: number, signal: AbortSignal) => Promise<void>,
  milliseconds: number,
  requestSignal: RequestSignal,
) {
  try {
    await sleep(milliseconds, requestSignal.signal);
  } catch (error) {
    throw normalizeTransportError(
      error,
      requestSignal.signal,
      requestSignal.timedOut(),
    );
  }
}

function defaultSleep(milliseconds: number, signal: AbortSignal) {
  return new Promise<void>((resolve, reject) => {
    const timeout = setTimeout(() => {
      signal.removeEventListener('abort', abort);
      resolve();
    }, milliseconds);
    const abort = () => {
      clearTimeout(timeout);
      reject(signal.reason);
    };

    if (signal.aborted) {
      abort();
      return;
    }

    signal.addEventListener('abort', abort, { once: true });
  });
}
