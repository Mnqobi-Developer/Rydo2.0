import {
  isApiError,
  type CreateRatingRequest,
  type Payment,
  type Rating,
  type Trip,
  type TripOffer,
} from '@rydo/mobile-api-client';
import { queryOptions } from '@tanstack/react-query';
import { File } from 'expo-file-system';

import { apiClient } from '@/api';

export const driverTripsKey = ['trips', 'driver'] as const;

export const driverTripsQuery = queryOptions({
  queryKey: driverTripsKey,
  queryFn: ({ signal }) => apiClient.get<Trip[]>('/api/v1/trips/me', { signal }),
});

export const driverTripOffersKey = ['trip-offers'] as const;
export const driverPerformanceKey = ['driver-performance'] as const;
export const driverAvailabilityKey = ['driver-availability'] as const;

export interface DriverAvailability {
  driverUserId: string;
  isOnline: boolean;
  latitude: number;
  longitude: number;
  locationUpdatedAt: string | null;
  updatedAt: string;
  version: number;
}

export interface DriverPerformance {
  acceptanceRate: number | null;
  completionRate: number | null;
  averageRating: number | null;
  ratingCount: number;
}

export const driverTripOffersQuery = queryOptions({
  queryKey: driverTripOffersKey,
  queryFn: ({ signal }) => apiClient.get<TripOffer[]>('/api/v1/drivers/me/trip-offers', { signal }),
  refetchInterval: 8_000,
});

export const driverPerformanceQuery = queryOptions({
  queryKey: driverPerformanceKey,
  queryFn: ({ signal }) => apiClient.get<DriverPerformance>('/api/v1/drivers/me/performance', { signal }),
  // Ratings can be submitted later by the passenger and currently have no dedicated realtime event.
  refetchInterval: 30_000,
});

export const driverAvailabilityQuery = queryOptions({
  queryKey: driverAvailabilityKey,
  queryFn: async ({ signal }) => {
    try {
      return await apiClient.get<DriverAvailability>('/api/v1/drivers/me/availability', { signal });
    } catch (error) {
      if (isApiError(error) && error.status === 404) return null;
      throw error;
    }
  },
  refetchInterval: 15_000,
});

export const driverTripPaymentKey = (tripId: string) => ['payment', tripId] as const;
export const driverTripRatingKey = (tripId: string) => ['rating', tripId, 'driver'] as const;

export function driverTripPaymentQuery(tripId: string) {
  return queryOptions({
    queryKey: driverTripPaymentKey(tripId),
    queryFn: async ({ signal }) => {
      try {
        return await apiClient.get<Payment>(`/api/v1/trips/${tripId}/payment`, { signal });
      } catch (error) {
        if (isApiError(error) && error.status === 404) return null;
        throw error;
      }
    },
  });
}

export function driverTripRatingQuery(tripId: string) {
  return queryOptions({
    queryKey: driverTripRatingKey(tripId),
    queryFn: async ({ signal }) => {
      try {
        return await apiClient.get<Rating>(`/api/v1/trips/${tripId}/ratings/me`, { signal });
      } catch (error) {
        if (isApiError(error) && error.status === 404) return null;
        throw error;
      }
    },
  });
}

export function rateDriverTrip(tripId: string, input: CreateRatingRequest) {
  return apiClient.post<Rating, CreateRatingRequest>(
    `/api/v1/trips/${tripId}/ratings`,
    input,
    { retry: 'never' },
  );
}

export interface DriverProfile {
  userId: string;
  firstName: string;
  lastName: string;
  email: string | null;
  onboardingStatus: 'Draft' | 'PendingReview' | 'Approved' | 'Rejected';
  canEdit: boolean;
  createdAt: string;
  updatedAt: string;
  submittedAt: string | null;
  reviewedAt: string | null;
  rejectionReason: string | null;
}

export interface UpdateDriverProfileInput {
  firstName: string;
  lastName: string;
  email: string | null;
}

export interface DriverVehicle {
  id: string;
  driverUserId: string;
  make: string;
  model: string;
  year: number;
  color: string;
  registrationNumber: string;
  vehicleIdentificationNumber: string;
  seatCapacity: number;
  reviewStatus: 'PendingReview' | 'Approved' | 'Rejected';
  createdAt: string;
  updatedAt: string;
  reviewedAt: string | null;
  rejectionReason: string | null;
}

export interface UpdateDriverVehicleInput {
  make: string;
  model: string;
  year: number;
  color: string;
  registrationNumber: string;
  vehicleIdentificationNumber: string;
  seatCapacity: number;
}

export interface DriverDocument {
  id: string;
  documentType: 'IdentityDocument' | 'DriversLicense' | 'ProfessionalDrivingPermit';
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  reviewStatus: 'PendingReview' | 'Approved' | 'Rejected';
  uploadedAt: string;
  reviewedAt: string | null;
  rejectionReason: string | null;
}

export interface UploadDriverDocumentInput {
  documentType: DriverDocument['documentType'];
  file: {
    uri: string;
    name: string;
    type: 'application/pdf' | 'image/jpeg' | 'image/png';
  };
}

export const driverProfileQuery = queryOptions({
  queryKey: ['driver-profile'] as const,
  queryFn: ({ signal }) => apiClient.get<DriverProfile>('/api/v1/drivers/me/profile', { signal }),
});

export function saveDriverProfile(input: UpdateDriverProfileInput) {
  return apiClient.put<DriverProfile, UpdateDriverProfileInput>(
    '/api/v1/drivers/me/profile',
    input,
    { retry: 'never' },
  );
}

export const driverVehicleQuery = queryOptions({
  queryKey: ['driver-vehicle'] as const,
  queryFn: async ({ signal }) => {
    try {
      return await apiClient.get<DriverVehicle>('/api/v1/drivers/me/vehicle', { signal });
    } catch (error) {
      if (isApiError(error) && error.status === 404) return null;
      throw error;
    }
  },
});

export const driverDocumentsQuery = queryOptions({
  queryKey: ['driver-documents'] as const,
  queryFn: ({ signal }) => apiClient.get<DriverDocument[]>('/api/v1/drivers/me/documents', { signal }),
});

export function saveDriverVehicle(input: UpdateDriverVehicleInput) {
  return apiClient.put<DriverVehicle, UpdateDriverVehicleInput>(
    '/api/v1/drivers/me/vehicle',
    input,
    { retry: 'never' },
  );
}

export function uploadDriverDocument(input: UploadDriverDocumentInput) {
  const body = new FormData();
  const file = new File(input.file.uri);
  body.append('documentType', input.documentType);
  body.append('file', file, input.file.name);
  return apiClient.post<DriverDocument, FormData>(
    '/api/v1/drivers/me/documents',
    body,
    { retry: 'never', timeoutMs: 90_000 },
  );
}

export function submitDriverOnboarding() {
  return apiClient.request<DriverProfile>('/api/v1/drivers/me/onboarding/submit', {
    method: 'POST',
    retry: 'never',
  });
}
