export type UserRole = 'Passenger' | 'Driver' | 'Admin';

export interface AuthenticatedUser {
  id: string;
  phoneNumber: string;
  role: UserRole;
}

export interface TokenPair {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: AuthenticatedUser;
}

export interface RequestOtpRequest {
  phoneNumber: string;
  role: Extract<UserRole, 'Passenger' | 'Driver'>;
}

export interface OtpRequestResult {
  challengeId: string;
  expiresAt: string;
  developmentCode: string | null;
}

export interface VerifyOtpRequest {
  challengeId: string;
  code: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface ApiProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}
