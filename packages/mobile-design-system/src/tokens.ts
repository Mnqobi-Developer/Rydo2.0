export const colors = {
  blue: '#1261D8',
  bluePressed: '#0D4FAF',
  blueMuted: '#EAF2FF',
  navy: '#0B1F3A',
  navyGlass: 'rgba(11,31,58,0.88)',
  surface: '#F5F7FB',
  surfaceElevated: '#FFFFFF',
  glass: 'rgba(255,255,255,0.94)',
  white: '#FFFFFF',
  text: '#0B1F3A',
  textMuted: '#5D6B7E',
  border: '#DDE5F0',
  danger: '#D83A3A',
  dangerMuted: '#FDECEC',
  success: '#178A55',
  successMuted: '#E6F5EE',
  amber: '#D78A16',
  amberMuted: '#FFF5DF',
  overlay: 'rgba(4,17,34,0.42)',
} as const;

export const typography = {
  family: {
    regular: 'System',
    medium: 'System',
    bold: 'System',
  },
  size: {
    caption: 12,
    body: 15,
    button: 16,
    title: 23,
    display: 32,
  },
  lineHeight: {
    caption: 17,
    body: 21,
    title: 29,
    display: 38,
  },
  weight: {
    regular: '400',
    medium: '600',
    bold: '800',
  },
} as const;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 28,
} as const;

export const radii = {
  sm: 10,
  md: 14,
  lg: 18,
  sheet: 28,
  pill: 999,
} as const;

export const shadows = {
  floating: {
    boxShadow: '0 10px 30px rgba(11,31,58,0.16)',
  },
  control: {
    boxShadow: '0 5px 16px rgba(11,31,58,0.18)',
  },
} as const;
