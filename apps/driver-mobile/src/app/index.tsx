import { ErrorState, LoadingState, RydoButton, colors, spacing } from '@rydo/mobile-design-system';
import { StyleSheet, Text, View } from 'react-native';

import { useAuthSession } from '@/auth/session';
import { DriverSignInScreen } from '@/features/auth/driver-sign-in-screen';
import { DriverDashboardScreen } from '@/features/dashboard/driver-dashboard-screen';

export default function DriverHomeScreen() {
  const session = useAuthSession();

  if (session.status === 'restoring') return <LoadingState label="Restoring your secure driver session…" />;
  if (session.status === 'unavailable') {
    return <ErrorState title="Unable to restore your session" message={session.error?.message} onRetry={() => void session.retryRestore()} />;
  }
  if (session.status !== 'authenticated') return <DriverSignInScreen />;
  if (session.user?.role !== 'Driver') {
    return (
      <View style={styles.roleMismatch}>
        <Text style={styles.roleTitle}>Driver account required</Text>
        <Text style={styles.roleMessage}>This app is reserved for approved RYDO driver accounts.</Text>
        <RydoButton label="Sign out" fullWidth={false} variant="secondary" onPress={() => void session.logout()} />
      </View>
    );
  }

  return <DriverDashboardScreen />;
}

const styles = StyleSheet.create({
  roleMismatch: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.md, padding: spacing.xxl, backgroundColor: colors.surface },
  roleTitle: { color: colors.navy, fontSize: 23, fontWeight: '900', textAlign: 'center' },
  roleMessage: { maxWidth: 300, color: colors.textMuted, lineHeight: 21, textAlign: 'center' },
});
