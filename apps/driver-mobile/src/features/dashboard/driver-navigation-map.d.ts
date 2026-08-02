import type { GeoCoordinate } from '@rydo/mobile-api-client';

export function DriverNavigationMap(props: {
  currentLocation: GeoCoordinate | null;
  destination: GeoCoordinate;
  pickup: GeoCoordinate;
  route: GeoCoordinate[];
}): React.JSX.Element;
