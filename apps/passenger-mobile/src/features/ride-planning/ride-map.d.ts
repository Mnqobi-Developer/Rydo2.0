import type { GeoCoordinate } from '@rydo/mobile-api-client';
import type { ForwardRefExoticComponent, RefAttributes } from 'react';

export interface RideMapHandle {
  fitRoute(coordinates: GeoCoordinate[]): void;
}

interface RideMapProps {
  pickup: GeoCoordinate | null;
  destination: GeoCoordinate | null;
  route: GeoCoordinate[];
  onMapPress(coordinate: GeoCoordinate): void;
}

export const RideMap: ForwardRefExoticComponent<RideMapProps & RefAttributes<RideMapHandle>>;
