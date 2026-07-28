import {
  createOperationsRealtimeClient,
  type OperationsRealtimeClient,
} from '@rydo/mobile-api-client';

import { apiClient, queryClient } from '@/api';
import { appConfig } from '@/config/environment';

import { createSignalRTransport } from './transport';

let realtimeClient: OperationsRealtimeClient | null = null;

export function getRealtimeClient() {
  realtimeClient ??= createOperationsRealtimeClient({
    baseUrl: appConfig.apiBaseUrl,
    getAccessToken: () => apiClient.auth.getAccessToken(),
    connectionFactory: createSignalRTransport,
    handlers: {
      TripUpdated: (trip) => {
        void queryClient.invalidateQueries({ queryKey: ['trips'] });
        void queryClient.invalidateQueries({ queryKey: ['trip', trip.id] });
      },
      TripOfferUpdated: (offer) => {
        void queryClient.invalidateQueries({ queryKey: ['trip-offers'] });
        void queryClient.invalidateQueries({ queryKey: ['trip-offer', offer.tripId] });
      },
      DriverAvailabilityUpdated: () => {
        void queryClient.invalidateQueries({ queryKey: ['driver-availability'] });
      },
      PaymentUpdated: (payment) => {
        void queryClient.invalidateQueries({ queryKey: ['payment', payment.tripId] });
      },
      DisputeUpdated: (dispute) => {
        void queryClient.invalidateQueries({ queryKey: ['disputes'] });
        void queryClient.invalidateQueries({ queryKey: ['dispute', dispute.id] });
      },
      DriverReviewUpdated: () => {
        void queryClient.invalidateQueries({ queryKey: ['driver-profile'] });
      },
    },
    refreshRestState: () => queryClient.invalidateQueries({ refetchType: 'active' }),
  });
  return realtimeClient;
}
