import type { ApiProblemDetails } from './contracts';

export type ApiErrorKind =
  | 'auth-expired'
  | 'cancelled'
  | 'decode'
  | 'http'
  | 'network'
  | 'timeout';

interface ApiErrorOptions {
  kind: ApiErrorKind;
  status?: number;
  problem?: ApiProblemDetails;
  requestId?: string;
  retryable?: boolean;
  cause?: unknown;
}

export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status?: number;
  readonly problem?: ApiProblemDetails;
  readonly requestId?: string;
  readonly retryable: boolean;

  constructor(message: string, options: ApiErrorOptions) {
    super(message, { cause: options.cause });
    this.name = 'ApiError';
    this.kind = options.kind;
    this.status = options.status;
    this.problem = options.problem;
    this.requestId = options.requestId;
    this.retryable = options.retryable ?? false;
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}
