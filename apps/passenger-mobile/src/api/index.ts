import {
  createApiClient,
  createSerializedTokenStore,
  mobileQueryClientOptions,
  type AsyncKeyValueStorage,
} from '@rydo/mobile-api-client';
import { QueryClient } from '@tanstack/react-query';
import { fetch } from 'expo/fetch';
import * as SecureStore from 'expo-secure-store';

import { appConfig } from '@/config/environment';

const secureStorage: AsyncKeyValueStorage = {
  getItem: (key) => SecureStore.getItemAsync(key),
  setItem: (key, value) => SecureStore.setItemAsync(key, value),
  removeItem: (key) => SecureStore.deleteItemAsync(key),
};

const tokenStore = createSerializedTokenStore(
  secureStorage,
  'rydo.passenger.authentication',
);

export const queryClient = new QueryClient(mobileQueryClientOptions);

export const apiClient = createApiClient({
  baseUrl: appConfig.apiBaseUrl,
  tokenStore,
  fetch,
  onAuthenticationExpired: () => {
    queryClient.clear();
  },
});
