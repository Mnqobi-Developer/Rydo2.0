import { isApiError, type PassengerProfile, type UpdatePassengerProfileRequest } from '@rydo/mobile-api-client';
import { queryOptions } from '@tanstack/react-query';

import { apiClient } from '@/api';

export const passengerProfileKey = ['passenger', 'profile'] as const;

export const passengerProfileQuery = queryOptions({
  queryKey: passengerProfileKey,
  queryFn: async ({ signal }) => {
    try {
      return await apiClient.get<PassengerProfile>('/api/v1/passengers/me/profile', { signal });
    } catch (error) {
      if (isApiError(error) && error.status === 404) return null;
      throw error;
    }
  },
});

export function savePassengerProfile(request: UpdatePassengerProfileRequest) {
  return apiClient.put<PassengerProfile, UpdatePassengerProfileRequest>(
    '/api/v1/passengers/me/profile',
    request,
    { retry: 'never' },
  );
}
