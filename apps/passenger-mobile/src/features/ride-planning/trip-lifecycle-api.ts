import {
  isApiError,
  type CreatePaymentRequest,
  type CreatePaymentResult,
  type CreateRatingRequest,
  type Payment,
  type Rating,
} from '@rydo/mobile-api-client';
import { queryOptions } from '@tanstack/react-query';

import { apiClient } from '@/api';

export const tripPaymentKey = (tripId: string) => ['payment', tripId] as const;
export const tripRatingKey = (tripId: string) => ['rating', tripId, 'passenger'] as const;

export function tripPaymentQuery(tripId: string) {
  return queryOptions({
    queryKey: tripPaymentKey(tripId),
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

export function tripRatingQuery(tripId: string) {
  return queryOptions({
    queryKey: tripRatingKey(tripId),
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

export function createTripPayment(tripId: string, request: CreatePaymentRequest) {
  return apiClient.post<CreatePaymentResult, CreatePaymentRequest>(
    `/api/v1/trips/${tripId}/payments`,
    request,
    { retry: 'never' },
  );
}

export function rateTrip(tripId: string, request: CreateRatingRequest) {
  return apiClient.post<Rating, CreateRatingRequest>(
    `/api/v1/trips/${tripId}/ratings`,
    request,
    { retry: 'never' },
  );
}
