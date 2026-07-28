import { TouchableOpacity } from '@gorhom/bottom-sheet';
import {
  decodeGooglePolyline,
  isApiError,
  type GeoCoordinate,
  type Place,
  type PlacePrediction,
  type RoutePreview,
  type RequestTripRequest,
  type Trip,
  type TripMatchingResult,
} from '@rydo/mobile-api-client';
import {
  MapControl,
  RideCard,
  RydoBottomSheet,
  RydoBottomSheetTextInput,
  RydoButton,
  colors,
  spacing,
  typography,
} from '@rydo/mobile-design-system';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as Location from 'expo-location';
import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiClient } from '@/api';

import { RideMap, type RideMapHandle } from './ride-map';

type Field = 'pickup' | 'destination';

interface RidePlannerScreenProps {
  greetingName?: string;
  profileReady: boolean;
  activeTrip: Trip | null;
}

export function RidePlannerScreen({ greetingName, profileReady, activeTrip }: RidePlannerScreenProps) {
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const mapRef = useRef<RideMapHandle>(null);
  const [activeField, setActiveField] = useState<Field>('destination');
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [pickup, setPickup] = useState<Place | null>(null);
  const [destination, setDestination] = useState<Place | null>(null);
  const [message, setMessage] = useState('Search or choose a point on the map.');
  const sessionTokenRef = useRef(createSessionToken());

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedQuery(query.trim()), 350);
    return () => clearTimeout(timeout);
  }, [query]);

  const autocomplete = useQuery({
    queryKey: ['maps', 'autocomplete', debouncedQuery, pickup?.location],
    enabled: debouncedQuery.length >= 3,
    queryFn: ({ signal }) => {
      const params = new URLSearchParams({
        query: debouncedQuery,
        sessionToken: sessionTokenRef.current,
      });
      if (pickup) {
        params.set('latitude', String(pickup.location.latitude));
        params.set('longitude', String(pickup.location.longitude));
      }
      return apiClient.get<PlacePrediction[]>(`/api/v1/maps/places/autocomplete?${params}`, { signal });
    },
  });

  const route = useQuery({
    queryKey: ['maps', 'route', pickup?.location, destination?.location],
    enabled: Boolean(pickup && destination),
    queryFn: ({ signal }) =>
      apiClient.post<RoutePreview, { origin: GeoCoordinate; destination: GeoCoordinate }>(
        '/api/v1/maps/routes',
        { origin: pickup!.location, destination: destination!.location },
        { signal },
      ),
  });
  const encodedPolyline = route.data?.encodedPolyline;
  const routeCoordinates = encodedPolyline ? decodeGooglePolyline(encodedPolyline) : [];
  const requestRide = useMutation({
    mutationFn: async (request: RequestTripRequest) => {
      const trip = await apiClient.post<Trip, RequestTripRequest>('/api/v1/trips', request, { retry: 'never' });
      await apiClient.request<TripMatchingResult>(`/api/v1/trips/${trip.id}/matching`, {
        method: 'POST',
        retry: 'never',
      });
      return trip;
    },
    onSuccess: (trip) => {
      setMessage('Finding the nearest available driver…');
      queryClient.setQueryData<Trip[]>(['trips'], (current = []) => [trip, ...current.filter((item) => item.id !== trip.id)]);
    },
  });

  useEffect(() => {
    if (encodedPolyline) {
      mapRef.current?.fitRoute(decodeGooglePolyline(encodedPolyline));
    }
  }, [encodedPolyline]);

  async function requestCurrentLocation() {
    const permission = await Location.requestForegroundPermissionsAsync();
    if (!permission.granted) {
      setMessage('Location permission is needed to use your position as pickup.');
      return;
    }

    const current = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
    await selectCoordinate({ latitude: current.coords.latitude, longitude: current.coords.longitude }, 'pickup');
  }

  async function selectCoordinate(location: GeoCoordinate, field = activeField) {
    setMessage('Looking up this address…');
    try {
      const place = await apiClient.get<Place>(
        `/api/v1/maps/geocode/reverse?latitude=${location.latitude}&longitude=${location.longitude}`,
      );
      selectPlace(place, field);
    } catch (error) {
      selectPlace({ placeId: '', name: 'Pinned location', address: 'Pinned location', location }, field);
      setMessage(mapErrorMessage(error, 'Location pinned, but its address could not be resolved.'));
    }
  }

  async function selectPrediction(prediction: PlacePrediction) {
    setMessage('Loading this place…');
    try {
      const place = await apiClient.get<Place>(
        `/api/v1/maps/places/${encodeURIComponent(prediction.placeId)}?sessionToken=${encodeURIComponent(sessionTokenRef.current)}`,
      );
      selectPlace(place, activeField);
      sessionTokenRef.current = createSessionToken();
    } catch (error) {
      setMessage(mapErrorMessage(error, 'This place could not be loaded.'));
    }
  }

  function selectPlace(place: Place, field: Field) {
    if (field === 'pickup') setPickup(place);
    else setDestination(place);
    setQuery('');
    setMessage(field === 'pickup' ? 'Pickup selected.' : 'Destination selected.');
  }

  function submitRideRequest() {
    if (!pickup || !destination || !profileReady) return;
    requestRide.reset();
    requestRide.mutate({
      pickupAddress: pickup.address,
      pickupLatitude: pickup.location.latitude,
      pickupLongitude: pickup.location.longitude,
      destinationAddress: destination.address,
      destinationLatitude: destination.location.latitude,
      destinationLongitude: destination.location.longitude,
    });
  }

  const rideError = requestRide.error
    ? isApiError(requestRide.error)
      ? requestRide.error.problem?.detail ?? requestRide.error.message
      : 'Your ride could not be requested.'
    : undefined;

  return (
    <View style={styles.container}>
      <RideMap
        ref={mapRef}
        pickup={pickup?.location ?? null}
        destination={destination?.location ?? null}
        route={routeCoordinates}
        onMapPress={(coordinate: GeoCoordinate) => void selectCoordinate(coordinate)}
      />
      <View style={[styles.locationButton, { top: insets.top + spacing.md }]}>
        <MapControl icon="location" label="Use my current location" onPress={() => void requestCurrentLocation()} />
      </View>
      {greetingName ? (
        <View style={[styles.greeting, { top: insets.top + spacing.md }]}>
          <Text selectable style={styles.greetingText}>Hello, {greetingName}</Text>
        </View>
      ) : null}
      <RydoBottomSheet snapPoints={['48%', '78%']} bottomInset={insets.bottom + 86}>
        <Text selectable style={styles.title}>Plan your ride</Text>
        {activeTrip ? (
          <RideCard
            title={formatTripStatus(activeTrip.status)}
            pickup={activeTrip.pickupAddress}
            destination={activeTrip.destinationAddress}
            metadata={tripStatusMessage(activeTrip.status)}
            selected
          />
        ) : (
          <>
        <LocationInput
          label="Pickup"
          value={pickup?.address ?? ''}
          active={activeField === 'pickup'}
          onPress={() => setActiveField('pickup')}
        />
        <LocationInput
          label="Destination"
          value={destination?.address ?? ''}
          active={activeField === 'destination'}
          onPress={() => setActiveField('destination')}
        />
        <RydoBottomSheetTextInput
          value={query}
          onChangeText={setQuery}
          placeholder={`Search ${activeField}`}
          autoCapitalize="words"
          returnKeyType="search"
        />
        {autocomplete.isFetching ? <ActivityIndicator color={colors.blue} /> : null}
        {autocomplete.error ? (
          <Text selectable style={styles.error}>
            {mapErrorMessage(autocomplete.error, 'Places search is temporarily unavailable.')}
          </Text>
        ) : null}
        {autocomplete.data?.slice(0, 4).map((prediction) => (
          <TouchableOpacity
            key={prediction.placeId}
            style={styles.suggestion}
            onPress={() => void selectPrediction(prediction)}
          >
            <Text selectable style={styles.suggestionTitle}>{prediction.mainText}</Text>
            <Text selectable numberOfLines={1} style={styles.suggestionDetail}>{prediction.secondaryText}</Text>
          </TouchableOpacity>
        ))}
        {route.isFetching ? <Text selectable style={styles.status}>Calculating the fastest route…</Text> : null}
        {route.error ? (
          <Text selectable style={styles.error}>
            {mapErrorMessage(route.error, 'A route could not be calculated for these locations.')}
          </Text>
        ) : null}
        {route.data ? (
          <View style={{ gap: spacing.md }}>
            <Text selectable style={styles.routeSummary}>
              {(route.data.distanceMeters / 1000).toFixed(1)} km · {Math.max(1, Math.ceil(route.data.durationSeconds / 60))} min
            </Text>
            <View style={styles.paymentRow}>
              <View style={styles.paymentSelected}>
                <Text selectable style={styles.paymentSelectedText}>Cash</Text>
              </View>
              <View style={styles.paymentDisabled}>
                <Text selectable style={styles.paymentDisabledText}>Card · coming soon</Text>
              </View>
            </View>
            {rideError ? <Text selectable style={styles.error}>{rideError}</Text> : null}
            <RydoButton
              label={profileReady ? 'Request ride' : 'Complete profile to ride'}
              loading={requestRide.isPending}
              disabled={!profileReady}
              onPress={submitRideRequest}
            />
          </View>
        ) : (
          <Text selectable style={styles.status}>{message}</Text>
        )}
          </>
        )}
      </RydoBottomSheet>
    </View>
  );
}

function LocationInput({ label, value, active, onPress }: { label: string; value: string; active: boolean; onPress(): void }) {
  return (
    <TouchableOpacity style={[styles.field, active && styles.fieldActive]} onPress={onPress}>
      <Text selectable style={styles.fieldLabel}>{label}</Text>
      <Text selectable numberOfLines={1} style={styles.fieldValue}>{value || `Choose ${label.toLowerCase()}`}</Text>
    </TouchableOpacity>
  );
}

function createSessionToken() {
  return `${Date.now()}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`;
}

function mapErrorMessage(error: unknown, fallback: string) {
  if (!isApiError(error)) return fallback;

  if (error.status === 503) {
    return 'Google Maps is not configured or is temporarily unavailable.';
  }

  return error.problem?.detail ?? error.message;
}

function formatTripStatus(status: Trip['status']) {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function tripStatusMessage(status: Trip['status']) {
  const messages: Record<Trip['status'], string> = {
    Requested: 'Finding the nearest available driver…',
    Accepted: 'Your driver is heading to the pickup point.',
    DriverArrived: 'Your driver has arrived.',
    InProgress: 'Your ride is in progress.',
    Completed: 'Ride completed.',
    Cancelled: 'Ride cancelled.',
  };
  return messages[status];
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  locationButton: { position: 'absolute', right: spacing.lg },
  greeting: { position: 'absolute', left: spacing.md, borderRadius: 999, paddingHorizontal: 16, paddingVertical: 11, backgroundColor: colors.navyGlass },
  greetingText: { color: colors.white, fontWeight: '800' },
  title: { color: colors.navy, fontSize: typography.size.title, fontWeight: typography.weight.bold },
  field: { borderWidth: 1, borderColor: colors.border, borderRadius: 14, paddingHorizontal: 14, paddingVertical: 9 },
  fieldActive: { borderColor: colors.blue, backgroundColor: colors.blueMuted },
  fieldLabel: { color: colors.blue, fontSize: 11, fontWeight: '800', textTransform: 'uppercase' },
  fieldValue: { color: colors.navy, fontSize: 14, marginTop: 2 },
  suggestion: { borderTopWidth: 1, borderTopColor: colors.border, paddingVertical: 8 },
  suggestionTitle: { color: colors.navy, fontWeight: '700' },
  suggestionDetail: { color: colors.textMuted, fontSize: 12, marginTop: 2 },
  status: { color: colors.textMuted, fontSize: 13 },
  routeSummary: { borderRadius: 14, padding: 12, textAlign: 'center', backgroundColor: colors.blue, color: colors.white, fontSize: 16, fontWeight: '800' },
  paymentRow: { flexDirection: 'row', gap: 8 },
  paymentSelected: { borderRadius: 999, backgroundColor: colors.navy, paddingHorizontal: 14, paddingVertical: 9 },
  paymentSelectedText: { color: colors.white, fontWeight: '800' },
  paymentDisabled: { borderRadius: 999, backgroundColor: colors.surface, paddingHorizontal: 14, paddingVertical: 9 },
  paymentDisabledText: { color: colors.textMuted, fontWeight: '700' },
  error: { color: colors.danger, fontSize: 13, lineHeight: 18 },
});
