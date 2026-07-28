import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { RydoIcon } from './icon';
import { colors, radii, shadows, spacing, typography } from './tokens';

export interface RideCardProps {
  title: string;
  pickup: string;
  destination: string;
  fare?: string;
  metadata?: string;
  selected?: boolean;
  action?: ReactNode;
}

export function RideCard({ title, pickup, destination, fare, metadata, selected = false, action }: RideCardProps) {
  return (
    <View
      style={{
        borderCurve: 'continuous',
        borderRadius: radii.lg,
        borderWidth: selected ? 2 : 1,
        borderColor: selected ? colors.blue : colors.border,
        backgroundColor: selected ? colors.blueMuted : colors.surfaceElevated,
        padding: spacing.lg,
        gap: spacing.md,
        ...shadows.floating,
      }}
    >
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md }}>
        <View style={{ flex: 1 }}>
          <Text selectable style={{ color: colors.text, fontSize: typography.size.body, fontWeight: typography.weight.bold }}>
            {title}
          </Text>
          {metadata ? <Text selectable style={{ color: colors.textMuted, marginTop: spacing.xs }}>{metadata}</Text> : null}
        </View>
        {fare ? <Text selectable style={{ color: colors.blue, fontSize: typography.size.button, fontWeight: typography.weight.bold }}>{fare}</Text> : null}
      </View>
      <RouteLine icon="location" text={pickup} />
      <RouteLine icon="map-pin" text={destination} />
      {action}
    </View>
  );
}

function RouteLine({ icon, text }: { icon: 'location' | 'map-pin'; text: string }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
      <RydoIcon name={icon} size={18} color={icon === 'location' ? colors.success : colors.blue} />
      <Text selectable numberOfLines={1} style={{ flex: 1, color: colors.text, fontSize: typography.size.body }}>
        {text}
      </Text>
    </View>
  );
}
