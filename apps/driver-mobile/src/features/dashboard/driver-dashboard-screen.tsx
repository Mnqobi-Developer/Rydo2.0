import { isApiError, type GeoCoordinate } from '@rydo/mobile-api-client';
import { RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import * as Location from 'expo-location';
import { useEffect, useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import Animated, { FadeInDown, FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiClient } from '@/api';
import { DriverAccountCreationScreen } from '@/features/auth/driver-account-creation-screen';
import { DRIVER_LOCATION_TASK } from '@/location/background-location-task';
import { driverTheme } from '@/theme/driver-theme';

import { DriverBottomNavigation, type DriverTab } from './driver-bottom-navigation';
import { DriverAccountCreationPrompt } from './driver-account-creation-prompt';
import { DriverEarningsScreen } from './driver-earnings-screen';
import { DriverRideFlowScreen } from './driver-ride-flow-screen';
import { DriverTripsScreen } from './driver-trips-screen';
import {
  driverProfileQuery,
  driverAvailabilityQuery,
  driverPerformanceQuery,
  driverTripOffersQuery,
  driverTripsQuery,
  type DriverProfile,
} from './api';
import { DriverAccountScreen } from './driver-account-screen';
import { DriverDocumentsScreen } from './driver-documents-screen';
import { DriverVehicleScreen } from './driver-vehicle-screen';
import { DriverWalletScreen } from './driver-wallet-screen';

type AccountScreen = 'overview' | 'vehicle' | 'documents';

export function DriverDashboardScreen() {
  const insets = useSafeAreaInsets();
  const profile = useQuery({ ...driverProfileQuery, retry: false });
  const [locationTaskActive, setLocationTaskActive] = useState(false);
  const [onlineIntent, setOnlineIntent] = useState(false);
  const [manuallyOffline, setManuallyOffline] = useState(false);
  const [actionPending, setActionPending] = useState(false);
  const [activeTab, setActiveTab] = useState<DriverTab>('home');
  const [accountPromptDismissed, setAccountPromptDismissed] = useState(false);
  const [accountScreen, setAccountScreen] = useState<AccountScreen>('overview');
  const [statusMessage, setStatusMessage] = useState('Go online to start receiving ride requests.');
  const [completedTripId, setCompletedTripId] = useState<string | null>(null);
  const foregroundLocationSubscription = useRef<Location.LocationSubscription | null>(null);
  const trips = useQuery(driverTripsQuery);
  const performance = useQuery(driverPerformanceQuery);
  const approved = profile.data?.onboardingStatus === 'Approved';
  const availability = useQuery({
    ...driverAvailabilityQuery,
    enabled: approved,
  });
  const refetchAvailability = availability.refetch;
  const today = useMemo(() => createTodaySummary(trips.data ?? []), [trips.data]);
  const activeTrip = trips.data?.find((trip) =>
    trip.status === 'Accepted' || trip.status === 'DriverArrived' || trip.status === 'InProgress') ?? null;
  const tracking = !manuallyOffline && availability.data?.isOnline === true;
  const offers = useQuery({
    ...driverTripOffersQuery,
    enabled: tracking && approved,
  });
  const completedFlowTrip = completedTripId
    ? trips.data?.find((trip) => trip.id === completedTripId) ?? null
    : null;
  const flowTrip = activeTrip ?? completedFlowTrip;
  const pendingOffer = offers.data?.find((offer) => offer.status === 'Pending') ?? null;
  const reconciliationInFlight = useRef(false);

  useEffect(() => {
    void Location.hasStartedLocationUpdatesAsync(DRIVER_LOCATION_TASK).then((active) => {
      setLocationTaskActive(active);
      if (active) setOnlineIntent(true);
    });
  }, []);

  useEffect(() => {
    if (locationTaskActive || (!tracking && !activeTrip)) return;

    let disposed = false;
    void Location.watchPositionAsync(
      {
        accuracy: Location.Accuracy.High,
        distanceInterval: 25,
        timeInterval: 10_000,
      },
      (location) => {
        void apiClient.post<unknown, GeoCoordinate>(
          '/api/v1/drivers/me/location',
          { latitude: location.coords.latitude, longitude: location.coords.longitude },
          { retry: 'never', timeoutMs: 8_000 },
        ).catch(() => undefined);
      },
    ).then((subscription) => {
      if (disposed) {
        subscription.remove();
      } else {
        foregroundLocationSubscription.current = subscription;
      }
    }).catch(() => {
      setStatusMessage('Keep location services enabled while you are online.');
    });

    return () => {
      disposed = true;
      foregroundLocationSubscription.current?.remove();
      foregroundLocationSubscription.current = null;
    };
  }, [activeTrip, locationTaskActive, tracking]);

  useEffect(() => {
    if (
      !onlineIntent ||
      !approved ||
      activeTrip ||
      trips.isPending ||
      availability.isPending ||
      availability.isError ||
      availability.data?.isOnline ||
      reconciliationInFlight.current
    ) {
      return;
    }

    reconciliationInFlight.current = true;
    void (async () => {
      try {
        const permission = await Location.getForegroundPermissionsAsync();
        if (!permission.granted) {
          setStatusMessage('Location access is required before you can receive ride requests.');
          return;
        }

        const current = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High });
        const coordinate = {
          latitude: current.coords.latitude,
          longitude: current.coords.longitude,
        } satisfies GeoCoordinate;
        await apiClient.post<unknown, GeoCoordinate>('/api/v1/drivers/me/availability/online', coordinate, {
          retry: 'never',
        });
        await refetchAvailability();
        setStatusMessage('You are ready to receive nearby ride requests.');
      } catch {
        setStatusMessage('RYDO could not restore your online status. Tap the power button to try again.');
      }
    })()
      .finally(() => {
        reconciliationInFlight.current = false;
      });
  }, [activeTrip, approved, availability.data?.isOnline, availability.isError, availability.isPending, onlineIntent, refetchAvailability, trips.isPending]);

  const firstName = profile.data?.firstName.trim() || 'Driver';
  const requiresAccountCreation = profile.isError && isApiError(profile.error) && profile.error.status === 404;
  const verificationLabel = approved ? 'Verified driver' : formatOnboardingStatus(profile.data?.onboardingStatus);

  async function locate() {
    const permission = await Location.requestForegroundPermissionsAsync();
    if (!permission.granted) {
      setStatusMessage('Location access is required before you can go online.');
      return null;
    }

    const current = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High });
    return { latitude: current.coords.latitude, longitude: current.coords.longitude } satisfies GeoCoordinate;
  }

  async function enableBackgroundTracking() {
    setActionPending(true);
    try {
      const coordinate = await locate();
      if (!coordinate) return;

      Alert.alert(
        'Allow background location',
        process.env.EXPO_OS === 'android'
          ? 'Choose Allow all the time so RYDO can match trips while you are online.'
          : 'Choose Always so RYDO can match trips while the app is not visible.',
        [
          { text: 'Not now', style: 'cancel' },
          { text: 'Continue', onPress: () => void requestBackgroundPermission(coordinate) },
        ],
      );
    } catch {
      setStatusMessage('Your current location could not be determined. Check location services and try again.');
    } finally {
      setActionPending(false);
    }
  }

  async function requestBackgroundPermission(coordinate: GeoCoordinate) {
    setActionPending(true);
    try {
      const permission = await Location.requestBackgroundPermissionsAsync();
      if (!permission.granted) {
        await apiClient.post<unknown, GeoCoordinate>('/api/v1/drivers/me/availability/online', coordinate, {
          retry: 'never',
        });
        await refetchAvailability();
        setOnlineIntent(true);
        setManuallyOffline(false);
        setStatusMessage('You are online while RYDO Driver remains open. Use a development build for background requests.');
        return;
      }

      await apiClient.post<unknown, GeoCoordinate>('/api/v1/drivers/me/availability/online', coordinate, {
        retry: 'never',
      });
      try {
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
        setLocationTaskActive(true);
      } catch {
        setLocationTaskActive(false);
        setStatusMessage('You are online while RYDO Driver remains open. Use a development build for background requests.');
      }
      await refetchAvailability();
      setOnlineIntent(true);
      setManuallyOffline(false);
      if (await Location.hasStartedLocationUpdatesAsync(DRIVER_LOCATION_TASK)) {
        setStatusMessage('You are ready to receive nearby ride requests.');
      }
    } catch {
      setStatusMessage('Going online requires an approved profile and a Driver development build.');
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
        // The device must stop sharing even if the API is temporarily unavailable.
      }
      setLocationTaskActive(false);
      await refetchAvailability();
      setOnlineIntent(false);
      setManuallyOffline(true);
      setStatusMessage('Go online to start receiving ride requests.');
    } finally {
      setActionPending(false);
    }
  }

  if (activeTab === 'home' && (flowTrip || pendingOffer)) {
    return (
      <View style={styles.page}>
        <DriverRideFlowScreen
          offer={pendingOffer}
          trip={flowTrip}
          onCompleted={(completedTrip) => setCompletedTripId(completedTrip.id)}
          onFinished={() => {
            setCompletedTripId(null);
            void trips.refetch();
            void offers.refetch();
            void refetchAvailability();
          }}
        />
        <DriverBottomNavigation activeTab={activeTab} onSelect={setActiveTab} />
      </View>
    );
  }

  if (activeTab === 'earnings') {
    return (
      <View style={styles.page}>
        <DriverEarningsScreen />
        <DriverBottomNavigation activeTab={activeTab} onSelect={setActiveTab} />
      </View>
    );
  }

  if (activeTab === 'trips') {
    return (
      <View style={styles.page}>
        <DriverTripsScreen />
        <DriverBottomNavigation activeTab={activeTab} onSelect={setActiveTab} />
      </View>
    );
  }

  if (activeTab === 'wallet') {
    return (
      <View style={styles.page}>
        <DriverWalletScreen />
        <DriverBottomNavigation activeTab={activeTab} onSelect={setActiveTab} />
      </View>
    );
  }

  if (activeTab === 'profile') {
    return (
      <View style={styles.page}>
        {requiresAccountCreation ? <DriverAccountCreationScreen embedded /> : accountScreen === 'vehicle' ? (
          <DriverVehicleScreen onBack={() => setAccountScreen('overview')} onOpenDocuments={() => setAccountScreen('documents')} />
        ) : accountScreen === 'documents' ? (
          <DriverDocumentsScreen
            onBack={() => setAccountScreen('overview')}
            onOpenVehicle={() => setAccountScreen('vehicle')}
            onSubmitted={() => setAccountScreen('overview')}
          />
        ) : (
          <DriverAccountScreen onOpenDocuments={() => setAccountScreen('documents')} onOpenVehicle={() => setAccountScreen('vehicle')} />
        )}
        <DriverBottomNavigation
          activeTab={activeTab}
          onSelect={(tab) => {
            if (tab === 'profile') setAccountScreen('overview');
            setActiveTab(tab);
          }}
        />
      </View>
    );
  }

  return (
    <View style={styles.page}>
      <ScrollView
        contentContainerStyle={[
          styles.content,
          { paddingTop: insets.top + spacing.lg, paddingBottom: 112 + insets.bottom },
        ]}
        contentInsetAdjustmentBehavior="automatic"
        showsVerticalScrollIndicator={false}
      >
        <Animated.View entering={FadeInDown.duration(220)} style={styles.topBar}>
          <Pressable accessibilityLabel="Open menu" accessibilityRole="button" style={styles.headerButton}>
            <RydoIcon name="menu" color={colors.navy} size={22} />
          </Pressable>
          <Pressable accessibilityLabel="Notifications" accessibilityRole="button" style={styles.headerButton}>
            <RydoIcon name="bell" color={colors.navy} size={21} />
            <View style={styles.notificationDot} />
          </Pressable>
        </Animated.View>

        <Animated.View entering={FadeInUp.duration(240)} style={styles.identity}>
          <Text selectable style={styles.greeting}>{getGreeting()},</Text>
          <View style={styles.nameRow}>
            <Text numberOfLines={1} selectable style={styles.name}>{firstName}</Text>
            {approved ? <View style={styles.verifiedBadge}><RydoIcon name="check" color={colors.white} size={11} /></View> : null}
          </View>
          <View style={styles.ratingRow}>
            <RydoIcon name="star" color={colors.amber} size={15} />
            <Text selectable style={styles.rating}>New driver</Text>
            <View style={styles.statusDivider} />
            <Text selectable style={styles.verification}>{verificationLabel}</Text>
          </View>
        </Animated.View>

        <Animated.View entering={FadeInUp.delay(60).duration(240)} style={styles.earningsCard}>
          <Text style={styles.cardEyebrow}>TODAY&apos;S EARNINGS</Text>
          <Text selectable style={styles.earningsValue}>{formatCurrency(today.earnings)}</Text>
          <View style={styles.summaryDivider} />
          <View style={styles.summaryRow}>
            <SummaryMetric label="DAILY TRIPS" value={String(today.tripCount)} />
            <View style={styles.verticalDivider} />
            <SummaryMetric label="ONLINE" value={tracking ? 'Active now' : '0h 00m'} />
          </View>
        </Animated.View>

        <Animated.View entering={FadeInUp.delay(110).duration(240)} style={styles.section}>
          <View style={styles.sectionHeader}>
            <Text style={styles.sectionTitle}>Performance</Text>
            <Pressable accessibilityRole="button" style={styles.viewAllButton}>
              <Text style={styles.viewAll}>View all</Text>
              <RydoIcon name="chevron-right" color={colors.blue} size={15} />
            </Pressable>
          </View>
          <View style={styles.performanceCard}>
            <PerformanceMetric label="Acceptance" value={formatPercentage(performance.data?.acceptanceRate)} />
            <View style={styles.verticalDivider} />
            <PerformanceMetric label="Rating" value={formatRating(performance.data?.averageRating)} accent />
            <View style={styles.verticalDivider} />
            <PerformanceMetric label="Completion" value={formatPercentage(performance.data?.completionRate)} />
          </View>
        </Animated.View>

        <Animated.View entering={FadeInUp.delay(160).duration(240)} style={[styles.availabilityCard, tracking && styles.availabilityCardOnline]}>
          <View style={styles.availabilityCopy}>
            <Text style={[styles.availabilityTitle, tracking && styles.availabilityTitleOnline]}>
              {tracking ? "You're online" : "You're offline"}
            </Text>
            <Text selectable style={styles.availabilityMessage}>{statusMessage}</Text>
          </View>
          <Pressable
            accessibilityLabel={tracking ? 'Go offline' : 'Go online'}
            accessibilityRole="button"
            accessibilityState={{ busy: actionPending }}
            disabled={actionPending}
            onPress={() => void (tracking ? stopTracking() : enableBackgroundTracking())}
            style={({ pressed }) => [
              styles.powerButton,
              tracking && styles.powerButtonOnline,
              pressed && styles.powerButtonPressed,
            ]}
          >
            {actionPending
              ? <ActivityIndicator color={tracking ? colors.white : colors.success} size="small" />
              : <RydoIcon name="power" color={tracking ? colors.white : colors.success} size={25} />}
          </Pressable>
        </Animated.View>
      </ScrollView>

      <DriverBottomNavigation activeTab={activeTab} onSelect={setActiveTab} />
      <DriverAccountCreationPrompt
        visible={requiresAccountCreation && !accountPromptDismissed}
        onCreateProfile={() => {
          setAccountPromptDismissed(true);
          setActiveTab('profile');
        }}
        onLater={() => setAccountPromptDismissed(true)}
      />
    </View>
  );
}

function SummaryMetric({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.summaryMetric}>
      <Text style={styles.metricLabel}>{label}</Text>
      <Text selectable style={styles.summaryValue}>{value}</Text>
    </View>
  );
}

function PerformanceMetric({ label, value, accent = false }: { label: string; value: string; accent?: boolean }) {
  return (
    <View style={styles.performanceMetric}>
      <Text selectable style={styles.performanceValue}>{value}</Text>
      {accent ? <RydoIcon name="star" color={colors.amber} size={12} /> : null}
      <Text style={styles.performanceLabel}>{label}</Text>
    </View>
  );
}

function getGreeting() {
  const hour = new Date().getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}

function formatOnboardingStatus(status: DriverProfile['onboardingStatus'] | undefined) {
  if (!status) return 'Profile not completed';
  if (status === 'PendingReview') return 'Verification pending';
  if (status === 'Rejected') return 'Profile needs attention';
  return 'Profile in progress';
}

function createTodaySummary(trips: import('@rydo/mobile-api-client').Trip[]) {
  const now = new Date();
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const completedToday = trips.filter((trip) =>
    trip.status === 'Completed' &&
    new Date(trip.completedAt ?? trip.updatedAt).getTime() >= todayStart);

  return {
    tripCount: completedToday.length,
    earnings: completedToday.reduce(
      (total, trip) => total + (trip.finalFareAmount ?? trip.estimatedFareAmount ?? 0),
      0,
    ),
  };
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-ZA', {
    style: 'currency',
    currency: 'ZAR',
    minimumFractionDigits: 2,
  }).format(value);
}

function formatPercentage(value: number | null | undefined) {
  return value == null ? '--' : `${value.toFixed(value % 1 === 0 ? 0 : 1)}%`;
}

function formatRating(value: number | null | undefined) {
  return value == null ? '--' : value.toFixed(2);
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: driverTheme.colors.background },
  content: { gap: spacing.xl, paddingHorizontal: spacing.xl },
  topBar: { minHeight: 44, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  headerButton: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center', borderRadius: 15, backgroundColor: colors.white },
  notificationDot: { position: 'absolute', top: 8, right: 9, width: 7, height: 7, borderWidth: 1.5, borderColor: colors.white, borderRadius: 4, backgroundColor: colors.danger },
  identity: { gap: 2 },
  greeting: { color: colors.textMuted, fontSize: 13, lineHeight: 18, fontWeight: '600' },
  nameRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  name: { maxWidth: '82%', color: colors.navy, fontSize: 30, lineHeight: 36, fontWeight: '900', letterSpacing: -0.8 },
  verifiedBadge: { width: 19, height: 19, alignItems: 'center', justifyContent: 'center', borderRadius: 10, backgroundColor: colors.blue },
  ratingRow: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingTop: spacing.xs },
  rating: { color: colors.navy, fontSize: 13, fontWeight: '800' },
  statusDivider: { width: 3, height: 3, borderRadius: 2, backgroundColor: colors.border },
  verification: { color: colors.textMuted, fontSize: 12, fontWeight: '600' },
  earningsCard: { gap: spacing.sm, padding: spacing.lg, borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  cardEyebrow: { color: colors.textMuted, fontSize: 10, fontWeight: '900', letterSpacing: 0.75 },
  earningsValue: { color: '#16A36B', fontSize: 28, lineHeight: 34, fontWeight: '900', fontVariant: ['tabular-nums'] },
  summaryDivider: { height: 1, backgroundColor: driverTheme.colors.softBorder },
  summaryRow: { minHeight: 56, flexDirection: 'row', alignItems: 'stretch' },
  summaryMetric: { flex: 1, justifyContent: 'center', gap: 3 },
  verticalDivider: { width: 1, alignSelf: 'stretch', backgroundColor: driverTheme.colors.softBorder },
  metricLabel: { color: colors.textMuted, fontSize: 10, fontWeight: '800', letterSpacing: 0.6 },
  summaryValue: { color: colors.navy, fontSize: 17, lineHeight: 22, fontWeight: '900', fontVariant: ['tabular-nums'] },
  section: { gap: spacing.md },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  sectionTitle: { color: colors.navy, fontSize: 16, fontWeight: '800' },
  viewAllButton: { flexDirection: 'row', alignItems: 'center', paddingVertical: spacing.xs },
  viewAll: { color: colors.blue, fontSize: 12, fontWeight: '800' },
  performanceCard: { minHeight: 88, flexDirection: 'row', alignItems: 'stretch', paddingVertical: spacing.md, borderRadius: driverTheme.radii.banner, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  performanceMetric: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 2 },
  performanceValue: { color: colors.navy, fontSize: 17, fontWeight: '900', fontVariant: ['tabular-nums'] },
  performanceLabel: { color: colors.textMuted, fontSize: 10, fontWeight: '600' },
  availabilityCard: { minHeight: 112, flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: '#E9F8F3' },
  availabilityCardOnline: { backgroundColor: colors.blueMuted },
  availabilityCopy: { minWidth: 0, flex: 1, gap: spacing.xs },
  availabilityTitle: { color: '#178A55', fontSize: 17, fontWeight: '900' },
  availabilityTitleOnline: { color: colors.blue },
  availabilityMessage: { color: colors.textMuted, fontSize: 12, lineHeight: 17 },
  powerButton: { width: 64, height: 64, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: '#D7EEE4', borderRadius: 32, backgroundColor: colors.white, boxShadow: '0 8px 20px rgba(23,138,85,0.10)' },
  powerButtonOnline: { borderColor: colors.blue, backgroundColor: colors.blue },
  powerButtonPressed: { transform: [{ scale: 0.94 }] },
});
