import type { GeoCoordinate } from '@rydo/mobile-api-client';
import { Text, View } from 'react-native';

import { colors } from '@/theme/colors';

export function DriverMap(_: { location: GeoCoordinate | null }) {
  return (
    <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#DDE8E3' }}>
      <Text style={{ color: colors.navy, fontWeight: '700' }}>Native driver map</Text>
      <Text style={{ color: colors.textMuted, marginTop: 6 }}>Open a development build on Android or iOS.</Text>
    </View>
  );
}
