import { describe, expect, it, vi } from 'vitest';

import {
  ApiError,
  createApiClient,
  createSerializedTokenStore,
  type TokenPair,
  type TokenStore,
} from '../src';

const oldTokens = tokenPair('old-access', 'old-refresh');
const newTokens = tokenPair('new-access', 'new-refresh');

describe('API client', () => {
  it('attaches JWTs and rotates one refresh token for concurrent 401 responses', async () => {
    const tokenStore = memoryTokenStore(oldTokens);
    let refreshCalls = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.endsWith('/api/v1/auth/refresh')) {
        refreshCalls += 1;
        expect(init?.body).toBe(JSON.stringify({ refreshToken: 'old-refresh' }));
        await Promise.resolve();
        return jsonResponse(newTokens);
      }

      if (new Headers(init?.headers).get('authorization') === 'Bearer new-access') {
        return jsonResponse({ id: url.endsWith('/one') ? 1 : 2 });
      }

      expect(new Headers(init?.headers).get('authorization')).toBe('Bearer old-access');
      return jsonResponse({}, 401);
    });
    const client = createClient(fetchMock, tokenStore);

    const [one, two] = await Promise.all([
      client.get<{ id: number }>('/one'),
      client.get<{ id: number }>('/two'),
    ]);

    expect(one.id).toBe(1);
    expect(two.id).toBe(2);
    expect(refreshCalls).toBe(1);
    expect(await tokenStore.get()).toEqual(newTokens);
  });

  it('never retries an ambiguous refresh failure and clears the session', async () => {
    const tokenStore = memoryTokenStore(oldTokens);
    const onAuthenticationExpired = vi.fn();
    let refreshCalls = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).endsWith('/api/v1/auth/refresh')) {
        refreshCalls += 1;
        throw new TypeError('connection reset');
      }

      return jsonResponse({}, 401);
    });
    const client = createClient(fetchMock, tokenStore, { onAuthenticationExpired });

    await expect(client.get('/protected')).rejects.toMatchObject({
      kind: 'auth-expired',
    });
    expect(refreshCalls).toBe(1);
    expect(await tokenStore.get()).toBeNull();
    expect(onAuthenticationExpired).toHaveBeenCalledOnce();
  });

  it('refreshes proactively before attaching an access token near expiry', async () => {
    const expiringTokens = tokenPair('expiring-access', 'valid-refresh', {
      accessTokenExpiresAt: '2026-07-28T12:00:30Z',
    });
    const tokenStore = memoryTokenStore(expiringTokens);
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith('/api/v1/auth/refresh')) {
        return jsonResponse(newTokens);
      }

      expect(new Headers(init?.headers).get('authorization')).toBe('Bearer new-access');
      return jsonResponse({ ready: true });
    });
    const client = createClient(fetchMock, tokenStore, {
      now: () => Date.parse('2026-07-28T12:00:00Z'),
    });

    await expect(client.get('/protected')).resolves.toEqual({ ready: true });
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(await tokenStore.get()).toEqual(newTokens);
  });

  it('provides SignalR with a fresh access token without exposing the refresh token', async () => {
    const expiringTokens = tokenPair('expiring-access', 'valid-refresh', {
      accessTokenExpiresAt: '2026-07-28T12:00:30Z',
    });
    const tokenStore = memoryTokenStore(expiringTokens);
    const fetchMock = vi.fn(async () => jsonResponse(newTokens));
    const client = createClient(fetchMock, tokenStore, {
      now: () => Date.parse('2026-07-28T12:00:00Z'),
    });

    await expect(client.auth.getAccessToken()).resolves.toBe('new-access');
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('expires locally without a network request when the refresh token is expired', async () => {
    const expiredTokens = tokenPair('expired-access', 'expired-refresh', {
      accessTokenExpiresAt: '2026-07-28T11:00:00Z',
      refreshTokenExpiresAt: '2026-07-28T11:30:00Z',
    });
    const tokenStore = memoryTokenStore(expiredTokens);
    const fetchMock = vi.fn(async () => jsonResponse({}));
    const client = createClient(fetchMock, tokenStore, {
      now: () => Date.parse('2026-07-28T12:00:00Z'),
    });

    await expect(client.get('/protected')).rejects.toMatchObject({
      kind: 'auth-expired',
    });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(await tokenStore.get()).toBeNull();
    expect(client.auth.getSnapshot()).toMatchObject({ status: 'expired', user: null });
  });

  it('restores a persisted session and publishes the current backend user', async () => {
    const tokenStore = memoryTokenStore(oldTokens);
    const backendUser = { ...oldTokens.user, phoneNumber: '+27821111111' };
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      expect(String(input)).toBe('https://api.rydo.test/api/v1/auth/me');
      return jsonResponse(backendUser);
    });
    const client = createClient(fetchMock, tokenStore);
    const listener = vi.fn();
    const unsubscribe = client.auth.subscribe(listener);

    await expect(client.auth.restoreSession()).resolves.toMatchObject({
      status: 'authenticated',
      user: backendUser,
    });
    expect((await tokenStore.get())?.user).toEqual(backendUser);
    expect(listener).toHaveBeenCalled();
    unsubscribe();
  });

  it('restores an empty secure store as an anonymous session', async () => {
    const fetchMock = vi.fn(async () => jsonResponse({}));
    const client = createClient(fetchMock, memoryTokenStore());

    await expect(client.auth.restoreSession()).resolves.toEqual({
      status: 'anonymous',
      user: null,
      error: null,
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('revokes on logout and clears local credentials even when revocation fails', async () => {
    const tokenStore = memoryTokenStore(oldTokens);
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      expect(String(input)).toBe(
        'https://api.rydo.test/api/v1/auth/sessions/revoke',
      );
      return jsonResponse({ title: 'Unavailable' }, 503);
    });
    const client = createClient(fetchMock, tokenStore);

    await expect(client.auth.logout()).rejects.toMatchObject({ status: 503 });
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(await tokenStore.get()).toBeNull();
    expect(client.auth.getSnapshot()).toEqual({
      status: 'anonymous',
      user: null,
      error: null,
    });
  });

  it('retries safe requests with bounded backoff', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ title: 'Busy' }, 503))
      .mockResolvedValueOnce(jsonResponse({ title: 'Busy' }, 503))
      .mockResolvedValueOnce(jsonResponse({ value: 'ready' }));
    const sleep = vi.fn(async () => undefined);
    const client = createClient(fetchMock, memoryTokenStore(), { sleep });

    await expect(
      client.get<{ value: string }>('/health', { authentication: 'none' }),
    ).resolves.toEqual({ value: 'ready' });
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(sleep).toHaveBeenCalledTimes(2);
  });

  it('does not retry mutations unless explicitly enabled', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ title: 'Busy' }, 503));
    const client = createClient(fetchMock, memoryTokenStore());

    await expect(
      client.post('/trips', { pickup: 'home' }, { authentication: 'none' }),
    ).rejects.toMatchObject({ status: 503, retryable: true });
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('normalizes ASP.NET problem details', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(
        {
          title: 'Invalid trip',
          detail: 'Pickup is required.',
          status: 400,
          errors: { pickup: ['Required'] },
        },
        400,
        { 'x-request-id': 'request-123' },
      ),
    );
    const client = createClient(fetchMock, memoryTokenStore());

    await expect(
      client.post('/trips', {}, { authentication: 'none' }),
    ).rejects.toMatchObject({
      kind: 'http',
      status: 400,
      message: 'Pickup is required.',
      requestId: 'request-123',
      problem: { title: 'Invalid trip' },
    });
  });

  it('rejects malformed sign-in token responses before persisting them', async () => {
    const tokenStore = memoryTokenStore();
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ accessToken: 'partial' }));
    const client = createClient(fetchMock, tokenStore);

    await expect(
      client.auth.verifyOtp({
        challengeId: '45a17be7-c90c-4bbc-bba7-9dad0359da07',
        code: '123456',
      }),
    ).rejects.toMatchObject({ kind: 'decode' });
    expect(await tokenStore.get()).toBeNull();
  });

  it('distinguishes caller cancellation from timeouts', async () => {
    const controller = new AbortController();
    controller.abort();
    const neverFetch = vi.fn(async () => jsonResponse({}));
    const cancelledClient = createClient(neverFetch, memoryTokenStore());

    await expect(
      cancelledClient.get('/cancelled', {
        authentication: 'none',
        signal: controller.signal,
      }),
    ).rejects.toMatchObject({ kind: 'cancelled' });
    expect(neverFetch).not.toHaveBeenCalled();

    const hangingFetch = vi.fn(
      (_input: RequestInfo | URL, init?: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => reject(new Error('aborted')));
        }),
    );
    const timeoutClient = createClient(hangingFetch, memoryTokenStore(), {
      requestTimeoutMs: 5,
    });

    await expect(
      timeoutClient.get('/timeout', { authentication: 'none' }),
    ).rejects.toMatchObject({ kind: 'timeout' });
  });

  it('rejects non-relative paths before attaching credentials', async () => {
    const fetchMock = vi.fn(async () => jsonResponse({}));
    const client = createClient(fetchMock, memoryTokenStore(oldTokens));

    await expect(client.get('https://attacker.example')).rejects.toThrow(
      'root-relative',
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe('serialized token store', () => {
  it('removes corrupt values instead of returning untrusted tokens', async () => {
    let value: string | null = '{broken';
    const storage = {
      getItem: vi.fn(async () => value),
      setItem: vi.fn(async (_key: string, next: string) => {
        value = next;
      }),
      removeItem: vi.fn(async () => {
        value = null;
      }),
    };
    const store = createSerializedTokenStore(storage, 'auth');

    await expect(store.get()).resolves.toBeNull();
    expect(storage.removeItem).toHaveBeenCalledWith('auth');
  });
});

function createClient(
  fetchMock: ReturnType<typeof vi.fn>,
  tokenStore: TokenStore,
  overrides: Partial<Parameters<typeof createApiClient>[0]> = {},
) {
  return createApiClient({
    baseUrl: 'https://api.rydo.test',
    tokenStore,
    fetch: fetchMock as typeof fetch,
    sleep: async () => undefined,
    now: () => Date.parse('2026-07-28T12:00:00Z'),
    ...overrides,
  });
}

function memoryTokenStore(initial: TokenPair | null = null): TokenStore {
  let tokens = initial;

  return {
    get: async () => tokens,
    set: async (next) => {
      tokens = next;
    },
    clear: async () => {
      tokens = null;
    },
  };
}

function tokenPair(
  accessToken: string,
  refreshToken: string,
  expirations: Partial<
    Pick<TokenPair, 'accessTokenExpiresAt' | 'refreshTokenExpiresAt'>
  > = {},
): TokenPair {
  return {
    accessToken,
    accessTokenExpiresAt: '2026-07-28T13:00:00Z',
    refreshToken,
    refreshTokenExpiresAt: '2026-08-28T13:00:00Z',
    user: {
      id: '45a17be7-c90c-4bbc-bba7-9dad0359da07',
      phoneNumber: '+27820000000',
      role: 'Passenger',
    },
    ...expirations,
  };
}

function jsonResponse(
  body: unknown,
  status = 200,
  headers: Record<string, string> = {},
) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json', ...headers },
  });
}
