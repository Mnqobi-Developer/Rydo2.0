import { TouchableOpacity } from '@gorhom/bottom-sheet';
import {
  decodeGooglePolyline,
  isApiError,
  type CreateFareQuoteRequest,
  type FareOption,
  type FareQuote,
  type GeoCoordinate,
  type Place,
  type PlacePrediction,
  type RequestTripRequest,
  type RideCategory,
  type Trip,
  type TripMatchingResult,
} from '@rydo/mobile-api-client';
import {
  MapControl,
  RideCard,
  RydoBottomSheet,
  RydoBottomSheetTextInput,
  RydoButton,
  RydoIcon,
  colors,
  spacing,
} from '@rydo/mobile-design-system';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Image } from 'expo-image';
import * as Location from 'expo-location';
import { router } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  AppState,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { KeyboardAwareScrollView } from 'react-native-keyboard-controller';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiClient } from '@/api';

import { RideMap, type RideMapHandle } from './ride-map';

type Field = 'pickup' | 'destination';
type LocationStatus = 'locating' | 'ready' | 'denied' | 'error';
type BookingStep = 'browse' | 'routeReady' | 'rideOptions' | 'confirmPickup' | 'matching';

interface RidePlannerScreenProps {
  greetingName?: string;
  profileReady: boolean;
  activeTrip: Trip | null;
  recentTrips: Trip[];
}

export function RidePlannerScreen({
  greetingName,
  profileReady,
  activeTrip,
  recentTrips,
}: RidePlannerScreenProps) {
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const mapRef = useRef<RideMapHandle>(null);
  const [activeField, setActiveField] = useState<Field>('destination');
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [pickup, setPickup] = useState<Place | null>(null);
  const [destination, setDestination] = useState<Place | null>(null);
  const [currentLocation, setCurrentLocation] = useState<GeoCoordinate | null>(null);
  const [locationStatus, setLocationStatus] = useState<LocationStatus>('locating');
  const [sheetIndex, setSheetIndex] = useState(0);
  const [searchAutoFocus, setSearchAutoFocus] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<RideCategory>('Solo');
  const [bookingStep, setBookingStep] = useState<BookingStep>('browse');
  const [paymentOpen, setPaymentOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [message, setMessage] = useState('Finding your current location…');
  const sessionTokenRef = useRef(createSessionToken());
  const coordinateLookupRef = useRef<Record<Field, number>>({ pickup: 0, destination: 0 });
  const locationRequestRef = useRef<Promise<void> | null>(null);
  const appStateRef = useRef(AppState.currentState);

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedQuery(query.trim()), 350);
    return () => clearTimeout(timeout);
  }, [query]);

  const autocomplete = useQuery({
    queryKey: ['maps', 'autocomplete', debouncedQuery, pickup?.location, currentLocation],
    enabled: debouncedQuery.length >= 3,
    queryFn: ({ signal }) => {
      const params = new URLSearchParams({
        query: debouncedQuery,
        sessionToken: sessionTokenRef.current,
      });
      const locationBias = pickup?.location ?? currentLocation;
      if (locationBias) {
        params.set('latitude', String(locationBias.latitude));
        params.set('longitude', String(locationBias.longitude));
      }
      return apiClient.get<PlacePrediction[]>(`/api/v1/maps/places/autocomplete?${params}`, { signal });
    },
  });

  const fareQuote = useQuery({
    queryKey: ['pricing', 'quote', pickup?.location, destination?.location],
    enabled: Boolean(pickup && destination),
    queryFn: ({ signal }) =>
      apiClient.post<FareQuote, CreateFareQuoteRequest>(
        '/api/v1/pricing/quotes',
        { pickup: pickup!.location, destination: destination!.location },
        { signal },
      ),
    staleTime: 4 * 60 * 1000,
    refetchInterval: 4 * 60 * 1000,
  });
  const encodedPolyline = fareQuote.data?.encodedPolyline;
  const routeCoordinates = encodedPolyline ? decodeGooglePolyline(encodedPolyline) : [];
  const requestRide = useMutation({
    mutationFn: async (request: RequestTripRequest) => {
      const trip = await apiClient.post<Trip, RequestTripRequest>('/api/v1/trips', request, { retry: 'never' });
      queryClient.setQueryData<Trip[]>(['trips'], (current = []) => [trip, ...current.filter((item) => item.id !== trip.id)]);
      try {
        await apiClient.request<TripMatchingResult>(`/api/v1/trips/${trip.id}/matching`, {
          method: 'POST',
          retry: 'never',
        });
      } catch {
        // The trip exists even when no driver can be offered immediately.
      }
      return trip;
    },
    onSuccess: (trip) => {
      setMessage('Finding the nearest available driver…');
      setBookingStep('matching');
      queryClient.setQueryData<Trip[]>(['trips'], (current = []) => [trip, ...current.filter((item) => item.id !== trip.id)]);
    },
  });
  const cancelRide = useMutation({
    mutationFn: ({ tripId, reason }: { tripId: string; reason: string }) => apiClient.post<Trip, { reason: string }>(
      `/api/v1/trips/${tripId}/cancel`,
      { reason },
      { retry: 'never' },
    ),
    onSuccess: (trip) => {
      queryClient.setQueryData<Trip[]>(['trips'], (current = []) =>
        current.map((item) => item.id === trip.id ? trip : item));
      void queryClient.invalidateQueries({ queryKey: ['trips'] });
      resetPlanning();
    },
  });

  useEffect(() => {
    if (encodedPolyline) {
      mapRef.current?.fitRoute(decodeGooglePolyline(encodedPolyline));
    }
  }, [encodedPolyline]);

  const selectPlace = useCallback((place: Place, field: Field) => {
    if (field === 'pickup') {
      setPickup(place);
      setActiveField('destination');
    } else {
      setDestination(place);
      setBookingStep('routeReady');
      setSearchAutoFocus(false);
      setSheetIndex(0);
    }
    setQuery('');
    setMessage(
      field === 'pickup'
        ? 'Pickup selected. Now choose your destination.'
        : 'Destination selected. Calculating your route…',
    );
  }, []);

  const selectCoordinate = useCallback(async (location: GeoCoordinate, field: Field) => {
    const lookupId = ++coordinateLookupRef.current[field];
    setMessage(`Looking up the ${field} address…`);
    try {
      const place = await apiClient.get<Place>(
        `/api/v1/maps/geocode/reverse?latitude=${location.latitude}&longitude=${location.longitude}`,
      );
      if (lookupId !== coordinateLookupRef.current[field]) return;
      selectPlace(place, field);
    } catch (error) {
      if (lookupId !== coordinateLookupRef.current[field]) return;
      selectPlace({ placeId: '', name: 'Pinned location', address: 'Pinned location', location }, field);
      setMessage(mapErrorMessage(error, 'Location pinned, but its address could not be resolved.'));
    }
  }, [selectPlace]);

  const requestCurrentLocation = useCallback((requestPermission: boolean, useAsPickup: boolean) => {
    if (locationRequestRef.current) return locationRequestRef.current;

    const request = (async () => {
      setLocationStatus('locating');
      setMessage('Finding your current location…');

      try {
        let permission = await Location.getForegroundPermissionsAsync();
        if (!permission.granted && requestPermission && permission.canAskAgain) {
          permission = await Location.requestForegroundPermissionsAsync();
        }

        if (!permission.granted) {
          setLocationStatus('denied');
          setMessage('Enable location permission to use your position, or tap the map to choose a pickup.');
          return;
        }

        const current = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High });
        const coordinate = {
          latitude: current.coords.latitude,
          longitude: current.coords.longitude,
        };
        setCurrentLocation(coordinate);
        setLocationStatus('ready');
        mapRef.current?.focusCoordinate(coordinate);
        if (useAsPickup) {
          await selectCoordinate(coordinate, 'pickup');
        } else {
          setMessage('Current location updated.');
        }
      } catch {
        setLocationStatus('error');
        setMessage('Your current location could not be determined. Check location services or choose a pickup on the map.');
      }
    })().finally(() => {
      locationRequestRef.current = null;
    });

    locationRequestRef.current = request;
    return request;
  }, [selectCoordinate]);

  useEffect(() => {
    void requestCurrentLocation(true, true);

    const subscription = AppState.addEventListener('change', (nextState) => {
      const returningToForeground = appStateRef.current !== 'active' && nextState === 'active';
      appStateRef.current = nextState;
      if (returningToForeground) {
        void requestCurrentLocation(false, false);
      }
    });

    return () => subscription.remove();
  }, [requestCurrentLocation]);

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

  function submitRideRequest() {
    if (!pickup || !destination || !profileReady || !fareQuote.data) return;
    if (Date.parse(fareQuote.data.expiresAt) <= Date.now()) {
      setMessage('Refreshing your fare before requesting the ride.');
      void fareQuote.refetch();
      return;
    }
    requestRide.reset();
    requestRide.mutate({
      pickupAddress: pickup.address,
      pickupLatitude: pickup.location.latitude,
      pickupLongitude: pickup.location.longitude,
      destinationAddress: destination.address,
      destinationLatitude: destination.location.latitude,
      destinationLongitude: destination.location.longitude,
      fareQuoteId: fareQuote.data.id,
      rideCategory: selectedCategory,
    });
  }

  function activateField(field: Field) {
    setActiveField(field);
    setQuery('');
    setMessage(`Search or tap the map to choose your ${field}.`);
  }

  function focusPlannerField(field: Field) {
    setBookingStep('browse');
    activateField(field);
    setSearchAutoFocus(true);
    setSheetIndex(2);
  }

  function selectRecentDestination(trip: Trip) {
    selectPlace(
      {
        placeId: '',
        name: trip.destinationAddress,
        address: trip.destinationAddress,
        location: {
          latitude: trip.destinationLatitude,
          longitude: trip.destinationLongitude,
        },
      },
      'destination',
    );
    setSearchAutoFocus(false);
    setSheetIndex(1);
  }

  function handlePlanRide() {
    if (!pickup) {
      focusPlannerField('pickup');
      return;
    }

    if (!destination) {
      focusPlannerField('destination');
      return;
    }

    if (!fareQuote.data) {
      setMessage('Calculating your route before you continueâ€¦');
      return;
    }

    if (Date.parse(fareQuote.data.expiresAt) <= Date.now()) {
      setMessage('Refreshing your fare before you continue.');
      void fareQuote.refetch();
      return;
    }

    setBookingStep('confirmPickup');
    setSheetIndex(0);
    mapRef.current?.focusCoordinate(pickup.location);
  }

  function handleChooseRide() {
    if (!fareQuote.data) {
      setMessage('Calculating your route before you continue…');
      return;
    }

    setBookingStep('rideOptions');
    setSheetIndex(1);
    if (routeCoordinates.length > 1) mapRef.current?.fitRoute(routeCoordinates);
  }

  function resetPlanning() {
    setDestination(null);
    setSelectedCategory('Solo');
    setBookingStep('browse');
    setQuery('');
    setSearchAutoFocus(false);
    setPaymentOpen(false);
    setCancelOpen(false);
    setSheetIndex(0);
    setMessage('Choose a destination to plan your ride.');
    requestRide.reset();
    if (pickup) mapRef.current?.focusCoordinate(pickup.location);
  }

  function backToRideOptions() {
    setBookingStep('rideOptions');
    setSheetIndex(1);
    if (routeCoordinates.length > 1) mapRef.current?.fitRoute(routeCoordinates);
  }

  const rideError = requestRide.error
    ? isApiError(requestRide.error)
      ? requestRide.error.problem?.detail ?? requestRide.error.message
      : 'Your ride could not be requested.'
    : undefined;
  const uniqueRecentTrips = recentTrips.filter(
    (trip, index, trips) => trips.findIndex((candidate) => candidate.destinationAddress === trip.destinationAddress) === index,
  ).slice(0, 3);
  const selectedFare = fareQuote.data?.options.find((option) => option.category === selectedCategory);
  const focusedFlow = Boolean(activeTrip) || bookingStep !== 'browse';
  const routeOrigin = activeTrip?.pickupAddress ?? pickup?.address;
  const routeDestination = activeTrip?.destinationAddress ?? destination?.address;
  const plannerSnapPoints: `${number}%`[] = activeTrip || bookingStep === 'matching'
    ? ['44%', '70%', '96%']
    : bookingStep === 'routeReady'
      ? ['62%', '78%', '96%']
      : bookingStep === 'rideOptions'
        ? ['60%', '78%', '96%']
        : bookingStep === 'confirmPickup'
          ? ['48%', '70%', '96%']
          : ['52%', '74%', '96%'];

  return (
    <View style={styles.container}>
      <RideMap
        ref={mapRef}
        currentLocation={currentLocation}
        pickup={pickup?.location ?? null}
        destination={destination?.location ?? null}
        route={routeCoordinates}
        onMapPress={(coordinate: GeoCoordinate) =>
          void selectCoordinate(coordinate, bookingStep === 'confirmPickup' ? 'pickup' : activeField)}
      />
      {locationStatus === 'locating' ? (
        <View pointerEvents="none" style={styles.locatingOverlay}>
          <ActivityIndicator color={colors.blue} size="large" />
          <Text selectable style={styles.locatingText}>Finding your current location…</Text>
        </View>
      ) : null}
      {!focusedFlow ? <View style={[styles.locationButton, { top: insets.top + spacing.md }]}>
        <MapControl
          icon="location"
          label="Use my current location"
          selected={locationStatus === 'ready'}
          disabled={locationStatus === 'locating'}
          onPress={() => void requestCurrentLocation(true, true)}
        />
      </View> : null}
      {!focusedFlow && greetingName ? (
        <View style={[styles.greeting, { top: insets.top + spacing.md }]}>
          <Text selectable style={styles.greetingText}>Hello, {greetingName}</Text>
        </View>
      ) : null}
      {focusedFlow && routeOrigin && routeDestination ? (
        <View style={[styles.routePill, { top: insets.top + spacing.sm }]}>
          {!activeTrip ? (
            <TouchableOpacity accessibilityLabel="Close ride planning" style={styles.routePillClose} onPress={resetPlanning}>
              <Text style={styles.routePillCloseText}>×</Text>
            </TouchableOpacity>
          ) : (
            <View style={styles.routePillStatus}><View style={styles.routePillStatusCenter} /></View>
          )}
          <Text selectable numberOfLines={1} style={styles.routePillText}>
            {shortAddress(routeOrigin)} → {shortAddress(routeDestination)}
          </Text>
        </View>
      ) : null}
      <RydoBottomSheet
        index={sheetIndex}
        snapPoints={plannerSnapPoints}
        bottomInset={0}
        contentStyle={styles.sheetContent}
        scrollable
        onChange={setSheetIndex}
      >
        {activeTrip ? (
          <>
            <View style={styles.statusHeadingRow}>
              <View style={styles.statusPulse}><View style={styles.statusPulseCenter} /></View>
              <View style={styles.statusHeadingCopy}>
                <Text selectable style={styles.focusedTitle}>{tripPanelTitle(activeTrip.status)}</Text>
                <Text selectable style={styles.focusedSubtitle}>{tripStatusMessage(activeTrip.status)}</Text>
              </View>
            </View>
            <View style={styles.progressTrack}><View style={styles.progressValue} /></View>
            <RideCard
              title={`${categoryLabel(activeTrip.rideCategory ?? 'Solo')} · ${formatTripStatus(activeTrip.status)}`}
              pickup={activeTrip.pickupAddress}
              destination={activeTrip.destinationAddress}
              fare={activeTrip.estimatedFareAmount == null
                ? undefined
                : formatFare(activeTrip.estimatedFareAmount, activeTrip.fareCurrency ?? 'ZAR')}
              selected
            />
            {activeTrip.status === 'Requested' || activeTrip.status === 'Accepted' || activeTrip.status === 'DriverArrived' ? (
              <TouchableOpacity
                style={styles.cancelAction}
                onPress={() => {
                  cancelRide.reset();
                  setCancelOpen(true);
                }}
              >
                <Text selectable style={styles.cancelActionText}>Cancel ride</Text>
              </TouchableOpacity>
            ) : null}
          </>
        ) : bookingStep === 'matching' ? (
          <View style={styles.centeredStatus}>
            <ActivityIndicator color={colors.blue} size="large" />
            <Text selectable style={styles.focusedTitle}>Looking for a driver…</Text>
            <Text selectable style={styles.focusedSubtitle}>We&apos;re contacting nearby eligible drivers.</Text>
          </View>
        ) : (bookingStep === 'routeReady' || bookingStep === 'rideOptions') && !fareQuote.data ? (
          <View style={styles.centeredStatus}>
            {fareQuote.isFetching ? <ActivityIndicator color={colors.blue} size="large" /> : null}
            <Text selectable style={styles.focusedTitle}>Preparing your ride options…</Text>
            <Text selectable style={fareQuote.error ? styles.error : styles.focusedSubtitle}>
              {fareQuote.error
                ? mapErrorMessage(fareQuote.error, 'A fare could not be calculated for these locations.')
                : 'Calculating the route, travel time, and current fares.'}
            </Text>
          </View>
        ) : bookingStep === 'routeReady' && pickup && destination && selectedFare && fareQuote.data ? (
          <>
            <View style={styles.routeReadyHeader}>
              <View style={styles.routeReadyHeadingCopy}>
                <Text selectable style={styles.focusedTitle}>Route ready</Text>
                <Text selectable style={styles.focusedSubtitle}>Review your trip details before choosing a ride.</Text>
              </View>
              <TouchableOpacity accessibilityLabel="Edit route" style={styles.routeReadyEdit} onPress={() => focusPlannerField('destination')}>
                <RydoIcon name="chevron-right" color={colors.navy} size={18} style={{ transform: [{ rotate: '90deg' }] }} />
              </TouchableOpacity>
            </View>
            <RouteLocationCard
              kind="pickup"
              address={pickup.address}
              onPress={() => focusPlannerField('pickup')}
            />
            <RouteLocationCard
              kind="destination"
              address={destination.address}
              onPress={() => focusPlannerField('destination')}
            />
            <View style={styles.routeMetrics}>
              <View style={styles.routeDuration}>
                <RydoIcon name="clock" color={colors.textMuted} size={20} />
                <Text selectable style={styles.routeDurationText}>
                  {(fareQuote.data.distanceMeters / 1000).toFixed(1)} km · {Math.max(1, Math.ceil(fareQuote.data.durationSeconds / 60))} min
                </Text>
              </View>
              <Text selectable style={styles.routeFare}>{formatFare(selectedFare.total, fareQuote.data.currency)}</Text>
            </View>
            <TouchableOpacity accessibilityRole="button" style={styles.chooseRideButton} onPress={handleChooseRide}>
              <RideAssetIcon color={colors.white} size={22} />
              <Text style={styles.chooseRideButtonText}>Choose ride</Text>
              <Text style={styles.chooseRideArrow}>→</Text>
            </TouchableOpacity>
          </>
        ) : bookingStep === 'confirmPickup' && pickup && selectedFare && fareQuote.data ? (
          <>
            <Text selectable style={styles.focusedTitle}>Confirm pickup spot</Text>
            <Text selectable style={styles.focusedSubtitle}>Tap the map to adjust where your driver should meet you.</Text>
            <View style={styles.compactTripSummary}>
              <Text selectable style={styles.compactTripCategory}>{categoryLabel(selectedCategory)}</Text>
              <Text selectable style={styles.compactTripFare}>{formatFare(selectedFare.total, fareQuote.data.currency)}</Text>
            </View>
            <View style={styles.pickupAddressCard}>
              <RydoIcon name="location" color={colors.blue} size={20} />
              <Text selectable numberOfLines={2} style={styles.pickupAddressText}>{pickup.address}</Text>
            </View>
            {fareQuote.isFetching ? <Text selectable style={styles.status}>Refreshing fare for this pickup…</Text> : null}
            {rideError ? <Text selectable style={styles.error}>{rideError}</Text> : null}
            <View style={styles.focusedActions}>
              <TouchableOpacity style={styles.secondaryButton} onPress={backToRideOptions}>
                <Text style={styles.secondaryButtonText}>Back</Text>
              </TouchableOpacity>
              <View style={styles.primaryActionWrap}>
                <RydoButton
                  label="Confirm pickup"
                  loading={requestRide.isPending}
                  disabled={fareQuote.isFetching}
                  onPress={submitRideRequest}
                />
              </View>
            </View>
          </>
        ) : bookingStep === 'rideOptions' && fareQuote.data ? (
          <>
            <View style={styles.focusedHeaderRow}>
              <View>
                <Text selectable style={styles.focusedTitle}>Choose your ride</Text>
                <Text selectable style={styles.focusedSubtitle}>
                  {(fareQuote.data.distanceMeters / 1000).toFixed(1)} km · {Math.max(1, Math.ceil(fareQuote.data.durationSeconds / 60))} min
                </Text>
              </View>
              <TouchableOpacity onPress={() => focusPlannerField('destination')}>
                <Text style={styles.editRouteText}>Edit route</Text>
              </TouchableOpacity>
            </View>
            {fareQuote.data.options.map((option) => (
              <FareOptionCard
                key={option.category}
                option={option}
                currency={fareQuote.data.currency}
                selected={option.category === selectedCategory}
                onPress={() => setSelectedCategory(option.category)}
              />
            ))}
            {fareQuote.data.demandMultiplier > 1 ? (
              <Text selectable style={styles.demandNotice}>
                High demand · {fareQuote.data.demandMultiplier.toFixed(2)}× is included in these prices.
              </Text>
            ) : null}
            <PaymentMethodRow onPress={() => setPaymentOpen(true)} />
            {rideError ? <Text selectable style={styles.error}>{rideError}</Text> : null}
            <RydoButton label="Continue" disabled={!profileReady} onPress={handlePlanRide} />
          </>
        ) : (
          <>
            <Text selectable style={styles.landingTitle}>Let&apos;s get you on your way.</Text>
            <View style={styles.rideOptions}>
              <RideOption icon="car" label="Ride now" detail="Let&apos;s get moving" selected onPress={() => focusPlannerField('destination')} />
              <RideOption
                icon="clock"
                label="Schedule"
                detail="Book ahead"
                onPress={() => {
                  setMessage('Scheduled rides are coming soon. You can plan a ride now.');
                  setSearchAutoFocus(false);
                  setSheetIndex(1);
                }}
              />
            </View>
            <LocationInput label="Pickup" kind="pickup" value={pickup?.address ?? ''} active={activeField === 'pickup'} onPress={() => focusPlannerField('pickup')} />
            <LocationInput label="Destination" kind="destination" value={destination?.address ?? ''} active={activeField === 'destination'} onPress={() => focusPlannerField('destination')} />
            <RydoBottomSheetTextInput
              value={query}
              onChangeText={setQuery}
              placeholder={`Search ${activeField}`}
              autoFocus={searchAutoFocus}
              onFocus={() => setSheetIndex(2)}
              onBlur={() => setSearchAutoFocus(false)}
              autoCapitalize="words"
              returnKeyType="search"
              style={styles.searchInput}
            />
            {autocomplete.isFetching ? <ActivityIndicator color={colors.blue} /> : null}
            {autocomplete.error ? <Text selectable style={styles.error}>{mapErrorMessage(autocomplete.error, 'Places search is temporarily unavailable.')}</Text> : null}
            {autocomplete.data?.slice(0, 4).map((prediction) => (
              <TouchableOpacity key={prediction.placeId} style={styles.suggestion} onPress={() => void selectPrediction(prediction)}>
                <Text selectable style={styles.suggestionTitle}>{prediction.mainText}</Text>
                <Text selectable numberOfLines={1} style={styles.suggestionDetail}>{prediction.secondaryText}</Text>
              </TouchableOpacity>
            ))}
            {fareQuote.isFetching ? <Text selectable style={styles.status}>Calculating your route and fare…</Text> : null}
            {fareQuote.error ? <Text selectable style={styles.error}>{mapErrorMessage(fareQuote.error, 'A fare could not be calculated for these locations.')}</Text> : null}
            {!query && !fareQuote.isFetching ? <Text selectable style={styles.status}>{message}</Text> : null}
            <View style={styles.sectionHeader}>
              <Text selectable style={styles.sectionTitle}>Recent destinations</Text>
              <TouchableOpacity onPress={() => router.push('/activity')}><Text selectable style={styles.sectionAction}>View trips</Text></TouchableOpacity>
            </View>
            {uniqueRecentTrips.length ? uniqueRecentTrips.map((trip) => (
              <RecentDestination key={trip.id} trip={trip} onPress={() => selectRecentDestination(trip)} />
            )) : (
              <View style={styles.emptyRecent}>
                <RydoIcon name="clock" color={colors.textMuted} size={22} />
                <View style={{ flex: 1, gap: 2 }}>
                  <Text selectable style={styles.emptyRecentTitle}>No recent destinations yet</Text>
                  <Text selectable style={styles.emptyRecentDetail}>Your completed rides will appear here.</Text>
                </View>
              </View>
            )}
          </>
        )}
      </RydoBottomSheet>
      <PaymentModal visible={paymentOpen} onClose={() => setPaymentOpen(false)} />
      {cancelOpen ? (
        <CancellationModal
          loading={cancelRide.isPending}
          error={cancelRide.error ? 'Your ride could not be cancelled. Please try again.' : undefined}
          onClose={() => setCancelOpen(false)}
          onConfirm={(reason) => activeTrip && cancelRide.mutate({ tripId: activeTrip.id, reason })}
        />
      ) : null}
    </View>
  );
}

function RouteLocationCard({
  kind,
  address,
  onPress,
}: {
  kind: 'pickup' | 'destination';
  address: string;
  onPress(): void;
}) {
  return (
    <TouchableOpacity accessibilityRole="button" style={styles.routeLocationCard} onPress={onPress}>
      <View style={styles.routeLocationIcon}>
        <RydoIcon name={kind === 'pickup' ? 'location' : 'map-pin'} color={colors.white} size={23} />
      </View>
      <View style={styles.routeLocationCopy}>
        <Text selectable style={styles.routeLocationLabel}>
          {kind === 'pickup' ? 'Pickup location' : 'Drop-off location'}
        </Text>
        <Text selectable numberOfLines={2} style={styles.routeLocationAddress}>{address}</Text>
      </View>
      <RydoIcon name="chevron-right" color={colors.textMuted} size={20} />
    </TouchableOpacity>
  );
}

function PaymentMethodRow({ onPress }: { onPress(): void }) {
  return (
    <TouchableOpacity accessibilityRole="button" style={styles.paymentMethodRow} onPress={onPress}>
      <View style={styles.cashIcon}><Text style={styles.cashIconText}>R</Text></View>
      <View style={styles.paymentMethodCopy}>
        <Text selectable style={styles.paymentMethodTitle}>Cash</Text>
        <Text selectable style={styles.paymentMethodDetail}>Pay your driver after the ride</Text>
      </View>
      <RydoIcon name="chevron-right" color={colors.textMuted} size={18} />
    </TouchableOpacity>
  );
}

function PaymentModal({ visible, onClose }: { visible: boolean; onClose(): void }) {
  return (
    <Modal visible={visible} transparent animationType="slide" statusBarTranslucent onRequestClose={onClose}>
      <View style={styles.modalBackdrop}>
        <View style={styles.paymentModalCard}>
          <View style={styles.modalHandle} />
          <View style={styles.modalHeader}>
            <Text selectable style={styles.modalTitle}>Payment</Text>
            <Pressable accessibilityLabel="Close payment methods" style={styles.modalClose} onPress={onClose}>
              <Text style={styles.modalCloseText}>×</Text>
            </Pressable>
          </View>
          <Text selectable style={styles.modalSectionLabel}>Payment methods</Text>
          <View style={styles.paymentChoiceSelected}>
            <View style={styles.cashIcon}><Text style={styles.cashIconText}>R</Text></View>
            <View style={styles.paymentMethodCopy}>
              <Text selectable style={styles.paymentMethodTitle}>Cash</Text>
              <Text selectable style={styles.paymentMethodDetail}>Selected for this ride</Text>
            </View>
            <View style={styles.radioSelected}><View style={styles.radioCenter} /></View>
          </View>
          <View style={styles.paymentChoiceDisabled}>
            <View style={styles.cardIcon}><Text style={styles.cardIconText}>••••</Text></View>
            <View style={styles.paymentMethodCopy}>
              <Text selectable style={styles.paymentDisabledTitle}>Card / PayFast</Text>
              <Text selectable style={styles.paymentMethodDetail}>Coming soon</Text>
            </View>
            <View style={styles.radioDisabled} />
          </View>
          <RydoButton label="Done" onPress={onClose} />
        </View>
      </View>
    </Modal>
  );
}

function CancellationModal({
  loading,
  error,
  onClose,
  onConfirm,
}: {
  loading: boolean;
  error?: string;
  onClose(): void;
  onConfirm(reason: string): void;
}) {
  const [stage, setStage] = useState<'confirm' | 'reason'>('confirm');
  const [selectedReason, setSelectedReason] = useState<string | null>(null);
  const [otherReason, setOtherReason] = useState('');
  const cancellationReasons = [
    'Driver asked me to cancel',
    'Driver is not getting closer',
    'Pickup wait is too long',
    'Pickup location is incorrect',
    'Price or payment issue',
    'Found another ride',
    'Driver made me feel unsafe',
    'My plans changed',
    'Other',
  ];
  const finalReason = selectedReason === 'Other' ? otherReason.trim() : selectedReason;

  if (stage === 'reason') {
    return (
      <Modal visible transparent animationType="slide" statusBarTranslucent onRequestClose={onClose}>
        <View style={styles.modalBackdrop}>
          <View style={styles.cancellationSheet}>
            <View style={styles.modalHandle} />
            <View style={styles.cancellationHeader}>
              <Pressable accessibilityLabel="Back to cancellation confirmation" style={styles.modalClose} onPress={() => setStage('confirm')}>
                <Text style={styles.modalBackText}>‹</Text>
              </Pressable>
              <Text selectable style={styles.cancellationTitle}>What went wrong?</Text>
              <Pressable accessibilityLabel="Close cancellation" style={styles.modalClose} onPress={onClose}>
                <Text style={styles.modalCloseText}>×</Text>
              </Pressable>
            </View>
            <Text selectable style={styles.cancellationSubtitle}>
              Choose the reason that best describes why you need to cancel.
            </Text>
            <KeyboardAwareScrollView
              bottomOffset={spacing.xl}
              contentContainerStyle={styles.cancellationReasons}
              contentInsetAdjustmentBehavior="automatic"
              keyboardShouldPersistTaps="handled"
              mode="insets"
              showsVerticalScrollIndicator={false}
            >
              {cancellationReasons.map((reason) => {
                const selected = selectedReason === reason;
                return (
                  <Pressable
                    key={reason}
                    accessibilityRole="radio"
                    accessibilityState={{ checked: selected }}
                    style={[styles.cancellationReason, selected && styles.cancellationReasonSelected]}
                    onPress={() => setSelectedReason(reason)}
                  >
                    <Text selectable style={[styles.cancellationReasonText, selected && styles.cancellationReasonTextSelected]}>
                      {reason}
                    </Text>
                    <View style={[styles.reasonRadio, selected && styles.reasonRadioSelected]}>
                      {selected ? <View style={styles.reasonRadioCenter} /> : null}
                    </View>
                  </Pressable>
                );
              })}
              {selectedReason === 'Other' ? (
                <View style={styles.otherReasonBlock}>
                  <Text selectable style={styles.otherReasonLabel}>Tell us what happened</Text>
                  <TextInput
                    value={otherReason}
                    onChangeText={setOtherReason}
                    placeholder="Describe the problem"
                    placeholderTextColor={colors.textMuted}
                    maxLength={250}
                    multiline
                    autoFocus
                    style={styles.otherReasonInput}
                  />
                  <Text selectable style={styles.characterCount}>{otherReason.length}/250</Text>
                </View>
              ) : null}
              {error ? <Text selectable style={styles.error}>{error}</Text> : null}
            </KeyboardAwareScrollView>
            <RydoButton
              label="Cancel ride"
              loading={loading}
              disabled={!finalReason}
              onPress={() => finalReason && onConfirm(finalReason)}
            />
          </View>
        </View>
      </Modal>
    );
  }

  return (
    <Modal visible transparent animationType="fade" statusBarTranslucent onRequestClose={onClose}>
      <View style={styles.modalBackdropCentered}>
        <View style={styles.cancelModalCard}>
          <Text selectable style={styles.cancelModalTitle}>Cancel this ride?</Text>
          <Text selectable style={styles.cancelModalMessage}>
            Cancelling may increase the time it takes to get another driver.
          </Text>
          {error ? <Text selectable style={styles.error}>{error}</Text> : null}
          <RydoButton label="Keep waiting" onPress={onClose} />
          <Pressable disabled={loading} style={styles.destructiveButton} onPress={() => setStage('reason')}>
            <Text style={styles.destructiveButtonText}>Continue to cancel</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

function FareOptionCard({
  option,
  currency,
  selected,
  onPress,
}: {
  option: FareOption;
  currency: string;
  selected: boolean;
  onPress(): void;
}) {
  const descriptions: Record<RideCategory, string> = {
    Solo: 'Best value for everyday rides',
    Group: 'More room for your journey',
    GroupPlus: 'Our largest ride option',
  };

  return (
    <TouchableOpacity
      accessibilityRole="radio"
      accessibilityState={{ checked: selected }}
      style={[styles.fareOption, selected && styles.fareOptionSelected]}
      onPress={onPress}
    >
      <View style={[styles.fareOptionIcon, selected && styles.fareOptionIconSelected]}>
        <RideAssetIcon color={selected ? colors.white : colors.blue} size={23} />
      </View>
      <View style={styles.fareOptionCopy}>
        <Text selectable style={styles.fareOptionTitle}>{categoryLabel(option.category)}</Text>
        <Text selectable numberOfLines={1} style={styles.fareOptionDetail}>{descriptions[option.category]}</Text>
      </View>
      <View style={styles.fareOptionPriceBlock}>
        <Text selectable style={styles.fareOptionPrice}>{formatFare(option.total, currency)}</Text>
        <Text selectable style={styles.fareOptionRate}>R{option.ratePerKilometre.toFixed(2)}/km</Text>
      </View>
    </TouchableOpacity>
  );
}

function categoryLabel(category: RideCategory) {
  return category === 'GroupPlus' ? 'Group+' : category;
}

function formatFare(amount: number, currency: string) {
  return new Intl.NumberFormat('en-ZA', {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(amount);
}

function shortAddress(address: string) {
  return address.split(',')[0]?.trim() || address;
}

function LocationInput({
  label,
  kind,
  value,
  active,
  onPress,
}: {
  label: string;
  kind: Field;
  value: string;
  active: boolean;
  onPress(): void;
}) {
  return (
    <TouchableOpacity style={[styles.field, active && styles.fieldActive]} onPress={onPress}>
      {kind === 'pickup' ? (
        <View style={styles.pickupMarker}><View style={styles.pickupMarkerCenter} /></View>
      ) : (
        <RydoIcon name="map-pin" color="#EF3155" size={25} />
      )}
      <View style={styles.fieldCopy}>
        <Text selectable style={styles.fieldLabel}>{kind === 'pickup' ? 'Pickup location' : label}</Text>
        <Text selectable numberOfLines={1} style={styles.fieldValue}>
          {value || (kind === 'pickup' ? 'Choose pickup location' : 'Where are you going?')}
        </Text>
      </View>
      <RydoIcon name="chevron-right" color={colors.textMuted} size={18} style={{ transform: [{ rotate: '90deg' }] }} />
    </TouchableOpacity>
  );
}

function RideOption({
  icon,
  label,
  detail,
  selected = false,
  onPress,
}: {
  icon: 'car' | 'clock';
  label: string;
  detail: string;
  selected?: boolean;
  onPress(): void;
}) {
  return (
    <TouchableOpacity style={[styles.rideOption, selected && styles.rideOptionSelected]} onPress={onPress}>
      <View style={[styles.rideOptionIcon, selected && styles.rideOptionIconSelected]}>
        {icon === 'car' ? (
          <RideAssetIcon color={selected ? colors.white : colors.blue} size={24} />
        ) : (
          <RydoIcon name={icon} color={selected ? colors.white : colors.blue} size={24} />
        )}
      </View>
      <Text selectable style={styles.rideOptionLabel}>{label}</Text>
      <Text selectable numberOfLines={1} style={styles.rideOptionDetail}>{detail}</Text>
    </TouchableOpacity>
  );
}

function RideAssetIcon({ color, size }: { color: string; size: number }) {
  return (
    <Image
      accessibilityIgnoresInvertColors
      contentFit="contain"
      source={require('../../../assets/icons/home/ride.png')}
      style={{ width: size, height: size, tintColor: color }}
    />
  );
}

function RecentDestination({ trip, onPress }: { trip: Trip; onPress(): void }) {
  return (
    <TouchableOpacity style={styles.recentDestination} onPress={onPress}>
      <View style={styles.recentIcon}>
        <RydoIcon name="clock" color={colors.textMuted} size={22} />
      </View>
      <View style={{ flex: 1, gap: 3 }}>
        <Text selectable numberOfLines={1} style={styles.recentTitle}>{trip.destinationAddress.split(',')[0]}</Text>
        <Text selectable numberOfLines={1} style={styles.recentDetail}>{trip.destinationAddress}</Text>
      </View>
      <RydoIcon name="chevron-right" color={colors.textMuted} size={17} />
    </TouchableOpacity>
  );
}

function createSessionToken() {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 14)}`;
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

function tripPanelTitle(status: Trip['status']) {
  const titles: Record<Trip['status'], string> = {
    Requested: 'Looking for a driver…',
    Accepted: 'Driver found',
    DriverArrived: 'Your driver has arrived',
    InProgress: "You're on your way",
    Completed: 'Ride complete',
    Cancelled: 'Ride cancelled',
  };
  return titles[status];
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
  locatingOverlay: {
    position: 'absolute',
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
    alignItems: 'center',
    justifyContent: 'center',
    gap: spacing.md,
    backgroundColor: '#DDE8E3',
  },
  locatingText: { color: colors.navy, fontSize: 15, fontWeight: '700' },
  locationButton: { position: 'absolute', right: spacing.lg },
  greeting: { position: 'absolute', left: spacing.md, borderRadius: 999, paddingHorizontal: 17, paddingVertical: 12, backgroundColor: 'rgba(255,255,255,0.96)', boxShadow: '0 7px 20px rgba(11,31,58,0.16)' },
  greetingText: { color: colors.navy, fontWeight: '800' },
  routePill: { position: 'absolute', left: 14, right: 14, minHeight: 48, borderRadius: 24, borderCurve: 'continuous', paddingHorizontal: 8, flexDirection: 'row', alignItems: 'center', gap: 9, backgroundColor: 'rgba(255,255,255,0.94)', boxShadow: '0 8px 24px rgba(11,31,58,0.18)' },
  routePillClose: { width: 34, height: 34, borderRadius: 17, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.surface },
  routePillCloseText: { color: colors.navy, fontSize: 25, lineHeight: 27, fontWeight: '500' },
  routePillStatus: { width: 34, height: 34, borderRadius: 17, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.blueMuted },
  routePillStatusCenter: { width: 12, height: 12, borderRadius: 6, backgroundColor: colors.blue },
  routePillText: { flex: 1, color: colors.navy, fontSize: 12, fontWeight: '800' },
  sheetContent: { gap: 10 },
  landingTitle: { color: colors.navy, fontSize: 27, lineHeight: 33, fontWeight: '900' },
  focusedTitle: { color: colors.navy, fontSize: 22, lineHeight: 28, fontWeight: '900' },
  focusedSubtitle: { color: colors.textMuted, fontSize: 13, lineHeight: 18 },
  focusedHeaderRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  routeReadyHeader: { minHeight: 58, flexDirection: 'row', alignItems: 'center', gap: 12 },
  routeReadyHeadingCopy: { flex: 1, gap: 3 },
  routeReadyEdit: { width: 46, height: 46, borderRadius: 23, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.surface, boxShadow: '0 5px 16px rgba(11,31,58,0.10)' },
  routeLocationCard: { minHeight: 82, borderWidth: 1, borderColor: colors.border, borderRadius: 21, borderCurve: 'continuous', paddingHorizontal: 14, paddingVertical: 12, flexDirection: 'row', alignItems: 'center', gap: 13, backgroundColor: '#F8FAFD', boxShadow: '0 5px 16px rgba(11,31,58,0.07)' },
  routeLocationIcon: { width: 50, height: 50, borderRadius: 25, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.blue },
  routeLocationCopy: { flex: 1, minWidth: 0, gap: 3 },
  routeLocationLabel: { color: colors.blue, fontSize: 12, lineHeight: 16, fontWeight: '800' },
  routeLocationAddress: { color: colors.navy, fontSize: 15, lineHeight: 20, fontWeight: '800' },
  routeMetrics: { minHeight: 50, paddingHorizontal: 4, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  routeDuration: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  routeDurationText: { color: colors.navy, fontSize: 14, fontWeight: '700', fontVariant: ['tabular-nums'] },
  routeFare: { color: colors.blue, fontSize: 25, lineHeight: 30, fontWeight: '900', fontVariant: ['tabular-nums'] },
  chooseRideButton: { minHeight: 56, borderRadius: 18, borderCurve: 'continuous', paddingHorizontal: 20, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 12, backgroundColor: colors.blue, boxShadow: '0 9px 20px rgba(18,97,216,0.24)' },
  chooseRideButtonText: { color: colors.white, fontSize: 17, fontWeight: '900' },
  chooseRideArrow: { position: 'absolute', right: 20, color: colors.white, fontSize: 26, lineHeight: 28 },
  editRouteText: { color: colors.blue, fontSize: 13, fontWeight: '800' },
  focusedActions: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  primaryActionWrap: { flex: 1 },
  secondaryButton: { minHeight: 50, borderRadius: 17, borderCurve: 'continuous', paddingHorizontal: 20, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.surface },
  secondaryButtonText: { color: colors.navy, fontSize: 15, fontWeight: '800' },
  compactTripSummary: { minHeight: 50, borderRadius: 16, borderCurve: 'continuous', paddingHorizontal: 14, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', backgroundColor: colors.blueMuted },
  compactTripCategory: { color: colors.navy, fontSize: 15, fontWeight: '900' },
  compactTripFare: { color: colors.blue, fontSize: 17, fontWeight: '900', fontVariant: ['tabular-nums'] },
  pickupAddressCard: { minHeight: 62, borderRadius: 17, borderCurve: 'continuous', paddingHorizontal: 14, paddingVertical: 11, flexDirection: 'row', alignItems: 'center', gap: 11, backgroundColor: '#F4F6F9' },
  pickupAddressText: { flex: 1, color: colors.navy, fontSize: 14, lineHeight: 19, fontWeight: '700' },
  statusHeadingRow: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  statusHeadingCopy: { flex: 1, gap: 2 },
  statusPulse: { width: 46, height: 46, borderRadius: 23, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.blueMuted },
  statusPulseCenter: { width: 18, height: 18, borderRadius: 9, backgroundColor: colors.blue },
  progressTrack: { height: 4, borderRadius: 2, overflow: 'hidden', backgroundColor: colors.border },
  progressValue: { width: '38%', height: '100%', borderRadius: 2, backgroundColor: colors.blue },
  centeredStatus: { minHeight: 190, alignItems: 'center', justifyContent: 'center', gap: 10 },
  cancelAction: { minHeight: 48, borderRadius: 16, borderCurve: 'continuous', alignItems: 'center', justifyContent: 'center', backgroundColor: '#FFF0F2' },
  cancelActionText: { color: colors.danger, fontSize: 15, fontWeight: '900' },
  rideOptions: { flexDirection: 'row', gap: 9 },
  rideOption: {
    flex: 1,
    minHeight: 92,
    borderWidth: 1,
    borderColor: colors.border,
    borderCurve: 'continuous',
    borderRadius: 20,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 3,
    paddingHorizontal: 7,
    backgroundColor: '#F2F4F7',
  },
  rideOptionSelected: { borderColor: '#BCD3F7', backgroundColor: colors.blueMuted },
  rideOptionIcon: { width: 36, height: 36, borderRadius: 18, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.white },
  rideOptionIconSelected: { backgroundColor: colors.blue },
  rideOptionLabel: { color: colors.navy, fontSize: 14, fontWeight: '800' },
  rideOptionDetail: { color: colors.textMuted, fontSize: 10, fontWeight: '600' },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: 4 },
  sectionTitle: { color: colors.navy, fontSize: 16, fontWeight: '900' },
  sectionAction: { color: colors.blue, fontSize: 12, fontWeight: '800' },
  recentDestination: { minHeight: 62, flexDirection: 'row', alignItems: 'center', gap: 12 },
  recentIcon: { width: 48, height: 48, borderRadius: 15, alignItems: 'center', justifyContent: 'center', backgroundColor: '#EEF1F4' },
  recentTitle: { color: colors.navy, fontSize: 15, fontWeight: '800' },
  recentDetail: { color: colors.textMuted, fontSize: 12 },
  emptyRecent: { minHeight: 66, borderRadius: 18, paddingHorizontal: 14, flexDirection: 'row', alignItems: 'center', gap: 12, backgroundColor: '#F4F6F8' },
  emptyRecentTitle: { color: colors.navy, fontSize: 14, fontWeight: '800' },
  emptyRecentDetail: { color: colors.textMuted, fontSize: 11 },
  field: {
    minHeight: 66,
    borderWidth: 1,
    borderColor: colors.border,
    borderCurve: 'continuous',
    borderRadius: 20,
    paddingHorizontal: 16,
    paddingVertical: 12,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 13,
    backgroundColor: '#F9FAFD',
  },
  fieldActive: { borderColor: colors.blue, backgroundColor: '#F4F8FF' },
  fieldCopy: { flex: 1, minWidth: 0, gap: 2 },
  fieldLabel: { color: colors.textMuted, fontSize: 12, fontWeight: '700' },
  fieldValue: { color: colors.navy, fontSize: 16, fontWeight: '700' },
  pickupMarker: { width: 23, height: 23, borderRadius: 12, borderWidth: 2, borderColor: colors.blue, alignItems: 'center', justifyContent: 'center' },
  pickupMarkerCenter: { width: 11, height: 11, borderRadius: 6, backgroundColor: colors.blue },
  searchInput: { minHeight: 48, borderRadius: 17, backgroundColor: '#F9FAFD' },
  suggestion: { borderTopWidth: 1, borderTopColor: colors.border, paddingVertical: 8 },
  suggestionTitle: { color: colors.navy, fontWeight: '700' },
  suggestionDetail: { color: colors.textMuted, fontSize: 12, marginTop: 2 },
  status: { color: colors.textMuted, fontSize: 13 },
  routeSummary: { borderRadius: 14, padding: 12, textAlign: 'center', backgroundColor: colors.blue, color: colors.white, fontSize: 16, fontWeight: '800' },
  fareOption: { minHeight: 72, borderWidth: 1, borderColor: colors.border, borderCurve: 'continuous', borderRadius: 18, paddingHorizontal: 13, paddingVertical: 10, flexDirection: 'row', alignItems: 'center', gap: 11, backgroundColor: '#F9FAFD' },
  fareOptionSelected: { borderWidth: 2, borderColor: colors.blue, backgroundColor: colors.blueMuted },
  fareOptionIcon: { width: 42, height: 42, borderRadius: 21, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.white },
  fareOptionIconSelected: { backgroundColor: colors.blue },
  fareOptionCopy: { flex: 1, minWidth: 0, gap: 2 },
  fareOptionTitle: { color: colors.navy, fontSize: 16, fontWeight: '900' },
  fareOptionDetail: { color: colors.textMuted, fontSize: 11 },
  fareOptionPriceBlock: { alignItems: 'flex-end', gap: 2 },
  fareOptionPrice: { color: colors.navy, fontSize: 16, fontWeight: '900', fontVariant: ['tabular-nums'] },
  fareOptionRate: { color: colors.textMuted, fontSize: 10, fontWeight: '700', fontVariant: ['tabular-nums'] },
  demandNotice: { borderRadius: 14, padding: 11, color: '#8A5700', backgroundColor: '#FFF4D6', fontSize: 12, lineHeight: 17, fontWeight: '700' },
  paymentMethodRow: { minHeight: 62, borderRadius: 17, borderCurve: 'continuous', paddingHorizontal: 13, flexDirection: 'row', alignItems: 'center', gap: 11, backgroundColor: '#F4F6F9' },
  paymentMethodCopy: { flex: 1, minWidth: 0, gap: 2 },
  paymentMethodTitle: { color: colors.navy, fontSize: 14, fontWeight: '900' },
  paymentMethodDetail: { color: colors.textMuted, fontSize: 11 },
  cashIcon: { width: 34, height: 34, borderRadius: 10, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.blueMuted },
  cashIconText: { color: colors.blue, fontSize: 15, fontWeight: '900' },
  cardIcon: { width: 34, height: 24, borderRadius: 6, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.navy },
  cardIconText: { color: colors.white, fontSize: 9, fontWeight: '900' },
  modalBackdrop: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(11,31,58,0.42)' },
  modalBackdropCentered: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24, backgroundColor: 'rgba(11,31,58,0.52)' },
  paymentModalCard: { borderTopLeftRadius: 30, borderTopRightRadius: 30, borderCurve: 'continuous', paddingHorizontal: 20, paddingTop: 10, paddingBottom: 30, gap: 14, backgroundColor: colors.white },
  modalHandle: { width: 44, height: 5, borderRadius: 3, alignSelf: 'center', backgroundColor: colors.border },
  modalHeader: { minHeight: 44, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  modalTitle: { color: colors.navy, fontSize: 25, fontWeight: '900' },
  modalClose: { width: 36, height: 36, borderRadius: 18, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.surface },
  modalCloseText: { color: colors.navy, fontSize: 24, lineHeight: 26 },
  modalBackText: { color: colors.navy, fontSize: 30, lineHeight: 30, marginTop: -2 },
  modalSectionLabel: { color: colors.navy, fontSize: 13, fontWeight: '900' },
  paymentChoiceSelected: { minHeight: 66, borderWidth: 2, borderColor: colors.blue, borderRadius: 18, borderCurve: 'continuous', paddingHorizontal: 13, flexDirection: 'row', alignItems: 'center', gap: 11, backgroundColor: colors.blueMuted },
  paymentChoiceDisabled: { minHeight: 66, borderWidth: 1, borderColor: colors.border, borderRadius: 18, borderCurve: 'continuous', paddingHorizontal: 13, flexDirection: 'row', alignItems: 'center', gap: 11, opacity: 0.62, backgroundColor: '#F7F8FA' },
  paymentDisabledTitle: { color: colors.textMuted, fontSize: 14, fontWeight: '800' },
  radioSelected: { width: 22, height: 22, borderRadius: 11, borderWidth: 2, borderColor: colors.blue, alignItems: 'center', justifyContent: 'center' },
  radioCenter: { width: 10, height: 10, borderRadius: 5, backgroundColor: colors.blue },
  radioDisabled: { width: 22, height: 22, borderRadius: 11, borderWidth: 2, borderColor: colors.border },
  cancelModalCard: { width: '100%', maxWidth: 390, borderRadius: 28, borderCurve: 'continuous', padding: 22, gap: 14, backgroundColor: colors.white, boxShadow: '0 18px 48px rgba(11,31,58,0.28)' },
  cancelModalTitle: { color: colors.navy, fontSize: 23, lineHeight: 29, textAlign: 'center', fontWeight: '900' },
  cancelModalMessage: { color: colors.textMuted, fontSize: 13, lineHeight: 19, textAlign: 'center' },
  cancellationSheet: { maxHeight: '92%', borderTopLeftRadius: 30, borderTopRightRadius: 30, borderCurve: 'continuous', paddingHorizontal: 20, paddingTop: 10, paddingBottom: 24, gap: 12, backgroundColor: colors.white },
  cancellationHeader: { minHeight: 44, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  cancellationTitle: { flex: 1, color: colors.navy, fontSize: 22, lineHeight: 28, textAlign: 'center', fontWeight: '900' },
  cancellationSubtitle: { color: colors.textMuted, fontSize: 13, lineHeight: 19, textAlign: 'center' },
  cancellationReasons: { gap: 8, paddingVertical: 4 },
  cancellationReason: { minHeight: 51, borderWidth: 1, borderColor: colors.border, borderRadius: 16, borderCurve: 'continuous', paddingHorizontal: 14, flexDirection: 'row', alignItems: 'center', gap: 12, backgroundColor: '#F9FAFD' },
  cancellationReasonSelected: { borderWidth: 2, borderColor: colors.blue, backgroundColor: colors.blueMuted },
  cancellationReasonText: { flex: 1, color: colors.navy, fontSize: 14, lineHeight: 19, fontWeight: '700' },
  cancellationReasonTextSelected: { color: colors.blue, fontWeight: '900' },
  reasonRadio: { width: 22, height: 22, borderRadius: 11, borderWidth: 2, borderColor: colors.border, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.white },
  reasonRadioSelected: { borderColor: colors.blue },
  reasonRadioCenter: { width: 10, height: 10, borderRadius: 5, backgroundColor: colors.blue },
  otherReasonBlock: { gap: 7, paddingTop: 3 },
  otherReasonLabel: { color: colors.navy, fontSize: 13, fontWeight: '900' },
  otherReasonInput: { minHeight: 92, borderWidth: 1, borderColor: colors.blue, borderRadius: 16, borderCurve: 'continuous', paddingHorizontal: 13, paddingVertical: 11, color: colors.navy, backgroundColor: '#F9FAFD', fontSize: 14, lineHeight: 19, textAlignVertical: 'top' },
  characterCount: { color: colors.textMuted, fontSize: 11, textAlign: 'right', fontVariant: ['tabular-nums'] },
  destructiveButton: { minHeight: 50, borderRadius: 17, borderCurve: 'continuous', alignItems: 'center', justifyContent: 'center', backgroundColor: colors.danger },
  destructiveButtonText: { color: colors.white, fontSize: 15, fontWeight: '900' },
  paymentRow: { flexDirection: 'row', gap: 8 },
  paymentSelected: { borderRadius: 999, backgroundColor: colors.navy, paddingHorizontal: 14, paddingVertical: 9 },
  paymentSelectedText: { color: colors.white, fontWeight: '800' },
  paymentDisabled: { borderRadius: 999, backgroundColor: colors.surface, paddingHorizontal: 14, paddingVertical: 9 },
  paymentDisabledText: { color: colors.textMuted, fontWeight: '700' },
  error: { color: colors.danger, fontSize: 13, lineHeight: 18 },
});
