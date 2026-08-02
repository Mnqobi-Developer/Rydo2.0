import Feather from '@expo/vector-icons/Feather';
import type { ComponentProps } from 'react';
import type { ColorValue, StyleProp, TextStyle } from 'react-native';

import { colors } from './tokens';

export type RydoIconName =
  | 'bell'
  | 'bookmark'
  | 'camera'
  | 'car'
  | 'card'
  | 'check'
  | 'chevron-left'
  | 'chevron-right'
  | 'clock'
  | 'empty'
  | 'error'
  | 'help'
  | 'home'
  | 'earnings'
  | 'menu'
  | 'location'
  | 'logout'
  | 'map-pin'
  | 'person'
  | 'phone'
  | 'power'
  | 'refresh'
  | 'settings'
  | 'shield'
  | 'star'
  | 'tag'
  | 'upload';

type FeatherName = ComponentProps<typeof Feather>['name'];

const featherNames: Record<RydoIconName, FeatherName> = {
  bell: 'bell',
  bookmark: 'bookmark',
  camera: 'camera',
  car: 'truck',
  card: 'credit-card',
  check: 'check',
  'chevron-left': 'chevron-left',
  'chevron-right': 'chevron-right',
  clock: 'clock',
  empty: 'inbox',
  error: 'alert-triangle',
  help: 'headphones',
  home: 'home',
  earnings: 'bar-chart-2',
  menu: 'menu',
  location: 'navigation',
  logout: 'log-out',
  'map-pin': 'map-pin',
  person: 'user',
  phone: 'phone',
  power: 'power',
  refresh: 'refresh-cw',
  settings: 'settings',
  shield: 'shield',
  star: 'star',
  tag: 'tag',
  upload: 'upload',
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
