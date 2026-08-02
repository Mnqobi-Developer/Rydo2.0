import type { Trip } from '@rydo/mobile-api-client';
import { ErrorState, LoadingState, RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import { Image } from 'expo-image';
import { useMemo } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import Animated, { FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DriverRideIcon } from '@/components/driver-ride-icon';
import { driverTheme } from '@/theme/driver-theme';

import { driverTripsQuery } from './api';

export function DriverEarningsScreen() {
  const insets = useSafeAreaInsets();
  const trips = useQuery(driverTripsQuery);
  const summary = useMemo(() => createEarningsSummary(trips.data ?? []), [trips.data]);

  if (trips.isLoading) return <LoadingState label="Loading your earnings…" />;
  if (trips.isError) {
    return <ErrorState title="Earnings unavailable" message={trips.error.message} onRetry={() => void trips.refetch()} />;
  }

  return (
    <ScrollView
      contentContainerStyle={[
        styles.content,
        { paddingTop: insets.top + spacing.xl, paddingBottom: 116 + insets.bottom },
      ]}
      contentInsetAdjustmentBehavior="automatic"
      refreshControl={<RefreshControl refreshing={trips.isRefetching} tintColor={colors.blue} onRefresh={() => void trips.refetch()} />}
      showsVerticalScrollIndicator={false}
    >
      <View style={styles.header}>
        <Text selectable style={styles.title}>Earnings</Text>
        <Text selectable style={styles.subtitle}>Gross fares from your completed RYDO trips.</Text>
      </View>

      <Animated.View entering={FadeInUp.duration(220)} style={styles.balanceCard}>
        <View style={styles.balanceHeading}>
          <View style={styles.iconCircle}>
            <Image
              accessibilityIgnoresInvertColors
              contentFit="contain"
              source={require('../../../assets/icons/navigation/earnings.png')}
              style={styles.earningsIcon}
            />
          </View>
          <Text style={styles.eyebrow}>THIS MONTH</Text>
        </View>
        <Text selectable style={styles.balance}>{formatCurrency(summary.month)}</Text>
        <Text selectable style={styles.balanceMeta}>{summary.monthTrips} completed {summary.monthTrips === 1 ? 'trip' : 'trips'}</Text>
      </Animated.View>

      <View style={styles.metricsRow}>
        <MetricCard label="Today" value={formatCurrency(summary.today)} detail={`${summary.todayTrips} trips`} />
        <MetricCard label="Last 7 days" value={formatCurrency(summary.week)} detail={`${summary.weekTrips} trips`} />
      </View>

      <View style={styles.sectionHeader}>
        <Text style={styles.sectionTitle}>Recent earnings</Text>
        <Text selectable style={styles.sectionMeta}>{formatCurrency(summary.total)} lifetime</Text>
      </View>

      {summary.recent.length === 0 ? (
        <View style={styles.emptyCard}>
          <View style={styles.emptyIcon}><DriverRideIcon color={colors.blue} size={25} /></View>
          <Text style={styles.emptyTitle}>No earnings yet</Text>
          <Text selectable style={styles.emptyText}>Completed trips and their gross fares will appear here.</Text>
        </View>
      ) : (
        <View style={styles.listCard}>
          {summary.recent.map((trip, index) => (
            <View key={trip.id} style={[styles.earningRow, index < summary.recent.length - 1 && styles.rowDivider]}>
              <View style={styles.rowIcon}><RydoIcon name="check" color={driverTheme.colors.online} size={17} /></View>
              <View style={styles.rowCopy}>
                <Text numberOfLines={1} selectable style={styles.destination}>{trip.destinationAddress}</Text>
                <Text selectable style={styles.tripMeta}>{formatTripDate(trip)} · {formatCategory(trip.rideCategory)}</Text>
              </View>
              <Text selectable style={styles.amount}>{formatCurrency(tripAmount(trip))}</Text>
            </View>
          ))}
        </View>
      )}

      <View style={styles.disclaimer}>
        <RydoIcon name="help" color={colors.textMuted} size={18} />
        <Text selectable style={styles.disclaimerText}>
          These figures are gross trip fares. Settled payouts and deductions will appear when Driver payouts are enabled.
        </Text>
      </View>
    </ScrollView>
  );
}

function MetricCard({ label, value, detail }: { label: string; value: string; detail: string }) {
  return (
    <View style={styles.metricCard}>
      <Text style={styles.metricLabel}>{label}</Text>
      <Text selectable style={styles.metricValue}>{value}</Text>
      <Text selectable style={styles.metricDetail}>{detail}</Text>
    </View>
  );
}

function createEarningsSummary(trips: Trip[]) {
  const now = new Date();
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const weekStart = todayStart - 6 * 24 * 60 * 60 * 1000;
  const monthStart = new Date(now.getFullYear(), now.getMonth(), 1).getTime();
  const completed = trips
    .filter((trip) => trip.status === 'Completed')
    .sort((left, right) => tripTime(right) - tripTime(left));
  const totals = (start: number) => completed.filter((trip) => tripTime(trip) >= start);
  const today = totals(todayStart);
  const week = totals(weekStart);
  const month = totals(monthStart);

  return {
    today: sumTrips(today),
    todayTrips: today.length,
    week: sumTrips(week),
    weekTrips: week.length,
    month: sumTrips(month),
    monthTrips: month.length,
    total: sumTrips(completed),
    recent: completed.slice(0, 8),
  };
}

function sumTrips(trips: Trip[]) {
  return trips.reduce((total, trip) => total + tripAmount(trip), 0);
}

function tripAmount(trip: Trip) {
  return trip.finalFareAmount ?? trip.estimatedFareAmount ?? 0;
}

function tripTime(trip: Trip) {
  return new Date(trip.completedAt ?? trip.updatedAt).getTime();
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', minimumFractionDigits: 2 }).format(value);
}

function formatTripDate(trip: Trip) {
  return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
    .format(new Date(trip.completedAt ?? trip.updatedAt));
}

function formatCategory(category: Trip['rideCategory']) {
  if (category === 'GroupPlus') return 'Group+';
  return category ?? 'Ride';
}

const styles = StyleSheet.create({
  content: { gap: spacing.xl, paddingHorizontal: spacing.xl, backgroundColor: driverTheme.colors.background },
  header: { gap: spacing.xs },
  title: { color: colors.navy, fontSize: 34, lineHeight: 41, fontWeight: '900', letterSpacing: -1 },
  subtitle: { color: colors.textMuted, fontSize: 14, lineHeight: 20 },
  balanceCard: { gap: spacing.sm, padding: spacing.xl, borderRadius: driverTheme.radii.card, backgroundColor: colors.navy, boxShadow: driverTheme.shadows.card },
  balanceHeading: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  iconCircle: { width: 38, height: 38, alignItems: 'center', justifyContent: 'center', borderRadius: 19, backgroundColor: 'rgba(255,255,255,0.12)' },
  earningsIcon: { width: 20, height: 20, tintColor: colors.white },
  eyebrow: { color: '#B9C7DA', fontSize: 11, fontWeight: '900', letterSpacing: 0.8 },
  balance: { color: colors.white, fontSize: 36, lineHeight: 43, fontWeight: '900', fontVariant: ['tabular-nums'] },
  balanceMeta: { color: '#B9C7DA', fontSize: 13, fontWeight: '600' },
  metricsRow: { flexDirection: 'row', gap: spacing.md },
  metricCard: { minWidth: 0, flex: 1, gap: 4, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  metricLabel: { color: colors.textMuted, fontSize: 11, fontWeight: '800' },
  metricValue: { color: colors.navy, fontSize: 20, lineHeight: 26, fontWeight: '900', fontVariant: ['tabular-nums'] },
  metricDetail: { color: colors.textMuted, fontSize: 11 },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  sectionTitle: { color: colors.navy, fontSize: 18, fontWeight: '900' },
  sectionMeta: { color: colors.textMuted, fontSize: 12, fontWeight: '700' },
  listCard: { overflow: 'hidden', borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  earningRow: { minHeight: 82, flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  rowDivider: { borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: driverTheme.colors.softBorder },
  rowIcon: { width: 34, height: 34, alignItems: 'center', justifyContent: 'center', borderRadius: 17, backgroundColor: driverTheme.colors.onlineSoft },
  rowCopy: { minWidth: 0, flex: 1, gap: 3 },
  destination: { color: colors.navy, fontSize: 14, fontWeight: '800' },
  tripMeta: { color: colors.textMuted, fontSize: 11 },
  amount: { color: driverTheme.colors.online, fontSize: 14, fontWeight: '900', fontVariant: ['tabular-nums'] },
  emptyCard: { alignItems: 'center', gap: spacing.sm, padding: spacing.xxl, borderRadius: driverTheme.radii.card, backgroundColor: colors.white },
  emptyIcon: { width: 58, height: 58, alignItems: 'center', justifyContent: 'center', borderRadius: 29, backgroundColor: colors.blueMuted },
  emptyTitle: { paddingTop: spacing.xs, color: colors.navy, fontSize: 18, fontWeight: '900' },
  emptyText: { maxWidth: 260, color: colors.textMuted, fontSize: 13, lineHeight: 19, textAlign: 'center' },
  disclaimer: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.sm, padding: spacing.lg, borderRadius: driverTheme.radii.control, backgroundColor: driverTheme.colors.softControl },
  disclaimerText: { minWidth: 0, flex: 1, color: colors.textMuted, fontSize: 12, lineHeight: 18 },
});
