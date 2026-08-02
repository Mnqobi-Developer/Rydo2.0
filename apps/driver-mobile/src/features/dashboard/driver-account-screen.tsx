import { ErrorState, LoadingState, RydoIcon, colors, spacing, type RydoIconName } from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import { Alert, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import Animated, { FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useAuthSession } from '@/auth/session';
import { DriverRideIcon } from '@/components/driver-ride-icon';
import { driverTheme } from '@/theme/driver-theme';

import { driverDocumentsQuery, driverProfileQuery, driverVehicleQuery } from './api';

export function DriverAccountScreen({ onOpenDocuments, onOpenVehicle }: { onOpenDocuments(): void; onOpenVehicle(): void }) {
  const insets = useSafeAreaInsets();
  const session = useAuthSession();
  const profile = useQuery(driverProfileQuery);
  const vehicle = useQuery(driverVehicleQuery);
  const documents = useQuery(driverDocumentsQuery);
  const refreshing = profile.isRefetching || vehicle.isRefetching || documents.isRefetching;

  if (profile.isLoading) return <LoadingState label="Loading your Driver account…" />;
  if (profile.isError) return <ErrorState title="Account unavailable" message={profile.error.message} onRetry={() => void profile.refetch()} />;
  if (!profile.data) return <ErrorState title="Driver profile required" message="Complete your Driver profile to manage this account." onRetry={() => void profile.refetch()} />;

  const driver = profile.data;
  const initials = `${driver.firstName[0] ?? ''}${driver.lastName[0] ?? ''}`.toUpperCase() || 'RD';
  const approvedDocuments = (documents.data ?? []).filter((document) => document.reviewStatus === 'Approved').length;
  const complete = driver.onboardingStatus === 'Approved';
  const pendingReview = driver.onboardingStatus === 'PendingReview';

  function refresh() {
    void Promise.all([profile.refetch(), vehicle.refetch(), documents.refetch()]);
  }

  function confirmLogout() {
    Alert.alert('Sign out of RYDO Driver?', 'You will need to verify your mobile number to sign in again.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Sign out', style: 'destructive', onPress: () => void session.logout() },
    ]);
  }

  return (
    <ScrollView
      contentContainerStyle={[styles.content, { paddingTop: insets.top + spacing.xl, paddingBottom: 116 + insets.bottom }]}
      contentInsetAdjustmentBehavior="automatic"
      refreshControl={<RefreshControl refreshing={refreshing} tintColor={colors.blue} onRefresh={refresh} />}
      showsVerticalScrollIndicator={false}
    >
      <Animated.View entering={FadeInUp.duration(220)} style={styles.header}>
        <View style={styles.identity}>
          <Text selectable style={styles.greeting}>Driver account</Text>
          <Text numberOfLines={2} selectable style={styles.name}>{driver.firstName} {driver.lastName}</Text>
          <View style={styles.verificationRow}>
            <View style={[styles.verificationBadge, complete && styles.verificationBadgeComplete]}>
              <RydoIcon name={complete ? 'check' : 'clock'} color={colors.white} size={12} />
            </View>
            <Text selectable style={styles.verificationText}>{formatOnboardingStatus(driver.onboardingStatus)}</Text>
          </View>
        </View>
        <View style={styles.avatar}><Text selectable style={styles.avatarText}>{initials}</Text><View style={styles.avatarDot} /></View>
      </Animated.View>

      <View style={[styles.statusBanner, complete && styles.statusBannerComplete]}>
        <View style={[styles.statusIcon, complete && styles.statusIconComplete]}>
          <RydoIcon name={complete ? 'check' : pendingReview ? 'clock' : 'person'} color={complete ? driverTheme.colors.online : colors.blue} size={20} />
        </View>
        <View style={styles.statusCopy}>
          <Text style={[styles.statusTitle, complete && styles.statusTitleComplete]}>
            {complete ? 'Ready to drive' : pendingReview ? 'Pending approval' : 'Onboarding in progress'}
          </Text>
          <Text selectable style={styles.statusMessage}>
            {complete
              ? 'Your Driver account is approved.'
              : pendingReview
                ? 'Your documents were received and are being reviewed.'
                : 'Complete your profile, vehicle, and required documents.'}
          </Text>
        </View>
      </View>

      <View style={styles.menuCard}>
        <AccountRow icon="person" label="Personal details" value={driver.email ?? session.user?.phoneNumber ?? 'Not provided'} />
        <AccountRow
          icon="ride-asset"
          label="Vehicle"
          onPress={onOpenVehicle}
          value={vehicle.data ? `${vehicle.data.year} ${vehicle.data.make} ${vehicle.data.model}` : vehicle.isError ? 'Unable to load' : 'Not added'}
          status={vehicle.data?.reviewStatus}
        />
        <AccountRow
          icon="bookmark"
          label="Driver documents"
          onPress={onOpenDocuments}
          value={pendingReview ? '3 documents received' : `${approvedDocuments} of 3 approved`}
          status={documents.isError ? 'NeedsAttention' : approvedDocuments === 3 ? 'Approved' : 'PendingReview'}
        />
      </View>

      {driver.rejectionReason ? (
        <View style={styles.alertCard}>
          <RydoIcon name="error" color={colors.danger} size={21} />
          <View style={styles.alertCopy}><Text style={styles.alertTitle}>Profile needs attention</Text><Text selectable style={styles.alertMessage}>{driver.rejectionReason}</Text></View>
        </View>
      ) : null}

      <View style={styles.menuCard}>
        <AccountRow icon="shield" label="Safety" value="Emergency and trip safety" />
        <AccountRow icon="help" label="Support" value="Help with your Driver account" />
        <AccountRow icon="settings" label="Settings" value="App and privacy preferences" />
      </View>

      <Pressable
        accessibilityRole="button"
        onPress={confirmLogout}
        style={({ pressed }) => [styles.logoutButton, pressed && styles.pressed]}
      >
        <RydoIcon name="logout" color={colors.danger} size={20} />
        <Text style={styles.logoutLabel}>Sign out</Text>
      </Pressable>

      <Text selectable style={styles.accountId}>Driver ID · {driver.userId.slice(0, 8).toUpperCase()}</Text>
    </ScrollView>
  );
}

function AccountRow({
  icon,
  label,
  onPress,
  value,
  status,
}: {
  icon: RydoIconName | 'ride-asset';
  label: string;
  onPress?: () => void;
  value: string;
  status?: string;
}) {
  return (
    <Pressable
      accessibilityRole={onPress ? 'button' : undefined}
      disabled={!onPress}
      onPress={onPress}
      style={({ pressed }) => [styles.menuRow, pressed && styles.menuRowPressed]}
    >
      <View style={styles.menuIcon}>
        {icon === 'ride-asset'
          ? <DriverRideIcon color={colors.blue} size={21} />
          : <RydoIcon name={icon} color={colors.blue} size={21} />}
      </View>
      <View style={styles.menuCopy}>
        <Text style={styles.menuLabel}>{label}</Text>
        <Text numberOfLines={1} selectable style={styles.menuValue}>{value}</Text>
      </View>
      <View style={styles.menuTrailing}>
        {status ? <StatusChip status={status} /> : null}
        {onPress ? <RydoIcon name="chevron-right" color={colors.textMuted} size={20} /> : null}
      </View>
    </Pressable>
  );
}

function StatusChip({ status }: { status: string }) {
  const approved = status === 'Approved';
  const rejected = status === 'Rejected' || status === 'NeedsAttention';
  return (
    <View style={[styles.statusChip, approved && styles.statusChipApproved, rejected && styles.statusChipRejected]}>
      <Text style={[styles.statusChipText, approved && styles.statusChipTextApproved, rejected && styles.statusChipTextRejected]}>
        {approved ? 'Approved' : rejected ? 'Review' : 'Pending'}
      </Text>
    </View>
  );
}

function formatOnboardingStatus(status: string) {
  if (status === 'Approved') return 'Verified Driver';
  if (status === 'PendingReview') return 'Verification pending';
  if (status === 'Rejected') return 'Profile needs attention';
  return 'Profile setup in progress';
}

const styles = StyleSheet.create({
  content: { gap: spacing.xl, paddingHorizontal: spacing.xl, backgroundColor: driverTheme.colors.background },
  header: { minHeight: 118, flexDirection: 'row', alignItems: 'center', gap: spacing.lg },
  identity: { minWidth: 0, flex: 1, gap: 3 },
  greeting: { color: colors.textMuted, fontSize: 13, fontWeight: '700' },
  name: { color: colors.navy, fontSize: 30, lineHeight: 36, fontWeight: '900', letterSpacing: -0.8 },
  verificationRow: { flexDirection: 'row', alignItems: 'center', gap: 7, paddingTop: spacing.xs },
  verificationBadge: { width: 20, height: 20, alignItems: 'center', justifyContent: 'center', borderRadius: 10, backgroundColor: colors.amber },
  verificationBadgeComplete: { backgroundColor: colors.blue },
  verificationText: { color: colors.textMuted, fontSize: 12, fontWeight: '700' },
  avatar: { width: 78, height: 78, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: driverTheme.colors.softBorder, borderRadius: 39, backgroundColor: colors.blueMuted },
  avatarText: { color: colors.navy, fontSize: 25, fontWeight: '900' },
  avatarDot: { position: 'absolute', right: 1, bottom: 7, width: 15, height: 15, borderWidth: 3, borderColor: driverTheme.colors.background, borderRadius: 8, backgroundColor: colors.blue },
  statusBanner: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: colors.blueMuted },
  statusBannerComplete: { backgroundColor: driverTheme.colors.onlineSoft },
  statusIcon: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderRadius: 16, backgroundColor: colors.white },
  statusIconComplete: { backgroundColor: 'rgba(255,255,255,0.82)' },
  statusCopy: { minWidth: 0, flex: 1, gap: 3 },
  statusTitle: { color: colors.blue, fontSize: 15, fontWeight: '900' },
  statusTitleComplete: { color: driverTheme.colors.online },
  statusMessage: { color: colors.textMuted, fontSize: 12, lineHeight: 17 },
  menuCard: { overflow: 'hidden', borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  menuRow: { minHeight: 82, flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  menuRowPressed: { opacity: 0.72, backgroundColor: colors.blueMuted },
  menuTrailing: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  menuIcon: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: driverTheme.colors.softBorder, borderRadius: 15, backgroundColor: driverTheme.colors.softControl },
  menuCopy: { minWidth: 0, flex: 1, gap: 3 },
  menuLabel: { color: colors.navy, fontSize: 15, fontWeight: '900' },
  menuValue: { color: colors.textMuted, fontSize: 11 },
  statusChip: { paddingHorizontal: 9, paddingVertical: 6, borderRadius: 999, backgroundColor: '#FFF5DF' },
  statusChipApproved: { backgroundColor: driverTheme.colors.onlineSoft },
  statusChipRejected: { backgroundColor: '#FFF0F1' },
  statusChipText: { color: '#966414', fontSize: 9, fontWeight: '900' },
  statusChipTextApproved: { color: driverTheme.colors.online },
  statusChipTextRejected: { color: colors.danger },
  alertCard: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.control, backgroundColor: '#FFF0F1' },
  alertCopy: { minWidth: 0, flex: 1, gap: 3 },
  alertTitle: { color: colors.danger, fontSize: 14, fontWeight: '900' },
  alertMessage: { color: colors.textMuted, fontSize: 12, lineHeight: 18 },
  logoutButton: { minHeight: 58, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, borderRadius: driverTheme.radii.button, backgroundColor: '#FFF0F1' },
  logoutLabel: { color: colors.danger, fontSize: 15, fontWeight: '900' },
  pressed: { opacity: 0.74, transform: [{ scale: 0.98 }] },
  accountId: { color: colors.textMuted, fontSize: 10, textAlign: 'center' },
});
