import { Image } from 'expo-image';
import type { ImageStyle, StyleProp } from 'react-native';
import { Text } from 'react-native';

import { colors } from './tokens';

export type RydoIconName =
  | 'car'
  | 'chevron-right'
  | 'empty'
  | 'error'
  | 'location'
  | 'map-pin'
  | 'refresh';

const symbols: Record<RydoIconName, string> = {
  car: 'car.fill',
  'chevron-right': 'chevron.right',
  empty: 'tray',
  error: 'exclamationmark.triangle.fill',
  location: 'location.fill',
  'map-pin': 'mappin.and.ellipse',
  refresh: 'arrow.clockwise',
};

const fallbacks: Record<RydoIconName, string> = {
  car: '●',
  'chevron-right': '›',
  empty: '□',
  error: '!',
  location: '◎',
  'map-pin': '●',
  refresh: '↻',
};

export interface RydoIconProps {
  name: RydoIconName;
  size?: number;
  color?: string;
  style?: StyleProp<ImageStyle>;
}

export function RydoIcon({ name, size = 22, color = colors.blue, style }: RydoIconProps) {
  if (process.env.EXPO_OS === 'ios') {
    return (
      <Image
        accessibilityIgnoresInvertColors
        contentFit="contain"
        source={`sf:${symbols[name]}`}
        style={[{ width: size, height: size, tintColor: color }, style]}
      />
    );
  }

  return (
    <Text aria-hidden style={{ color, fontSize: size, fontWeight: '800', lineHeight: size }}>
      {fallbacks[name]}
    </Text>
  );
}
