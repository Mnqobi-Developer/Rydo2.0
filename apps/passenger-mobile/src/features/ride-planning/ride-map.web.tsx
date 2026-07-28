import type { GeoCoordinate } from '@rydo/mobile-api-client';
import { forwardRef, useImperativeHandle } from 'react';
import { Text, View } from 'react-native';

import { colors } from '@/theme/colors';

export interface RideMapHandle {
  fitRoute(coordinates: GeoCoordinate[]): void;
}

interface RideMapProps {
  onMapPress(coordinate: GeoCoordinate): void;
}

export const RideMap = forwardRef<RideMapHandle, RideMapProps>(function RideMap(_, ref) {
  useImperativeHandle(ref, () => ({ fitRoute() {} }));
  return (
    <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#DDE8E3' }}>
      <Text style={{ color: colors.navy, fontWeight: '700' }}>Native map preview</Text>
      <Text style={{ color: colors.textMuted, marginTop: 6 }}>Open a development build on Android or iOS.</Text>
    </View>
  );
});
