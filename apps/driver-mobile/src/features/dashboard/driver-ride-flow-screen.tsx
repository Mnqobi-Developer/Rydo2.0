import {
  decodeGooglePolyline,
  isApiError,
  type GeoCoordinate,
  type Payment,
  type Rating,
  type RoutePreview,
  type Trip,
  type TripOffer,
} from '@rydo/mobile-api-client';
import { RydoButton, RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as Location from 'expo-location';
import { useEffect, useState } from 'react';
import {
  Alert,
  Linking,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import Animated, { FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiClient } from '@/api';
import { DriverRideIcon } from '@/components/driver-ride-icon';
import { driverTheme } from '@/theme/driver-theme';

import {
  driverTripOffersKey,
  driverPerformanceKey,
  driverTripPaymentKey,
  driverTripPaymentQuery,
  driverTripRatingKey,
  driverTripRatingQuery,
  driverTripsKey,
  rateDriverTrip,
} from './api';
import { DriverNavigationMap } from './driver-navigation-map';

export function DriverRideFlowScreen({
  offer,
  trip,
  onCompleted,
  onFinished,
}: {
  offer: TripOffer | null;
  trip: Trip | null;
  onCompleted(trip: Trip): void;
  onFinished(): void;
}) {
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const [ratingScore, setRatingScore] = useState(5);
  const [ratingComment, setRatingComment] = useState('');
  const [currentLocation, setCurrentLocation] = useState<GeoCoordinate | null>(null);
  const tripId = trip?.id ?? '';
  const pickup = offer
    ? { latitude: offer.pickupLatitude, longitude: offer.pickupLongitude }
    : trip
      ? { latitude: trip.pickupLatitude, longitude: trip.pickupLongitude }
      : null;
  const destination = offer
    ? { latitude: offer.destinationLatitude, longitude: offer.destinationLongitude }
    : trip
      ? { latitude: trip.destinationLatitude, longitude: trip.destinationLongitude }
      : null;
  const navigationTarget = trip?.status === 'InProgress' ? destination : pickup;
  const route = useQuery({
    queryKey: [
      'driver-navigation-route',
      roundedCoordinate(currentLocation),
      navigationTarget,
      trip?.status ?? 'Offer',
    ],
    enabled: Boolean(currentLocation && navigationTarget),
    queryFn: ({ signal }) => apiClient.post<RoutePreview, { origin: GeoCoordinate; destination: GeoCoordinate }>(
      '/api/v1/maps/routes',
      { origin: currentLocation!, destination: navigationTarget! },
      { signal },
    ),
    staleTime: 20_000,
  });
  const routeCoordinates = route.data?.encodedPolyline
    ? decodeGooglePolyline(route.data.encodedPolyline)
    : [];
  const payment = useQuery({
    ...driverTripPaymentQuery(tripId),
    enabled: trip?.status === 'Completed',
    refetchInterval: trip?.status === 'Completed' ? 5_000 : false,
  });
  const rating = useQuery({
    ...driverTripRatingQuery(tripId),
    enabled: trip?.status === 'Completed',
  });

  useEffect(() => {
    let active = true;
    let subscription: Location.LocationSubscription | null = null;

    void (async () => {
      const permission = await Location.getForegroundPermissionsAsync();
      if (!permission.granted || !active) return;
      const lastKnown = await Location.getLastKnownPositionAsync({ maxAge: 30_000 });
      if (lastKnown && active) {
        setCurrentLocation(toCoordinate(lastKnown));
      }
      subscription = await Location.watchPositionAsync(
        {
          accuracy: Location.Accuracy.High,
          distanceInterval: 20,
          timeInterval: 5_000,
        },
        (position) => setCurrentLocation(toCoordinate(position)),
      );
      if (!active) subscription.remove();
    })().catch(() => undefined);

    return () => {
      active = false;
      subscription?.remove();
    };
  }, []);

  const accept = useMutation({
    mutationFn: (tripIdToAccept: string) => apiClient.request<Trip>(
      `/api/v1/trips/${tripIdToAccept}/accept`,
      { method: 'POST', retry: 'never' },
    ),
    onSuccess: updateTrip,
  });
  const decline = useMutation({
    mutationFn: (tripIdToDecline: string) => apiClient.request<TripOffer>(
      `/api/v1/drivers/me/trip-offers/${tripIdToDecline}/decline`,
      { method: 'POST', retry: 'never' },
    ),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: driverTripOffersKey }),
  });
  const transition = useMutation({
    mutationFn: ({ tripId: targetTripId, action }: { tripId: string; action: 'arrive' | 'start' | 'complete' }) =>
      apiClient.request<Trip>(`/api/v1/trips/${targetTripId}/${action}`, {
        method: 'POST',
        retry: 'never',
      }),
    onSuccess: (updatedTrip) => {
      updateTrip(updatedTrip);
      if (updatedTrip.status === 'Completed') onCompleted(updatedTrip);
    },
  });
  const cancel = useMutation({
    mutationFn: (targetTripId: string) => apiClient.post<Trip, { reason: string }>(
      `/api/v1/trips/${targetTripId}/cancel`,
      { reason: 'Driver cancelled the ride' },
      { retry: 'never' },
    ),
    onSuccess: (updatedTrip) => {
      updateTrip(updatedTrip);
      onFinished();
    },
  });
  const confirmCash = useMutation({
    mutationFn: (paymentId: string) => apiClient.request<Payment>(
      `/api/v1/payments/${paymentId}/cash/confirm`,
      { method: 'POST', retry: 'never' },
    ),
    onSuccess: (updatedPayment) => {
      queryClient.setQueryData(driverTripPaymentKey(updatedPayment.tripId), updatedPayment);
    },
  });
  const submitRating = useMutation({
    mutationFn: () => rateDriverTrip(tripId, {
      score: ratingScore,
      comment: ratingComment.trim() || null,
    }),
    onSuccess: (result) => queryClient.setQueryData(driverTripRatingKey(result.tripId), result),
  });

  function updateTrip(updatedTrip: Trip) {
    queryClient.setQueryData<Trip[]>(driverTripsKey, (current = []) => [
      updatedTrip,
      ...current.filter((item) => item.id !== updatedTrip.id),
    ]);
    void queryClient.invalidateQueries({ queryKey: driverTripsKey });
    void queryClient.invalidateQueries({ queryKey: driverTripOffersKey });
    void queryClient.invalidateQueries({ queryKey: driverPerformanceKey });
  }

  const error = accept.error ?? decline.error ?? transition.error ?? cancel.error;
  const errorMessage = error
    ? isApiError(error)
      ? error.problem?.detail ?? error.message
      : 'The ride state could not be updated. Refresh and try again.'
    : undefined;

  return (
    <ScrollView
      contentContainerStyle={[
        styles.content,
        { paddingTop: insets.top + spacing.xl, paddingBottom: insets.bottom + spacing.xxl },
      ]}
      contentInsetAdjustmentBehavior="automatic"
      keyboardShouldPersistTaps="handled"
      showsVerticalScrollIndicator={false}
    >
      {offer && !trip ? (
        <Animated.View entering={FadeInUp.duration(220)} style={styles.offerCard}>
          <View style={styles.offerHeader}>
            <View style={styles.offerIcon}><DriverRideIcon color={colors.white} size={28} /></View>
            <View style={styles.headingCopy}>
              <Text selectable style={styles.eyebrow}>NEW RIDE REQUEST</Text>
              <Text selectable style={styles.title}>Passenger nearby</Text>
              <Text selectable style={styles.subtitle}>
                {offer.pickupDistanceKilometres.toFixed(1)} km from your current location
              </Text>
            </View>
          </View>
          {pickup && destination ? (
            <NavigationMapCard
              currentLocation={currentLocation}
              destination={destination}
              pickup={pickup}
              route={routeCoordinates}
              routeError={route.isError}
              routePreview={route.data}
              targetLabel="Passenger pickup"
            />
          ) : null}
          <RouteSummary pickup={offer.pickupAddress} destination={offer.destinationAddress} />
          <View style={styles.offerFacts}>
            <Text selectable style={styles.offerFact}>
              {offer.rideCategory ?? 'Ride'}
            </Text>
            {offer.estimatedFareAmount != null ? (
              <Text selectable style={styles.offerFare}>
                {new Intl.NumberFormat('en-ZA', {
                  style: 'currency',
                  currency: offer.fareCurrency ?? 'ZAR',
                }).format(offer.estimatedFareAmount)}
              </Text>
            ) : null}
          </View>
          <Text selectable style={styles.expiryText}>
            Offer expires {new Intl.DateTimeFormat('en-ZA', { hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(new Date(offer.expiresAt))}
          </Text>
          {errorMessage ? <Text selectable style={styles.error}>{errorMessage}</Text> : null}
          <View style={styles.actionRow}>
            <View style={styles.actionHalf}>
              <RydoButton
                label="Decline"
                loading={decline.isPending}
                variant="secondary"
                onPress={() => decline.mutate(offer.tripId)}
              />
            </View>
            <View style={styles.actionHalf}>
              <RydoButton
                label="Accept"
                loading={accept.isPending}
                onPress={() => accept.mutate(offer.tripId)}
              />
            </View>
          </View>
        </Animated.View>
      ) : trip ? (
        <Animated.View entering={FadeInUp.duration(220)} style={styles.tripFlow}>
          <View style={styles.tripHeader}>
            <View style={[styles.stateIcon, trip.status === 'Completed' && styles.stateIconComplete]}>
              <RydoIcon name={trip.status === 'Completed' ? 'check' : 'car'} color={colors.white} size={24} />
            </View>
            <View style={styles.headingCopy}>
              <Text selectable style={styles.eyebrow}>{formatStatus(trip.status).toUpperCase()}</Text>
              <Text selectable style={styles.title}>{driverStateTitle(trip.status)}</Text>
              <Text selectable style={styles.subtitle}>{driverStateMessage(trip.status)}</Text>
            </View>
          </View>

          {pickup && destination && trip.status !== 'Completed' ? (
            <NavigationMapCard
              currentLocation={currentLocation}
              destination={destination}
              pickup={pickup}
              route={routeCoordinates}
              routeError={route.isError}
              routePreview={route.data}
              targetLabel={trip.status === 'InProgress' ? 'Passenger destination' : 'Passenger pickup'}
            />
          ) : null}

          <RouteSummary pickup={trip.pickupAddress} destination={trip.destinationAddress} />
          <View style={styles.fareRow}>
            <Text selectable style={styles.tripReference}>Trip {trip.id.slice(0, 8).toUpperCase()}</Text>
            <Text selectable style={styles.fare}>{formatFare(trip)}</Text>
          </View>

          {trip.status !== 'Completed' ? (
            <RydoButton
              label={trip.status === 'InProgress' ? 'Navigate to destination' : 'Navigate to pickup'}
              leading={<RydoIcon name="location" color={colors.blue} size={18} />}
              variant="secondary"
              onPress={() => void openNavigation(trip)}
            />
          ) : null}

          {errorMessage ? <Text selectable style={styles.error}>{errorMessage}</Text> : null}

          {trip.status === 'Accepted' ? (
            <RydoButton label="I’ve arrived" loading={transition.isPending} onPress={() => transition.mutate({ tripId: trip.id, action: 'arrive' })} />
          ) : trip.status === 'DriverArrived' ? (
            <RydoButton label="Start ride" loading={transition.isPending} onPress={() => transition.mutate({ tripId: trip.id, action: 'start' })} />
          ) : trip.status === 'InProgress' ? (
            <RydoButton label="Complete ride" loading={transition.isPending} onPress={() => transition.mutate({ tripId: trip.id, action: 'complete' })} />
          ) : null}

          {trip.status === 'Accepted' || trip.status === 'DriverArrived' ? (
            <Pressable
              accessibilityRole="button"
              onPress={() => Alert.alert(
                'Cancel this ride?',
                'The passenger will be notified immediately.',
                [
                  { text: 'Keep ride', style: 'cancel' },
                  { text: 'Cancel ride', style: 'destructive', onPress: () => cancel.mutate(trip.id) },
                ],
              )}
              style={styles.cancelButton}
            >
              <Text style={styles.cancelLabel}>Cancel ride</Text>
            </Pressable>
          ) : null}

          {trip.status === 'Completed' ? (
            <DriverCompletion
              payment={payment.data ?? null}
              paymentLoading={payment.isLoading || payment.isRefetching}
              paymentPending={confirmCash.isPending}
              rating={rating.data ?? null}
              ratingComment={ratingComment}
              ratingPending={submitRating.isPending}
              ratingScore={ratingScore}
              onChangeComment={setRatingComment}
              onChangeScore={setRatingScore}
              onConfirmCash={() => payment.data && confirmCash.mutate(payment.data.id)}
              onDone={onFinished}
              onSubmitRating={() => submitRating.mutate()}
            />
          ) : null}
        </Animated.View>
      ) : null}
    </ScrollView>
  );
}

function RouteSummary({ pickup, destination }: { pickup: string; destination: string }) {
  return (
    <View style={styles.routeCard}>
      <RoutePoint color={driverTheme.colors.online} label="Pickup" address={pickup} />
      <View style={styles.routeLine} />
      <RoutePoint color={colors.blue} label="Destination" address={destination} />
    </View>
  );
}

function NavigationMapCard({
  currentLocation,
  destination,
  pickup,
  route,
  routeError,
  routePreview,
  targetLabel,
}: {
  currentLocation: GeoCoordinate | null;
  destination: GeoCoordinate;
  pickup: GeoCoordinate;
  route: GeoCoordinate[];
  routeError: boolean;
  routePreview: RoutePreview | undefined;
  targetLabel: string;
}) {
  return (
    <View style={styles.navigationCard}>
      <View style={styles.navigationMap}>
        <DriverNavigationMap
          currentLocation={currentLocation}
          destination={destination}
          pickup={pickup}
          route={route}
        />
      </View>
      <View style={styles.navigationMeta}>
        <View style={styles.navigationTargetIcon}>
          <RydoIcon name="location" color={colors.blue} size={17} />
        </View>
        <View style={styles.navigationCopy}>
          <Text selectable style={styles.navigationTarget}>{targetLabel}</Text>
          <Text selectable style={styles.navigationDetail}>
            {routePreview
              ? `${formatDistance(routePreview.distanceMeters)} · ${formatDuration(routePreview.durationSeconds)}`
              : routeError
                ? 'Route preview unavailable. Tap Navigate to continue in Google Maps.'
              : currentLocation
                ? 'Calculating the best driving route…'
                : 'Waiting for your current location…'}
          </Text>
        </View>
      </View>
    </View>
  );
}

function RoutePoint({ color, label, address }: { color: string; label: string; address: string }) {
  return (
    <View style={styles.routePoint}>
      <View style={[styles.routeDot, { backgroundColor: color }]} />
      <View style={styles.routeCopy}>
        <Text style={styles.routeLabel}>{label}</Text>
        <Text numberOfLines={2} selectable style={styles.address}>{address}</Text>
      </View>
    </View>
  );
}

function DriverCompletion({
  payment,
  paymentLoading,
  paymentPending,
  rating,
  ratingComment,
  ratingPending,
  ratingScore,
  onChangeComment,
  onChangeScore,
  onConfirmCash,
  onDone,
  onSubmitRating,
}: {
  payment: Payment | null;
  paymentLoading: boolean;
  paymentPending: boolean;
  rating: Rating | null;
  ratingComment: string;
  ratingPending: boolean;
  ratingScore: number;
  onChangeComment(value: string): void;
  onChangeScore(value: number): void;
  onConfirmCash(): void;
  onDone(): void;
  onSubmitRating(): void;
}) {
  return (
    <View style={styles.completionSection}>
      <View style={styles.paymentCard}>
        <RydoIcon name="card" color={colors.blue} size={22} />
        <View style={styles.paymentCopy}>
          <Text selectable style={styles.paymentTitle}>
            {paymentLoading
              ? 'Checking payment…'
              : payment?.status === 'Paid'
                ? 'Payment confirmed'
                : payment?.method === 'Cash'
                  ? 'Confirm cash received'
                  : 'Waiting for passenger payment'}
          </Text>
          <Text selectable style={styles.paymentMessage}>
            {payment ? `${formatMoney(payment.amount, payment.currency)} · ${formatStatus(payment.status)}` : 'The passenger is preparing payment.'}
          </Text>
        </View>
      </View>
      {payment?.method === 'Cash' && payment.status === 'AwaitingPayment' ? (
        <RydoButton label="Cash received" loading={paymentPending} onPress={onConfirmCash} />
      ) : null}

      {rating ? (
        <View style={styles.ratingComplete}>
          <RydoIcon name="check" color={colors.success} size={18} />
          <Text selectable style={styles.ratingCompleteLabel}>Passenger rating submitted.</Text>
        </View>
      ) : (
        <View style={styles.ratingSection}>
          <Text selectable style={styles.ratingTitle}>Rate the passenger</Text>
          <View accessibilityRole="radiogroup" style={styles.stars}>
            {[1, 2, 3, 4, 5].map((score) => (
              <Pressable
                key={score}
                accessibilityRole="radio"
                accessibilityState={{ checked: ratingScore === score }}
                onPress={() => onChangeScore(score)}
                style={styles.starButton}
              >
                <RydoIcon name="star" color={score <= ratingScore ? colors.amber : colors.border} size={29} />
              </Pressable>
            ))}
          </View>
          <TextInput
            maxLength={500}
            multiline
            onChangeText={onChangeComment}
            placeholder="Add a comment (optional)"
            placeholderTextColor={colors.textMuted}
            style={styles.ratingInput}
            value={ratingComment}
          />
          <RydoButton label="Submit rating" loading={ratingPending} onPress={onSubmitRating} />
        </View>
      )}
      {rating ? <RydoButton label="Finish" onPress={onDone} /> : null}
    </View>
  );
}

async function openNavigation(trip: Trip) {
  const destination = trip.status === 'InProgress'
    ? { latitude: trip.destinationLatitude, longitude: trip.destinationLongitude }
    : { latitude: trip.pickupLatitude, longitude: trip.pickupLongitude };
  const fallbackUrl = `https://www.google.com/maps/dir/?api=1&destination=${destination.latitude},${destination.longitude}&travelmode=driving&dir_action=navigate`;
  if (process.env.EXPO_OS === 'android') {
    const nativeUrl = `google.navigation:q=${destination.latitude},${destination.longitude}&mode=d`;
    try {
      await Linking.openURL(nativeUrl);
      return;
    } catch {
      // Use the universal Maps URL when the Google Maps app is unavailable.
    }
  }
  await Linking.openURL(fallbackUrl);
}

function toCoordinate(position: Location.LocationObject): GeoCoordinate {
  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
  };
}

function roundedCoordinate(coordinate: GeoCoordinate | null) {
  if (!coordinate) return null;
  return {
    latitude: Number(coordinate.latitude.toFixed(4)),
    longitude: Number(coordinate.longitude.toFixed(4)),
  };
}

function formatDistance(distanceMeters: number) {
  return distanceMeters < 1_000
    ? `${distanceMeters} m`
    : `${(distanceMeters / 1_000).toFixed(1)} km`;
}

function formatDuration(durationSeconds: number) {
  const minutes = Math.max(1, Math.round(durationSeconds / 60));
  return `${minutes} min`;
}

function driverStateTitle(status: Trip['status']) {
  return {
    Requested: 'Ride request',
    Accepted: 'Drive to pickup',
    DriverArrived: 'Passenger pickup',
    InProgress: 'Ride in progress',
    Completed: 'Ride completed',
    Cancelled: 'Ride cancelled',
  }[status];
}

function driverStateMessage(status: Trip['status']) {
  return {
    Requested: 'Review this request before responding.',
    Accepted: 'Navigate to the passenger and mark your arrival.',
    DriverArrived: 'Start only after the passenger is safely inside.',
    InProgress: 'Follow the route and complete at the destination.',
    Completed: 'Confirm payment and rate the passenger.',
    Cancelled: 'This ride is no longer active.',
  }[status];
}

function formatStatus(status: string) {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function formatFare(trip: Trip) {
  return formatMoney(trip.finalFareAmount ?? trip.estimatedFareAmount ?? 0, trip.fareCurrency ?? 'ZAR');
}

function formatMoney(amount: number, currency: string) {
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency, minimumFractionDigits: 2 }).format(amount);
}

const styles = StyleSheet.create({
  content: { flexGrow: 1, gap: spacing.xl, paddingHorizontal: spacing.xl, backgroundColor: driverTheme.colors.background },
  offerCard: { gap: spacing.lg, padding: spacing.lg, borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  tripFlow: { gap: spacing.lg },
  offerHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  tripHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  offerIcon: { width: 58, height: 58, alignItems: 'center', justifyContent: 'center', borderRadius: 20, backgroundColor: colors.blue },
  stateIcon: { width: 56, height: 56, alignItems: 'center', justifyContent: 'center', borderRadius: 20, backgroundColor: colors.blue },
  stateIconComplete: { backgroundColor: colors.success },
  headingCopy: { minWidth: 0, flex: 1, gap: 2 },
  eyebrow: { color: colors.blue, fontSize: 11, fontWeight: '900', letterSpacing: 0.8 },
  title: { color: colors.navy, fontSize: 25, lineHeight: 31, fontWeight: '900' },
  subtitle: { color: colors.textMuted, fontSize: 13, lineHeight: 18 },
  routeCard: { gap: 2, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  navigationCard: { overflow: 'hidden', borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  navigationMap: { height: 250, overflow: 'hidden', backgroundColor: colors.blueMuted },
  navigationMeta: { minHeight: 68, flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  navigationTargetIcon: { width: 38, height: 38, alignItems: 'center', justifyContent: 'center', borderRadius: 14, backgroundColor: colors.blueMuted },
  navigationCopy: { minWidth: 0, flex: 1, gap: 2 },
  navigationTarget: { color: colors.navy, fontSize: 14, fontWeight: '900' },
  navigationDetail: { color: colors.textMuted, fontSize: 12, fontWeight: '600', fontVariant: ['tabular-nums'] },
  routePoint: { minHeight: 58, flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md },
  routeDot: { width: 12, height: 12, marginTop: 19, borderRadius: 6 },
  routeLine: { width: 2, height: 22, marginLeft: 5, marginTop: -25, marginBottom: -1, backgroundColor: driverTheme.colors.softBorder },
  routeCopy: { minWidth: 0, flex: 1, gap: 3 },
  routeLabel: { color: colors.textMuted, fontSize: 11, fontWeight: '800' },
  address: { color: colors.navy, fontSize: 15, lineHeight: 20, fontWeight: '700' },
  offerFacts: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md },
  offerFact: { color: colors.navy, fontSize: 14, fontWeight: '800' },
  offerFare: { color: colors.blue, fontSize: 20, fontWeight: '900', fontVariant: ['tabular-nums'] },
  expiryText: { color: colors.textMuted, fontSize: 12, textAlign: 'center', fontVariant: ['tabular-nums'] },
  actionRow: { flexDirection: 'row', gap: spacing.md },
  actionHalf: { flex: 1 },
  fareRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md },
  tripReference: { color: colors.textMuted, fontSize: 11 },
  fare: { color: colors.blue, fontSize: 22, fontWeight: '900', fontVariant: ['tabular-nums'] },
  error: { color: colors.danger, fontSize: 13, lineHeight: 18 },
  cancelButton: { minHeight: 50, alignItems: 'center', justifyContent: 'center', borderRadius: 17, backgroundColor: '#FFF0F2' },
  cancelLabel: { color: colors.danger, fontSize: 15, fontWeight: '900' },
  completionSection: { gap: spacing.md },
  paymentCard: { minHeight: 72, flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.md, borderRadius: driverTheme.radii.banner, backgroundColor: colors.blueMuted },
  paymentCopy: { minWidth: 0, flex: 1, gap: 2 },
  paymentTitle: { color: colors.navy, fontSize: 15, fontWeight: '900' },
  paymentMessage: { color: colors.textMuted, fontSize: 12 },
  ratingSection: { gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  ratingTitle: { color: colors.navy, fontSize: 18, fontWeight: '900', textAlign: 'center' },
  stars: { flexDirection: 'row', justifyContent: 'center', gap: 5 },
  starButton: { padding: 3 },
  ratingInput: { minHeight: 78, borderWidth: 1, borderColor: colors.border, borderRadius: 16, paddingHorizontal: 13, paddingVertical: 11, color: colors.navy, backgroundColor: driverTheme.colors.softControl, textAlignVertical: 'top' },
  ratingComplete: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, padding: spacing.md, borderRadius: 16, backgroundColor: colors.successMuted },
  ratingCompleteLabel: { color: colors.success, fontSize: 13, fontWeight: '800' },
});
