import { colors } from '@rydo/mobile-design-system';

export const driverTheme = {
  colors: {
    ...colors,
    background: colors.blueMuted,
    card: colors.white,
    softControl: colors.white,
    softBorder: colors.border,
    online: '#178A55',
    onlineSoft: '#E8F6EF',
  },
  radii: {
    card: 28,
    banner: 24,
    control: 16,
    button: 18,
    pill: 999,
  },
  shadows: {
    card: '0 8px 24px rgba(36,87,255,0.08)',
    control: '0 6px 18px rgba(36,87,255,0.10)',
  },
} as const;
