import type { GeoCoordinate } from '@rydo/mobile-api-client';
import { RydoBottomSheet, RydoButton, colors, spacing, typography } from '@rydo/mobile-design-system';
import * as Location from 'expo-location';
import { useEffect, useState } from 'react';
import { Alert, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiClient } from '@/api';
import { DRIVER_LOCATION_TASK } from '@/location/background-location-task';

import { DriverMap } from './driver-map';

export function DriverLocationScreen() {
  const insets = useSafeAreaInsets();
  const [location, setLocation] = useState<GeoCoordinate | null>(null);
  const [tracking, setTracking] = useState(false);
  const [message, setMessage] = useState('Location sharing starts only when you choose to go online.');

  useEffect(() => {
    void Location.hasStartedLocationUpdatesAsync(DRIVER_LOCATION_TASK).then(setTracking);
  }, []);

  async function locate() {
    const permission = await Location.requestForegroundPermissionsAsync();
    if (!permission.granted) {
      setMessage('Foreground location permission was not granted.');
      return null;
    }
    const current = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High });
    const coordinate = { latitude: current.coords.latitude, longitude: current.coords.longitude };
    setLocation(coordinate);
    return coordinate;
  }

  async function enableBackgroundTracking() {
    const coordinate = await locate();
    if (!coordinate) return;

    Alert.alert(
      'Allow background location',
      process.env.EXPO_OS === 'android'
        ? 'Android may open Settings. Choose Allow all the time so passengers can see your position while you are online.'
        : 'Choose Always so active trips keep receiving your position when the app is not visible.',
      [
        { text: 'Not now', style: 'cancel' },
        { text: 'Continue', onPress: () => void requestBackgroundPermission(coordinate) },
      ],
    );
  }

  async function requestBackgroundPermission(coordinate: GeoCoordinate) {
    const permission = await Location.requestBackgroundPermissionsAsync();
    if (!permission.granted) {
      setMessage('Background permission was not granted. You can enable it later in Settings.');
      return;
    }

    try {
      await apiClient.post<unknown, GeoCoordinate>('/api/v1/drivers/me/availability/online', coordinate, {
        retry: 'never',
      });
    } catch {
      setMessage('Sign in and complete driver approval before going online.');
      return;
    }

    await Location.startLocationUpdatesAsync(DRIVER_LOCATION_TASK, {
      accuracy: Location.Accuracy.High,
      distanceInterval: 25,
      timeInterval: 10_000,
      activityType: Location.ActivityType.AutomotiveNavigation,
      pausesUpdatesAutomatically: false,
      showsBackgroundLocationIndicator: true,
      foregroundService: {
        notificationTitle: 'RYDO Driver is online',
        notificationBody: 'Sharing your location for ride matching and active trips.',
        killServiceOnDestroy: false,
      },
    });
    setTracking(true);
    setMessage('Background location sharing is active.');
  }

  async function stopTracking() {
    if (await Location.hasStartedLocationUpdatesAsync(DRIVER_LOCATION_TASK)) {
      await Location.stopLocationUpdatesAsync(DRIVER_LOCATION_TASK);
    }
    try {
      await apiClient.request<unknown>('/api/v1/drivers/me/availability/offline', {
        method: 'POST',
        retry: 'never',
      });
    } catch {
      // Local background sharing still stops when the API is unavailable.
    }
    setTracking(false);
    setMessage('You are offline. Background location sharing has stopped.');
  }

  return (
    <View style={styles.container}>
      <DriverMap location={location} />
      <RydoBottomSheet snapPoints={['42%', '62%']} bottomInset={insets.bottom + spacing.md}>
        <View style={styles.headingRow}>
          <View style={[styles.statusDot, { backgroundColor: tracking ? colors.success : colors.textMuted }]} />
          <Text selectable style={styles.title}>{tracking ? 'You are online' : 'You are offline'}</Text>
        </View>
        <Text selectable style={styles.body}>{message}</Text>
        <RydoButton label="Use current location" variant="secondary" onPress={() => void locate()} />
        <RydoButton
          label={tracking ? 'Go offline' : 'Enable background tracking'}
          variant={tracking ? 'danger' : 'primary'}
          onPress={() => void (tracking ? stopTracking() : enableBackgroundTracking())}
        />
        <Text selectable style={styles.privacy}>
          Background location is used only while you are online for matching and active-trip tracking.
        </Text>
      </RydoBottomSheet>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  headingRow: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  statusDot: { width: 12, height: 12, borderRadius: 6 },
  title: { color: colors.navy, fontSize: typography.size.title, fontWeight: typography.weight.bold },
  body: { color: colors.textMuted, lineHeight: 21 },
  privacy: { color: colors.textMuted, fontSize: 12, lineHeight: 17 },
});
