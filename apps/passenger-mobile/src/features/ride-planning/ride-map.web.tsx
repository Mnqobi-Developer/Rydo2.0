/// <reference types="google.maps" />

import { importLibrary, setOptions } from '@googlemaps/js-api-loader';
import type { GeoCoordinate } from '@rydo/mobile-api-client';
import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { appConfig } from '@/config/environment';
import { colors } from '@/theme/colors';

export interface RideMapHandle {
  fitRoute(coordinates: GeoCoordinate[]): void;
  focusCoordinate(coordinate: GeoCoordinate): void;
}

interface RideMapProps {
  currentLocation: GeoCoordinate | null;
  pickup: GeoCoordinate | null;
  destination: GeoCoordinate | null;
  route: GeoCoordinate[];
  onMapPress(coordinate: GeoCoordinate): void;
}

const JOHANNESBURG = { latitude: -26.2041, longitude: 28.0473 };
let librariesPromise:
  | Promise<{
      core: google.maps.CoreLibrary;
      maps: google.maps.MapsLibrary;
      marker: google.maps.MarkerLibrary;
    }>
  | null = null;

function loadGoogleMaps(apiKey: string) {
  if (!librariesPromise) {
    setOptions({ key: apiKey, v: 'weekly' });
    librariesPromise = Promise.all([
      importLibrary('core') as Promise<google.maps.CoreLibrary>,
      importLibrary('maps') as Promise<google.maps.MapsLibrary>,
      importLibrary('marker') as Promise<google.maps.MarkerLibrary>,
    ]).then(([core, maps, marker]) => ({ core, maps, marker }));
  }

  return librariesPromise;
}

export const RideMap = forwardRef<RideMapHandle, RideMapProps>(function RideMap(
  { currentLocation, pickup, destination, route, onMapPress },
  forwardedRef,
) {
  const containerRef = useRef<View>(null);
  const mapRef = useRef<google.maps.Map | null>(null);
  const coreLibraryRef = useRef<google.maps.CoreLibrary | null>(null);
  const mapsLibraryRef = useRef<google.maps.MapsLibrary | null>(null);
  const markerLibraryRef = useRef<google.maps.MarkerLibrary | null>(null);
  const pickupMarkerRef = useRef<google.maps.Marker | null>(null);
  const destinationMarkerRef = useRef<google.maps.Marker | null>(null);
  const routePolylineRef = useRef<google.maps.Polyline | null>(null);
  const onMapPressRef = useRef(onMapPress);
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  onMapPressRef.current = onMapPress;

  useImperativeHandle(forwardedRef, () => ({
    fitRoute(coordinates) {
      const map = mapRef.current;
      const core = coreLibraryRef.current;
      if (!map || !core || coordinates.length < 2) return;

      const bounds = new core.LatLngBounds();
      coordinates.forEach((coordinate) => bounds.extend(toLatLngLiteral(coordinate)));
      map.fitBounds(bounds, { top: 100, right: 50, bottom: 330, left: 50 });
    },
    focusCoordinate(coordinate) {
      const map = mapRef.current;
      if (!map) return;

      map.panTo(toLatLngLiteral(coordinate));
      if ((map.getZoom() ?? 0) < 16) {
        map.setZoom(16);
      }
    },
  }));

  useEffect(() => {
    const apiKey = appConfig.googleMapsWebApiKey;
    if (!apiKey) {
      setError('Add EXPO_PUBLIC_GOOGLE_MAPS_WEB_API_KEY to render the web map.');
      return;
    }

    let disposed = false;
    let clickListener: google.maps.MapsEventListener | undefined;

    void loadGoogleMaps(apiKey)
      .then(({ core, maps, marker }) => {
        if (disposed || !containerRef.current) return;

        coreLibraryRef.current = core;
        mapsLibraryRef.current = maps;
        markerLibraryRef.current = marker;
        const map = new maps.Map(containerRef.current as unknown as HTMLElement, {
          center: toLatLngLiteral(JOHANNESBURG),
          zoom: 11,
          clickableIcons: false,
          disableDefaultUI: true,
          gestureHandling: 'greedy',
          zoomControl: true,
        });
        clickListener = map.addListener('click', (event: google.maps.MapMouseEvent) => {
          if (!event.latLng) return;
          onMapPressRef.current({
            latitude: event.latLng.lat(),
            longitude: event.latLng.lng(),
          });
        });
        mapRef.current = map;
        setReady(true);
      })
      .catch(() => {
        if (!disposed) {
          setError('Google Maps could not load. Check the browser key and Maps JavaScript API.');
        }
      });

    return () => {
      disposed = true;
      clickListener?.remove();
      pickupMarkerRef.current?.setMap(null);
      destinationMarkerRef.current?.setMap(null);
      routePolylineRef.current?.setMap(null);
      mapRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    if (!ready || !map || !currentLocation) return;

    map.panTo(toLatLngLiteral(currentLocation));
    map.setZoom(16);
  }, [currentLocation, ready]);

  useEffect(() => {
    const map = mapRef.current;
    const marker = markerLibraryRef.current;
    if (!ready || !map || !marker) return;

    if (!pickup) {
      pickupMarkerRef.current?.setMap(null);
      pickupMarkerRef.current = null;
      return;
    }

    if (!pickupMarkerRef.current) {
      pickupMarkerRef.current = new marker.Marker({
        map,
        position: toLatLngLiteral(pickup),
        title: 'Pickup',
      });
    } else {
      pickupMarkerRef.current.setPosition(toLatLngLiteral(pickup));
      pickupMarkerRef.current.setMap(map);
    }
  }, [pickup, ready]);

  useEffect(() => {
    const map = mapRef.current;
    const marker = markerLibraryRef.current;
    if (!ready || !map || !marker) return;

    if (!destination) {
      destinationMarkerRef.current?.setMap(null);
      destinationMarkerRef.current = null;
      return;
    }

    if (!destinationMarkerRef.current) {
      destinationMarkerRef.current = new marker.Marker({
        map,
        position: toLatLngLiteral(destination),
        title: 'Destination',
      });
    } else {
      destinationMarkerRef.current.setPosition(toLatLngLiteral(destination));
      destinationMarkerRef.current.setMap(map);
    }
  }, [destination, ready]);

  useEffect(() => {
    const map = mapRef.current;
    const maps = mapsLibraryRef.current;
    if (!ready || !map || !maps) return;

    routePolylineRef.current?.setMap(null);
    routePolylineRef.current = null;
    if (route.length < 2) return;

    routePolylineRef.current = new maps.Polyline({
      map,
      path: route.map(toLatLngLiteral),
      strokeColor: colors.blue,
      strokeOpacity: 1,
      strokeWeight: 5,
    });
  }, [ready, route]);

  return (
    <View style={styles.container}>
      <View ref={containerRef} style={styles.map} />
      {error ? (
        <View style={styles.errorPanel}>
          <Text selectable style={styles.errorTitle}>Map unavailable</Text>
          <Text selectable style={styles.errorMessage}>{error}</Text>
        </View>
      ) : null}
    </View>
  );
});

function toLatLngLiteral(coordinate: GeoCoordinate): google.maps.LatLngLiteral {
  return { lat: coordinate.latitude, lng: coordinate.longitude };
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#DDE8E3' },
  map: { flex: 1 },
  errorPanel: {
    position: 'absolute',
    top: '35%',
    alignSelf: 'center',
    maxWidth: 320,
    borderRadius: 18,
    padding: 18,
    backgroundColor: colors.white,
  },
  errorTitle: { color: colors.navy, fontSize: 16, fontWeight: '800', textAlign: 'center' },
  errorMessage: { color: colors.textMuted, marginTop: 6, textAlign: 'center' },
});
