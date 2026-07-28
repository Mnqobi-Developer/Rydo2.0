import { Image } from 'expo-image';
import type { ColorValue, ImageStyle, StyleProp } from 'react-native';
import { Text } from 'react-native';

import { colors } from './tokens';

export type RydoIconName =
  | 'bookmark'
  | 'car'
  | 'chevron-right'
  | 'clock'
  | 'empty'
  | 'error'
  | 'home'
  | 'location'
  | 'map-pin'
  | 'person'
  | 'refresh';

const symbols: Record<RydoIconName, string> = {
  bookmark: 'bookmark.fill',
  car: 'car.fill',
  'chevron-right': 'chevron.right',
  clock: 'clock.fill',
  empty: 'tray',
  error: 'exclamationmark.triangle.fill',
  home: 'house.fill',
  location: 'location.fill',
  'map-pin': 'mappin.and.ellipse',
  person: 'person.crop.circle.fill',
  refresh: 'arrow.clockwise',
};

const fallbacks: Record<RydoIconName, string> = {
  bookmark: '◆',
  car: '●',
  'chevron-right': '›',
  clock: '◷',
  empty: '□',
  error: '!',
  home: '⌂',
  location: '◎',
  'map-pin': '●',
  person: '●',
  refresh: '↻',
};

export interface RydoIconProps {
  name: RydoIconName;
  size?: number;
  color?: ColorValue;
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
