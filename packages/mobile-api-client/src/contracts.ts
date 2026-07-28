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

export type AuthSessionStatus =
  | 'restoring'
  | 'authenticated'
  | 'anonymous'
  | 'expired'
  | 'unavailable';

export interface AuthSessionSnapshot {
  status: AuthSessionStatus;
  user: AuthenticatedUser | null;
  error: import('./errors').ApiError | null;
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

export interface PassengerProfile {
  userId: string;
  firstName: string;
  lastName: string;
  email: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpdatePassengerProfileRequest {
  firstName: string;
  lastName: string;
  email: string | null;
}

export type TripStatus =
  | 'Requested'
  | 'Accepted'
  | 'DriverArrived'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled';

export interface Trip {
  id: string;
  passengerUserId: string;
  driverUserId: string | null;
  pickupAddress: string;
  pickupLatitude: number;
  pickupLongitude: number;
  destinationAddress: string;
  destinationLatitude: number;
  destinationLongitude: number;
  status: TripStatus;
  requestedAt: string;
  updatedAt: string;
  acceptedAt: string | null;
  driverArrivedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  cancelledByUserId: string | null;
  cancellationReason: string | null;
  finalFareAmount: number | null;
  version: number;
}

export interface RequestTripRequest {
  pickupAddress: string;
  pickupLatitude: number;
  pickupLongitude: number;
  destinationAddress: string;
  destinationLatitude: number;
  destinationLongitude: number;
}

export interface TripMatchingResult {
  tripId: string;
  offeredDriverCount: number;
  offersExpireAt: string | null;
}
