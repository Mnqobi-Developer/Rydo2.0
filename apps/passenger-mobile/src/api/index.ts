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

const nativeSecureStorage: AsyncKeyValueStorage = {
  getItem: (key) => SecureStore.getItemAsync(key),
  setItem: (key, value) =>
    SecureStore.setItemAsync(key, value, {
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    }),
  removeItem: (key) => SecureStore.deleteItemAsync(key),
};

const webMemory = new Map<string, string>();
const webMemoryStorage: AsyncKeyValueStorage = {
  getItem: async (key) => webMemory.get(key) ?? null,
  setItem: async (key, value) => {
    webMemory.set(key, value);
  },
  removeItem: async (key) => {
    webMemory.delete(key);
  },
};

const authenticationStorage =
  process.env.EXPO_OS === 'web' ? webMemoryStorage : nativeSecureStorage;

const tokenStore = createSerializedTokenStore(
  authenticationStorage,
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
