import type { ReactNode } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';
import Animated, { FadeIn } from 'react-native-reanimated';

import { RydoButton } from './button';
import { RydoIcon, type RydoIconName } from './icon';
import { colors, spacing, typography } from './tokens';

interface StateViewProps {
  title: string;
  message?: string;
  icon: RydoIconName;
  iconColor?: string;
  action?: ReactNode;
}

function StateView({ title, message, icon, iconColor = colors.blue, action }: StateViewProps) {
  return (
    <Animated.View entering={FadeIn.duration(180)} style={{ alignItems: 'center', justifyContent: 'center', padding: spacing.xxl, gap: spacing.md }}>
      <RydoIcon name={icon} size={34} color={iconColor} />
      <View style={{ alignItems: 'center', gap: spacing.xs }}>
        <Text selectable style={{ color: colors.text, fontSize: typography.size.button, fontWeight: typography.weight.bold, textAlign: 'center' }}>
          {title}
        </Text>
        {message ? <Text selectable style={{ color: colors.textMuted, lineHeight: typography.lineHeight.body, textAlign: 'center' }}>{message}</Text> : null}
      </View>
      {action}
    </Animated.View>
  );
}

export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <View accessibilityLiveRegion="polite" style={{ alignItems: 'center', justifyContent: 'center', padding: spacing.xxl, gap: spacing.md }}>
      <ActivityIndicator color={colors.blue} size="large" />
      <Text selectable style={{ color: colors.textMuted }}>{label}</Text>
    </View>
  );
}

export function ErrorState({ title = 'Something went wrong', message, onRetry }: { title?: string; message?: string; onRetry?(): void }) {
  return <StateView title={title} message={message} icon="error" iconColor={colors.danger} action={onRetry ? <RydoButton label="Try again" variant="secondary" fullWidth={false} onPress={onRetry} /> : undefined} />;
}

export function EmptyState({ title, message, action }: { title: string; message?: string; action?: ReactNode }) {
  return <StateView title={title} message={message} icon="empty" action={action} />;
}
