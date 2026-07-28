import {
  createApiClient,
  createMobileQueryClient,
  createSerializedTokenStore,
  type AsyncKeyValueStorage,
} from '@rydo/mobile-api-client';
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
  'rydo.driver.authentication',
);

export const queryClient = createMobileQueryClient();

export const apiClient = createApiClient({
  baseUrl: appConfig.apiBaseUrl,
  tokenStore,
  fetch,
  onAuthenticationExpired: () => {
    queryClient.clear();
  },
});
