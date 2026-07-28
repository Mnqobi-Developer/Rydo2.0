import { TouchableOpacity } from '@gorhom/bottom-sheet';
import {
  decodeGooglePolyline,
  type GeoCoordinate,
  type Place,
  type PlacePrediction,
  type RoutePreview,
} from '@rydo/mobile-api-client';
import {
  MapControl,
  RydoBottomSheet,
  RydoBottomSheetTextInput,
  colors,
  spacing,
  typography,
} from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import * as Location from 'expo-location';
import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiClient } from '@/api';

import { RideMap, type RideMapHandle } from './ride-map';

type Field = 'pickup' | 'destination';

export function RidePlannerScreen() {
  const insets = useSafeAreaInsets();
  const mapRef = useRef<RideMapHandle>(null);
  const [activeField, setActiveField] = useState<Field>('destination');
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [pickup, setPickup] = useState<Place | null>(null);
  const [destination, setDestination] = useState<Place | null>(null);
  const [message, setMessage] = useState('Long-press the map or search for a destination.');
  const sessionToken = useRef(createSessionToken()).current;

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedQuery(query.trim()), 350);
    return () => clearTimeout(timeout);
  }, [query]);

  const autocomplete = useQuery({
    queryKey: ['maps', 'autocomplete', debouncedQuery, pickup?.location],
    enabled: debouncedQuery.length >= 3,
    queryFn: ({ signal }) => {
      const params = new URLSearchParams({ query: debouncedQuery, sessionToken });
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
    } catch {
      selectPlace({ placeId: '', name: 'Pinned location', address: 'Pinned location', location }, field);
      setMessage('Location pinned. Sign in and configure Maps to resolve its address.');
    }
  }

  async function selectPrediction(prediction: PlacePrediction) {
    const place = await apiClient.get<Place>(
      `/api/v1/maps/places/${encodeURIComponent(prediction.placeId)}?sessionToken=${encodeURIComponent(sessionToken)}`,
    );
    selectPlace(place, activeField);
  }

  function selectPlace(place: Place, field: Field) {
    if (field === 'pickup') setPickup(place);
    else setDestination(place);
    setQuery('');
    setMessage(field === 'pickup' ? 'Pickup selected.' : 'Destination selected.');
  }

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
      <RydoBottomSheet snapPoints={['45%', '75%']} bottomInset={insets.bottom + spacing.md}>
        <Text selectable style={styles.title}>Plan your ride</Text>
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
        {route.data ? (
          <Text selectable style={styles.routeSummary}>
            {(route.data.distanceMeters / 1000).toFixed(1)} km · {Math.max(1, Math.ceil(route.data.durationSeconds / 60))} min
          </Text>
        ) : (
          <Text selectable style={styles.status}>{message}</Text>
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

const styles = StyleSheet.create({
  container: { flex: 1 },
  locationButton: { position: 'absolute', right: spacing.lg },
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
});
