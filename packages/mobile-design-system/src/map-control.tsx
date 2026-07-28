import type { ComponentProps } from 'react';
import { Pressable } from 'react-native';

import { RydoIcon, type RydoIconName } from './icon';
import { colors, radii, shadows } from './tokens';

export interface MapControlProps extends Omit<ComponentProps<typeof Pressable>, 'children' | 'style'> {
  icon: RydoIconName;
  label: string;
  selected?: boolean;
}

export function MapControl({ icon, label, selected = false, ...props }: MapControlProps) {
  return (
    <Pressable
      accessibilityLabel={label}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      hitSlop={8}
      style={({ pressed }) => ({
        width: 48,
        height: 48,
        borderCurve: 'continuous',
        borderRadius: radii.md,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: selected ? colors.blue : colors.glass,
        opacity: pressed ? 0.78 : 1,
        ...shadows.control,
      })}
      {...props}
    >
      <RydoIcon name={icon} color={selected ? colors.white : colors.blue} />
    </Pressable>
  );
}
