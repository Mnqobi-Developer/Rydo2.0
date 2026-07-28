import type { Trip } from '@rydo/mobile-api-client';
import { queryOptions } from '@tanstack/react-query';

import { apiClient } from '@/api';

export const passengerTripsKey = ['trips'] as const;

export const passengerTripsQuery = queryOptions({
  queryKey: passengerTripsKey,
  queryFn: ({ signal }) => apiClient.get<Trip[]>('/api/v1/trips/me', { signal }),
});
