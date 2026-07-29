import Feather from '@expo/vector-icons/Feather';
import type { ComponentProps } from 'react';
import type { ColorValue, StyleProp, TextStyle } from 'react-native';

import { colors } from './tokens';

export type RydoIconName =
  | 'bookmark'
  | 'camera'
  | 'car'
  | 'card'
  | 'check'
  | 'chevron-right'
  | 'clock'
  | 'empty'
  | 'error'
  | 'help'
  | 'home'
  | 'location'
  | 'logout'
  | 'map-pin'
  | 'person'
  | 'phone'
  | 'refresh'
  | 'settings'
  | 'shield'
  | 'star'
  | 'tag';

type FeatherName = ComponentProps<typeof Feather>['name'];

const featherNames: Record<RydoIconName, FeatherName> = {
  bookmark: 'bookmark',
  camera: 'camera',
  car: 'truck',
  card: 'credit-card',
  check: 'check',
  'chevron-right': 'chevron-right',
  clock: 'clock',
  empty: 'inbox',
  error: 'alert-triangle',
  help: 'headphones',
  home: 'home',
  location: 'navigation',
  logout: 'log-out',
  'map-pin': 'map-pin',
  person: 'user',
  phone: 'phone',
  refresh: 'refresh-cw',
  settings: 'settings',
  shield: 'shield',
  star: 'star',
  tag: 'tag',
};

export interface RydoIconProps {
  name: RydoIconName;
  size?: number;
  color?: ColorValue;
  style?: StyleProp<TextStyle>;
}

export function RydoIcon({ name, size = 22, color = colors.blue, style }: RydoIconProps) {
  return <Feather accessibilityElementsHidden color={color as string} name={featherNames[name]} size={size} style={style} />;
}
