import type { GeoCoordinate } from '@rydo/mobile-api-client';
import { RydoBottomSheet, RydoButton, RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import * as Location from 'expo-location';
import { useEffect, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import Animated, { FadeInDown, FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiClient } from '@/api';
import { DriverRideIcon } from '@/components/driver-ride-icon';
import { DRIVER_LOCATION_TASK } from '@/location/background-location-task';
import { driverTheme } from '@/theme/driver-theme';

import { DriverMap } from './driver-map';

export function DriverLocationScreen() {
  const insets = useSafeAreaInsets();
  const [location, setLocation] = useState<GeoCoordinate | null>(null);
  const [tracking, setTracking] = useState(false);
  const [actionPending, setActionPending] = useState(false);
  const [message, setMessage] = useState('Go online when you are ready to receive nearby ride requests.');

  useEffect(() => {
    void Location.hasStartedLocationUpdatesAsync(DRIVER_LOCATION_TASK).then(setTracking);
    void restoreLastLocation();

    async function restoreLastLocation() {
      const permission = await Location.getForegroundPermissionsAsync();
      if (!permission.granted) return;
      const lastKnown = await Location.getLastKnownPositionAsync({ maxAge: 120_000, requiredAccuracy: 250 });
      if (lastKnown) {
        setLocation({ latitude: lastKnown.coords.latitude, longitude: lastKnown.coords.longitude });
      }
    }
  }, []);

  async function locate() {
    setActionPending(true);
    try {
      const permission = await Location.requestForegroundPermissionsAsync();
      if (!permission.granted) {
        setMessage('Location access is required to position you for ride requests.');
        return null;
      }
      const current = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High });
      const coordinate = { latitude: current.coords.latitude, longitude: current.coords.longitude };
      setLocation(coordinate);
      return coordinate;
    } catch {
      setMessage('Your current location could not be determined. Check device location services and try again.');
      return null;
    } finally {
      setActionPending(false);
    }
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
    setActionPending(true);
    try {
      const permission = await Location.requestBackgroundPermissionsAsync();
      if (!permission.granted) {
        setMessage('Background access was not granted. You can enable it later in device Settings.');
        return;
      }

      await apiClient.post<unknown, GeoCoordinate>('/api/v1/drivers/me/availability/online', coordinate, {
        retry: 'never',
      });

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
      setMessage('You are visible for nearby ride requests.');
    } catch {
      setMessage(
        'Going online requires an approved driver profile and a Driver development build with background location enabled.',
      );
    } finally {
      setActionPending(false);
    }
  }

  async function stopTracking() {
    setActionPending(true);
    try {
      if (await Location.hasStartedLocationUpdatesAsync(DRIVER_LOCATION_TASK)) {
        await Location.stopLocationUpdatesAsync(DRIVER_LOCATION_TASK);
      }
      try {
        await apiClient.request<unknown>('/api/v1/drivers/me/availability/offline', {
          method: 'POST',
          retry: 'never',
        });
      } catch {
        // Local background sharing must still stop if the API is unavailable.
      }
      setTracking(false);
      setMessage('You are offline. Location sharing has stopped.');
    } finally {
      setActionPending(false);
    }
  }

  return (
    <View style={styles.container}>
      <DriverMap location={location} />

      <Animated.View entering={FadeInDown.duration(240)} style={[styles.mapHeader, { top: insets.top + spacing.md }]}>
        <View style={styles.driverPill}>
          <View style={[styles.smallStatusDot, tracking && styles.smallStatusDotOnline]} />
          <View style={styles.driverPillCopy}>
            <Text style={styles.driverPillTitle}>Driver mode</Text>
            <Text style={styles.driverPillStatus}>{tracking ? 'Online' : 'Offline'}</Text>
          </View>
        </View>
        <Pressable
          accessibilityLabel="Center map on my location"
          accessibilityRole="button"
          android_ripple={{ color: 'rgba(36,87,255,0.10)', borderless: true }}
          onPress={() => void locate()}
          style={({ pressed }) => [styles.mapControl, pressed && styles.controlPressed]}
        >
          <RydoIcon name="location" color={colors.blue} size={23} />
        </Pressable>
      </Animated.View>

      <RydoBottomSheet
        bottomInset={insets.bottom + spacing.md}
        contentStyle={styles.sheetContent}
        snapPoints={['44%', '68%']}
      >
        <Animated.View entering={FadeInUp.duration(240)} style={styles.headingArea}>
          <Text selectable style={styles.eyebrow}>{tracking ? 'YOU ARE ONLINE' : 'DRIVER AVAILABILITY'}</Text>
          <Text selectable style={styles.title}>{tracking ? 'Ready for ride requests' : 'Ready to start driving?'}</Text>
          <Text selectable style={styles.body}>{message}</Text>
        </Animated.View>

        <View style={[styles.statusCard, tracking && styles.statusCardOnline]}>
          <View style={[styles.statusIcon, tracking && styles.statusIconOnline]}>
            {tracking
              ? <RydoIcon name="check" color={colors.success} size={24} />
              : <DriverRideIcon color={colors.blue} size={24} />}
          </View>
          <View style={styles.statusCopy}>
            <Text style={styles.statusTitle}>{tracking ? 'Accepting requests' : 'Not accepting requests'}</Text>
            <Text style={styles.statusDescription}>
              {tracking ? 'Your location is shared for matching.' : 'Passengers cannot currently match with you.'}
            </Text>
          </View>
        </View>

        <RydoButton
          label="Use current location"
          leading={<RydoIcon name="location" color={colors.blue} size={19} />}
          loading={actionPending && !tracking}
          variant="secondary"
          onPress={() => void locate()}
        />
        <RydoButton
          label={tracking ? 'Go offline' : 'Go online'}
          loading={actionPending}
          variant={tracking ? 'danger' : 'primary'}
          onPress={() => void (tracking ? stopTracking() : enableBackgroundTracking())}
        />

        <View style={styles.privacyRow}>
          <RydoIcon name="shield" color={colors.textMuted} size={16} />
          <Text selectable style={styles.privacy}>
            Background location is used only while online for matching and active-trip tracking.
          </Text>
        </View>
      </RydoBottomSheet>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: driverTheme.colors.background },
  mapHeader: { position: 'absolute', left: spacing.lg, right: spacing.lg, zIndex: 4, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  driverPill: { minHeight: 58, flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, borderRadius: 20, backgroundColor: 'rgba(255,255,255,0.96)', boxShadow: driverTheme.shadows.control },
  smallStatusDot: { width: 10, height: 10, borderRadius: 5, backgroundColor: colors.textMuted },
  smallStatusDotOnline: { backgroundColor: colors.success },
  driverPillCopy: { gap: 1 },
  driverPillTitle: { color: colors.navy, fontSize: 14, fontWeight: '800' },
  driverPillStatus: { color: colors.textMuted, fontSize: 12 },
  mapControl: { width: 52, height: 52, alignItems: 'center', justifyContent: 'center', borderRadius: 18, backgroundColor: 'rgba(255,255,255,0.96)', boxShadow: driverTheme.shadows.control },
  controlPressed: { opacity: 0.8, transform: [{ scale: 0.95 }] },
  sheetContent: { gap: spacing.lg },
  headingArea: { gap: spacing.xs },
  eyebrow: { color: colors.blue, fontSize: 11, lineHeight: 16, fontWeight: '900', letterSpacing: 0.9 },
  title: { color: colors.navy, fontSize: 25, lineHeight: 31, fontWeight: '900', letterSpacing: -0.5 },
  body: { color: colors.textMuted, fontSize: 14, lineHeight: 20 },
  statusCard: { minHeight: 76, flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.md, borderRadius: driverTheme.radii.banner, backgroundColor: colors.blueMuted },
  statusCardOnline: { backgroundColor: driverTheme.colors.onlineSoft },
  statusIcon: { width: 48, height: 48, alignItems: 'center', justifyContent: 'center', borderRadius: 16, backgroundColor: colors.white },
  statusIconOnline: { backgroundColor: 'rgba(255,255,255,0.82)' },
  statusCopy: { minWidth: 0, flex: 1, gap: 2 },
  statusTitle: { color: colors.navy, fontSize: 15, lineHeight: 20, fontWeight: '800' },
  statusDescription: { color: colors.textMuted, fontSize: 12, lineHeight: 17 },
  privacyRow: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.sm, paddingHorizontal: spacing.xs },
  privacy: { minWidth: 0, flex: 1, color: colors.textMuted, fontSize: 11, lineHeight: 16 },
});
