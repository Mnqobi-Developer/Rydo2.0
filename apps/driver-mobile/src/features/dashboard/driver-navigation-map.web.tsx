import type { GeoCoordinate } from '@rydo/mobile-api-client';
import { RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { Text, View } from 'react-native';

export function DriverNavigationMap(_: {
  currentLocation: GeoCoordinate | null;
  destination: GeoCoordinate;
  pickup: GeoCoordinate;
  route: GeoCoordinate[];
}) {
  return (
    <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: colors.blueMuted }}>
      <RydoIcon name="location" color={colors.blue} size={28} />
      <Text selectable style={{ color: colors.navy, fontWeight: '800' }}>Live navigation map</Text>
      <Text selectable style={{ color: colors.textMuted, fontSize: 12 }}>Open the Driver app on Android or iOS.</Text>
    </View>
  );
}
