import type { Trip } from '@rydo/mobile-api-client';
import { ErrorState, LoadingState, RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import Animated, { FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DriverRideIcon } from '@/components/driver-ride-icon';
import { driverTheme } from '@/theme/driver-theme';

import { driverTripsQuery } from './api';

export function DriverWalletScreen() {
  const insets = useSafeAreaInsets();
  const trips = useQuery(driverTripsQuery);
  const wallet = useMemo(() => createWalletSummary(trips.data ?? []), [trips.data]);

  if (trips.isLoading) return <LoadingState label="Loading your wallet…" />;
  if (trips.isError) return <ErrorState title="Wallet unavailable" message={trips.error.message} onRetry={() => void trips.refetch()} />;

  return (
    <ScrollView
      contentContainerStyle={[styles.content, { paddingTop: insets.top + spacing.xl, paddingBottom: 116 + insets.bottom }]}
      contentInsetAdjustmentBehavior="automatic"
      refreshControl={<RefreshControl refreshing={trips.isRefetching} tintColor={colors.blue} onRefresh={() => void trips.refetch()} />}
      showsVerticalScrollIndicator={false}
    >
      <View style={styles.header}>
        <Text selectable style={styles.title}>Wallet</Text>
        <Text selectable style={styles.subtitle}>Your Driver money overview and trip activity.</Text>
      </View>

      <Animated.View entering={FadeInUp.duration(220)} style={styles.walletCard}>
        <View style={styles.walletTopRow}>
          <View style={styles.walletIcon}><RydoIcon name="card" color={colors.white} size={23} /></View>
          <View style={styles.statusPill}><View style={styles.statusDot} /><Text style={styles.statusText}>Gross fares</Text></View>
        </View>
        <Text style={styles.balanceLabel}>Completed trip value</Text>
        <Text selectable style={styles.balance}>{formatCurrency(wallet.grossTotal)}</Text>
        <View style={styles.walletDivider} />
        <View style={styles.walletFooter}>
          <WalletMetric label="THIS MONTH" value={formatCurrency(wallet.monthTotal)} />
          <View style={styles.metricDivider} />
          <WalletMetric label="COMPLETED" value={String(wallet.completedCount)} />
        </View>
      </Animated.View>

      <View style={styles.payoutCard}>
        <View style={styles.payoutIcon}><RydoIcon name="clock" color={colors.blue} size={21} /></View>
        <View style={styles.payoutCopy}>
          <Text style={styles.payoutTitle}>Driver payouts are coming</Text>
          <Text selectable style={styles.payoutText}>Settled balance, deductions, and bank transfers will appear here after the payout service is enabled.</Text>
        </View>
      </View>

      <View style={styles.sectionHeader}>
        <Text style={styles.sectionTitle}>Wallet activity</Text>
        <Text selectable style={styles.sectionMeta}>{wallet.completedCount} completed</Text>
      </View>

      {wallet.recent.length === 0 ? (
        <View style={styles.emptyCard}>
          <View style={styles.emptyIcon}><RydoIcon name="card" color={colors.blue} size={26} /></View>
          <Text style={styles.emptyTitle}>No wallet activity</Text>
          <Text selectable style={styles.emptyText}>Completed trip fares will appear here.</Text>
        </View>
      ) : (
        <View style={styles.activityCard}>
          {wallet.recent.map((trip, index) => (
            <View key={trip.id} style={[styles.activityRow, index < wallet.recent.length - 1 && styles.rowDivider]}>
              <View style={styles.activityIcon}><DriverRideIcon color={colors.blue} size={17} /></View>
              <View style={styles.activityCopy}>
                <Text numberOfLines={1} selectable style={styles.activityTitle}>{trip.destinationAddress}</Text>
                <Text selectable style={styles.activityDate}>{formatTripDate(trip)}</Text>
              </View>
              <View style={styles.activityAmountWrap}>
                <Text selectable style={styles.activityAmount}>+{formatCurrency(tripAmount(trip))}</Text>
                <Text style={styles.activityStatus}>Gross</Text>
              </View>
            </View>
          ))}
        </View>
      )}
    </ScrollView>
  );
}

function WalletMetric({ label, value }: { label: string; value: string }) {
  return <View style={styles.walletMetric}><Text style={styles.metricLabel}>{label}</Text><Text selectable style={styles.metricValue}>{value}</Text></View>;
}

function createWalletSummary(trips: Trip[]) {
  const now = new Date();
  const monthStart = new Date(now.getFullYear(), now.getMonth(), 1).getTime();
  const completed = trips.filter((trip) => trip.status === 'Completed').sort((a, b) => tripTime(b) - tripTime(a));
  return {
    grossTotal: sum(completed),
    monthTotal: sum(completed.filter((trip) => tripTime(trip) >= monthStart)),
    completedCount: completed.length,
    recent: completed.slice(0, 10),
  };
}

function sum(trips: Trip[]) { return trips.reduce((total, trip) => total + tripAmount(trip), 0); }
function tripAmount(trip: Trip) { return trip.finalFareAmount ?? trip.estimatedFareAmount ?? 0; }
function tripTime(trip: Trip) { return new Date(trip.completedAt ?? trip.updatedAt).getTime(); }
function formatCurrency(value: number) { return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', minimumFractionDigits: 2 }).format(value); }
function formatTripDate(trip: Trip) { return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(new Date(trip.completedAt ?? trip.updatedAt)); }

const styles = StyleSheet.create({
  content: { gap: spacing.xl, paddingHorizontal: spacing.xl, backgroundColor: driverTheme.colors.background },
  header: { gap: spacing.xs },
  title: { color: colors.navy, fontSize: 34, lineHeight: 41, fontWeight: '900', letterSpacing: -1 },
  subtitle: { color: colors.textMuted, fontSize: 14, lineHeight: 20 },
  walletCard: { gap: spacing.md, padding: spacing.xl, borderRadius: driverTheme.radii.card, backgroundColor: colors.blue, boxShadow: '0 12px 28px rgba(36,87,255,0.18)' },
  walletTopRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  walletIcon: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderRadius: 16, backgroundColor: 'rgba(255,255,255,0.14)' },
  statusPill: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 11, paddingVertical: 7, borderRadius: 999, backgroundColor: 'rgba(255,255,255,0.12)' },
  statusDot: { width: 7, height: 7, borderRadius: 4, backgroundColor: '#8FF0BE' },
  statusText: { color: colors.white, fontSize: 11, fontWeight: '800' },
  balanceLabel: { color: '#D6E4FA', fontSize: 12, fontWeight: '700' },
  balance: { color: colors.white, fontSize: 36, lineHeight: 43, fontWeight: '900', fontVariant: ['tabular-nums'] },
  walletDivider: { height: 1, backgroundColor: 'rgba(255,255,255,0.16)' },
  walletFooter: { minHeight: 50, flexDirection: 'row', alignItems: 'stretch' },
  walletMetric: { flex: 1, justifyContent: 'center', gap: 3 },
  metricDivider: { width: 1, backgroundColor: 'rgba(255,255,255,0.16)' },
  metricLabel: { color: '#D6E4FA', fontSize: 9, fontWeight: '900', letterSpacing: 0.7 },
  metricValue: { color: colors.white, fontSize: 16, fontWeight: '900', fontVariant: ['tabular-nums'] },
  payoutCard: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: colors.blueMuted },
  payoutIcon: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center', borderRadius: 15, backgroundColor: colors.white },
  payoutCopy: { minWidth: 0, flex: 1, gap: 4 },
  payoutTitle: { color: colors.navy, fontSize: 15, fontWeight: '900' },
  payoutText: { color: colors.textMuted, fontSize: 12, lineHeight: 18 },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  sectionTitle: { color: colors.navy, fontSize: 18, fontWeight: '900' },
  sectionMeta: { color: colors.textMuted, fontSize: 12, fontWeight: '700' },
  activityCard: { overflow: 'hidden', borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  activityRow: { minHeight: 82, flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  rowDivider: { borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: driverTheme.colors.softBorder },
  activityIcon: { width: 38, height: 38, alignItems: 'center', justifyContent: 'center', borderRadius: 14, backgroundColor: colors.blueMuted },
  activityCopy: { minWidth: 0, flex: 1, gap: 3 },
  activityTitle: { color: colors.navy, fontSize: 13, fontWeight: '800' },
  activityDate: { color: colors.textMuted, fontSize: 11 },
  activityAmountWrap: { alignItems: 'flex-end', gap: 2 },
  activityAmount: { color: driverTheme.colors.online, fontSize: 13, fontWeight: '900', fontVariant: ['tabular-nums'] },
  activityStatus: { color: colors.textMuted, fontSize: 9, fontWeight: '700' },
  emptyCard: { alignItems: 'center', gap: spacing.sm, padding: spacing.xxl, borderRadius: driverTheme.radii.card, backgroundColor: colors.white },
  emptyIcon: { width: 58, height: 58, alignItems: 'center', justifyContent: 'center', borderRadius: 29, backgroundColor: colors.blueMuted },
  emptyTitle: { paddingTop: spacing.xs, color: colors.navy, fontSize: 18, fontWeight: '900' },
  emptyText: { color: colors.textMuted, fontSize: 13, textAlign: 'center' },
});
