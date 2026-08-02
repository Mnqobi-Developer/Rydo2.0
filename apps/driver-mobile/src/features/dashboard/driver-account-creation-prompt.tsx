import { RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { driverTheme } from '@/theme/driver-theme';

export function DriverAccountCreationPrompt({
  visible,
  onCreateProfile,
  onLater,
}: {
  visible: boolean;
  onCreateProfile(): void;
  onLater(): void;
}) {
  const insets = useSafeAreaInsets();

  return (
    <Modal
      animationType="fade"
      onRequestClose={onLater}
      statusBarTranslucent
      transparent
      visible={visible}
    >
      <View style={styles.backdrop}>
        <Pressable accessibilityLabel="Close profile setup notification" onPress={onLater} style={StyleSheet.absoluteFill} />
        <Animated.View entering={FadeInDown.duration(220)} style={[styles.card, { marginBottom: insets.bottom + spacing.lg }]}>
          <View style={styles.iconWrap}>
            <RydoIcon name="person" color={colors.white} size={28} />
            <View style={styles.badge}><RydoIcon name="check" color={colors.blue} size={10} /></View>
          </View>

          <View style={styles.copy}>
            <Text selectable style={styles.eyebrow}>WELCOME TO RYDO DRIVER</Text>
            <Text selectable style={styles.title}>Finish creating your account</Text>
            <Text selectable style={styles.message}>Add your personal details before completing vehicle and document verification.</Text>
          </View>

          <Pressable
            accessibilityRole="button"
            onPress={onCreateProfile}
            style={({ pressed }) => [styles.primaryButton, pressed && styles.primaryPressed]}
          >
            <Text style={styles.primaryLabel}>Create Driver profile</Text>
            <RydoIcon name="chevron-right" color={colors.white} size={21} />
          </Pressable>

          <Pressable accessibilityRole="button" onPress={onLater} style={({ pressed }) => [styles.laterButton, pressed && styles.laterPressed]}>
            <Text style={styles.laterLabel}>I&apos;ll do this later</Text>
          </Pressable>
        </Animated.View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, justifyContent: 'flex-end', paddingHorizontal: spacing.lg, backgroundColor: 'rgba(11,31,58,0.54)' },
  card: { gap: spacing.lg, padding: spacing.xl, borderCurve: 'continuous', borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: '0 16px 40px rgba(11,31,58,0.22)' },
  iconWrap: { width: 60, height: 60, alignItems: 'center', justifyContent: 'center', borderRadius: 20, backgroundColor: colors.blue },
  badge: { position: 'absolute', right: -4, bottom: -4, width: 24, height: 24, alignItems: 'center', justifyContent: 'center', borderWidth: 3, borderColor: colors.white, borderRadius: 12, backgroundColor: colors.blueMuted },
  copy: { gap: spacing.sm },
  eyebrow: { color: colors.blue, fontSize: 10, fontWeight: '900', letterSpacing: 1 },
  title: { maxWidth: 310, color: colors.navy, fontSize: 26, lineHeight: 32, fontWeight: '900', letterSpacing: -0.6 },
  message: { maxWidth: 330, color: colors.textMuted, fontSize: 14, lineHeight: 21 },
  primaryButton: { minHeight: 58, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, borderRadius: driverTheme.radii.button, backgroundColor: colors.blue },
  primaryPressed: { transform: [{ scale: 0.985 }], backgroundColor: colors.bluePressed },
  primaryLabel: { color: colors.white, fontSize: 16, fontWeight: '900' },
  laterButton: { minHeight: 44, alignItems: 'center', justifyContent: 'center' },
  laterPressed: { opacity: 0.62 },
  laterLabel: { color: colors.textMuted, fontSize: 13, fontWeight: '800' },
});
