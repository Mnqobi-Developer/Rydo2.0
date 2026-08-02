import type { GeoCoordinate } from '@rydo/mobile-api-client';
import { colors } from '@rydo/mobile-design-system';
import { useEffect, useRef } from 'react';
import MapView, { Marker, Polyline, PROVIDER_GOOGLE } from 'react-native-maps';

interface DriverNavigationMapProps {
  currentLocation: GeoCoordinate | null;
  destination: GeoCoordinate;
  pickup: GeoCoordinate;
  route: GeoCoordinate[];
}

export function DriverNavigationMap({
  currentLocation,
  destination,
  pickup,
  route,
}: DriverNavigationMapProps) {
  const mapRef = useRef<MapView>(null);

  useEffect(() => {
    const coordinates = route.length > 1
      ? route
      : [currentLocation, pickup, destination].filter(
          (coordinate): coordinate is GeoCoordinate => coordinate !== null,
        );
    if (coordinates.length < 2) return;
    mapRef.current?.fitToCoordinates(coordinates, {
      animated: true,
      edgePadding: { top: 42, right: 42, bottom: 42, left: 42 },
    });
  }, [currentLocation, destination, pickup, route]);

  return (
    <MapView
      ref={mapRef}
      initialRegion={{
        ...pickup,
        latitudeDelta: 0.025,
        longitudeDelta: 0.025,
      }}
      provider={PROVIDER_GOOGLE}
      showsMyLocationButton={false}
      showsUserLocation
      style={{ flex: 1 }}
    >
      {currentLocation ? (
        <Marker coordinate={currentLocation} pinColor={colors.navy} title="Your location" />
      ) : null}
      <Marker coordinate={pickup} pinColor={colors.blue} title="Passenger pickup" />
      <Marker coordinate={destination} pinColor="#D83A3A" title="Passenger destination" />
      {route.length > 1 ? (
        <Polyline coordinates={route} strokeColor={colors.blue} strokeWidth={5} />
      ) : null}
    </MapView>
  );
}
