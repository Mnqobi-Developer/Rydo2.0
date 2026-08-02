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

export type RideCategory = 'Solo' | 'Group' | 'GroupPlus';

export interface FareBreakdown {
  distanceCharge: number;
  minimumFareAdjustment: number;
  bookingFee: number;
  demandAdjustment: number;
  estimatedTolls: number;
  waitingFee: number;
  discount: number;
}

export interface FareOption {
  category: RideCategory;
  ratePerKilometre: number;
  minimumFare: number;
  total: number;
  breakdown: FareBreakdown;
}

export interface FareQuote {
  id: string;
  pricingVersion: string;
  currency: string;
  distanceMeters: number;
  durationSeconds: number;
  demandMultiplier: number;
  createdAt: string;
  expiresAt: string;
  options: FareOption[];
  encodedPolyline: string;
}

export interface CreateFareQuoteRequest {
  pickup: import('./maps').GeoCoordinate;
  destination: import('./maps').GeoCoordinate;
}

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
  fareQuoteId: string | null;
  rideCategory: RideCategory | null;
  estimatedFareAmount: number | null;
  fareCurrency: string | null;
  pricingVersion: string | null;
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
  fareQuoteId: string;
  rideCategory: RideCategory;
}

export interface TripMatchingResult {
  tripId: string;
  offeredDriverCount: number;
  offersExpireAt: string | null;
}

export interface TripOffer {
  id: string;
  tripId: string;
  driverUserId: string;
  pickupAddress: string;
  pickupLatitude: number;
  pickupLongitude: number;
  destinationAddress: string;
  destinationLatitude: number;
  destinationLongitude: number;
  pickupDistanceKilometres: number;
  rideCategory: RideCategory | null;
  estimatedFareAmount: number | null;
  fareCurrency: string | null;
  status: 'Pending' | 'Accepted' | 'Declined' | 'Expired';
  offeredAt: string;
  expiresAt: string;
  respondedAt: string | null;
  version: number;
}

export interface DriverAvailability {
  driverUserId: string;
  isOnline: boolean;
  latitude: number;
  longitude: number;
  locationUpdatedAt: string | null;
  updatedAt: string;
  version: number;
}

export type PaymentMethod = 'Cash' | 'PayFast';
export type PaymentStatus = 'AwaitingPayment' | 'Paid' | 'Cancelled' | 'Failed';

export interface Payment {
  id: string;
  tripId: string;
  passengerUserId: string;
  method: PaymentMethod;
  status: PaymentStatus;
  amount: number;
  currency: string;
  providerPaymentId: string | null;
  createdAt: string;
  updatedAt: string;
  paidAt: string | null;
  failedAt: string | null;
  failureReason: string | null;
  version: number;
}

export interface PayFastCheckout {
  processUrl: string;
  fields: Record<string, string>;
}

export interface CreatePaymentResult {
  payment: Payment;
  payFastCheckout: PayFastCheckout | null;
}

export interface CreatePaymentRequest {
  method: PaymentMethod;
}

export interface Rating {
  id: string;
  tripId: string;
  raterUserId: string;
  ratedUserId: string;
  score: number;
  comment: string | null;
  createdAt: string;
}

export interface CreateRatingRequest {
  score: number;
  comment: string | null;
}

export interface RatingSummary {
  userId: string;
  averageScore: number | null;
  ratingCount: number;
  distribution: Record<number, number>;
}
