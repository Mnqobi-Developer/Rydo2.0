import * as Location from 'expo-location';
import * as TaskManager from 'expo-task-manager';

import { apiClient } from '@/api';

export const DRIVER_LOCATION_TASK = 'rydo-driver-background-location';

interface DriverLocationTaskData {
  locations: Location.LocationObject[];
}

TaskManager.defineTask<DriverLocationTaskData>(DRIVER_LOCATION_TASK, async ({ data, error }) => {
  if (error || !data.locations.length) return;

  const latest = data.locations.at(-1);
  if (!latest) return;

  try {
    await apiClient.post<unknown, { latitude: number; longitude: number }>(
      '/api/v1/drivers/me/location',
      { latitude: latest.coords.latitude, longitude: latest.coords.longitude },
      { retry: 'never', timeoutMs: 8_000 },
    );
  } catch {
    // The next location update retries naturally; credentials stay in SecureStore.
  }
});
