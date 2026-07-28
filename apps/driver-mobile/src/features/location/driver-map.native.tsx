import type { GeoCoordinate } from '@rydo/mobile-api-client';
import MapView, { Marker, PROVIDER_GOOGLE } from 'react-native-maps';

export function DriverMap({ location }: { location: GeoCoordinate | null }) {
  return (
    <MapView
      provider={PROVIDER_GOOGLE}
      style={{ flex: 1 }}
      region={location ? { ...location, latitudeDelta: 0.02, longitudeDelta: 0.02 } : undefined}
      initialRegion={{ latitude: -26.2041, longitude: 28.0473, latitudeDelta: 0.16, longitudeDelta: 0.16 }}
      showsUserLocation
      showsMyLocationButton={false}
    >
      {location ? <Marker coordinate={location} title="Your location" pinColor="#178A55" /> : null}
    </MapView>
  );
}
