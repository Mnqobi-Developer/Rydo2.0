import type { GeoCoordinate } from '@rydo/mobile-api-client';
import { colors } from '@rydo/mobile-design-system';
import { forwardRef, useEffect, useImperativeHandle, useRef } from 'react';
import MapView, { Marker, Polyline, PROVIDER_GOOGLE } from 'react-native-maps';

export interface RideMapHandle {
  fitRoute(coordinates: GeoCoordinate[]): void;
  focusCoordinate(coordinate: GeoCoordinate): void;
}

interface RideMapProps {
  currentLocation: GeoCoordinate | null;
  driverLocation: GeoCoordinate | null;
  pickup: GeoCoordinate | null;
  destination: GeoCoordinate | null;
  route: GeoCoordinate[];
  onMapPress(coordinate: GeoCoordinate): void;
}

export const RideMap = forwardRef<RideMapHandle, RideMapProps>(function RideMap(
  { currentLocation, driverLocation, pickup, destination, route, onMapPress },
  forwardedRef,
) {
  const mapRef = useRef<MapView>(null);

  useImperativeHandle(forwardedRef, () => ({
    fitRoute(coordinates) {
      if (coordinates.length > 1) {
        mapRef.current?.fitToCoordinates(coordinates, {
          animated: true,
          edgePadding: { top: 100, right: 50, bottom: 330, left: 50 },
        });
      }
    },
    focusCoordinate(coordinate) {
      mapRef.current?.animateToRegion(
        {
          ...coordinate,
          latitudeDelta: 0.012,
          longitudeDelta: 0.012,
        },
        500,
      );
    },
  }));

  useEffect(() => {
    if (!currentLocation) return;

    mapRef.current?.animateToRegion(
      {
        ...currentLocation,
        latitudeDelta: 0.012,
        longitudeDelta: 0.012,
      },
      500,
    );
  }, [currentLocation]);

  return (
    <MapView
      ref={mapRef}
      provider={PROVIDER_GOOGLE}
      style={{ flex: 1 }}
      initialRegion={{
        latitude: -26.2041,
        longitude: 28.0473,
        latitudeDelta: 0.16,
        longitudeDelta: 0.16,
      }}
      onPress={(event) => onMapPress(event.nativeEvent.coordinate)}
      showsUserLocation
      showsMyLocationButton={false}
    >
      {pickup ? <Marker coordinate={pickup} title="Pickup" pinColor={colors.blue} /> : null}
      {destination ? <Marker coordinate={destination} title="Destination" pinColor="#D83A3A" /> : null}
      {driverLocation ? (
        <Marker coordinate={driverLocation} title="Your driver" pinColor={colors.navy} />
      ) : null}
      {route.length > 1 ? (
        <Polyline coordinates={route} strokeColor={colors.blue} strokeWidth={5} />
      ) : null}
    </MapView>
  );
});
