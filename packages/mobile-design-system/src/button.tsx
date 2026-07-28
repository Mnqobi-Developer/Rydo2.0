import type { ComponentProps, ReactNode } from 'react';
import { ActivityIndicator, Pressable, Text } from 'react-native';
import Animated, { useAnimatedStyle, useReducedMotion, useSharedValue, withTiming } from 'react-native-reanimated';

import { colors, radii, spacing, typography } from './tokens';

type PressableProps = ComponentProps<typeof Pressable>;
type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';

export interface RydoButtonProps extends Omit<PressableProps, 'children' | 'style'> {
  label: string;
  variant?: ButtonVariant;
  loading?: boolean;
  leading?: ReactNode;
  fullWidth?: boolean;
}

const AnimatedPressable = Animated.createAnimatedComponent(Pressable);

export function RydoButton({
  label,
  variant = 'primary',
  loading = false,
  leading,
  fullWidth = true,
  disabled,
  onPressIn,
  onPressOut,
  ...props
}: RydoButtonProps) {
  const scale = useSharedValue(1);
  const reduceMotion = useReducedMotion();
  const animatedStyle = useAnimatedStyle(() => ({ transform: [{ scale: scale.value }] }));
  const palette = buttonPalette[variant];
  const isDisabled = disabled || loading;

  return (
    <AnimatedPressable
      accessibilityRole="button"
      accessibilityState={{ disabled: isDisabled, busy: loading }}
      disabled={isDisabled}
      onPressIn={(event) => {
        scale.value = reduceMotion ? 1 : withTiming(0.98, { duration: 90 });
        onPressIn?.(event);
      }}
      onPressOut={(event) => {
        scale.value = withTiming(1, { duration: 120 });
        onPressOut?.(event);
      }}
      style={[
        {
          minHeight: 52,
          width: fullWidth ? '100%' : undefined,
          borderCurve: 'continuous',
          borderRadius: radii.lg,
          borderWidth: palette.borderWidth,
          borderColor: palette.borderColor,
          backgroundColor: palette.background,
          paddingHorizontal: spacing.lg,
          paddingVertical: spacing.md,
          alignItems: 'center',
          justifyContent: 'center',
          flexDirection: 'row',
          gap: spacing.sm,
          opacity: isDisabled ? 0.55 : 1,
        },
        animatedStyle,
      ]}
      {...props}
    >
      {loading ? <ActivityIndicator color={palette.foreground} /> : leading}
      <Text
        selectable
        style={{ color: palette.foreground, fontSize: typography.size.button, fontWeight: typography.weight.bold }}
      >
        {label}
      </Text>
    </AnimatedPressable>
  );
}

const buttonPalette: Record<ButtonVariant, { background: string; foreground: string; borderColor: string; borderWidth: number }> = {
  primary: { background: colors.blue, foreground: colors.white, borderColor: colors.blue, borderWidth: 1 },
  secondary: { background: colors.white, foreground: colors.blue, borderColor: colors.blue, borderWidth: 1 },
  danger: { background: colors.danger, foreground: colors.white, borderColor: colors.danger, borderWidth: 1 },
  ghost: { background: 'transparent', foreground: colors.blue, borderColor: 'transparent', borderWidth: 0 },
};
