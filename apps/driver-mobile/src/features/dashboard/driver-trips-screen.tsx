import type { Trip } from '@rydo/mobile-api-client';
import { ErrorState, LoadingState, RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import { Image } from 'expo-image';
import { useMemo, useState } from 'react';
import { Pressable, RefreshControl, SectionList, StyleSheet, Text, View } from 'react-native';
import Animated, { FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DriverRideIcon } from '@/components/driver-ride-icon';
import { driverTheme } from '@/theme/driver-theme';

import { driverTripsQuery } from './api';

type TripFilter = 'active' | 'history';
type TripSection = { title: string; data: Trip[] };

const activeStatuses = new Set<Trip['status']>(['Accepted', 'DriverArrived', 'InProgress']);

export function DriverTripsScreen() {
  const insets = useSafeAreaInsets();
  const trips = useQuery(driverTripsQuery);
  const [filter, setFilter] = useState<TripFilter>('active');
  const sections = useMemo(() => groupTrips(
    (trips.data ?? []).filter((trip) => filter === 'active' ? activeStatuses.has(trip.status) : !activeStatuses.has(trip.status)),
  ), [filter, trips.data]);

  if (trips.isLoading) return <LoadingState label="Loading your trips…" />;
  if (trips.isError) return <ErrorState title="Trips unavailable" message={trips.error.message} onRetry={() => void trips.refetch()} />;

  return (
    <SectionList<Trip, TripSection>
      contentContainerStyle={[
        styles.content,
        { paddingTop: insets.top + spacing.xl, paddingBottom: 116 + insets.bottom },
        sections.length === 0 && styles.emptyContent,
      ]}
      contentInsetAdjustmentBehavior="automatic"
      keyExtractor={(trip) => trip.id}
      ListEmptyComponent={<TripsEmpty filter={filter} />}
      ListHeaderComponent={(
        <View style={styles.header}>
          <Text selectable style={styles.title}>Trips</Text>
          <Text selectable style={styles.subtitle}>Track active work and review your driving history.</Text>
          <View accessibilityRole="tablist" style={styles.filterBar}>
            <FilterButton active={filter === 'active'} label="Active" onPress={() => setFilter('active')} />
            <FilterButton active={filter === 'history'} label="History" onPress={() => setFilter('history')} />
          </View>
        </View>
      )}
      refreshControl={<RefreshControl refreshing={trips.isRefetching} tintColor={colors.blue} onRefresh={() => void trips.refetch()} />}
      renderItem={({ item, index }) => (
        <Animated.View entering={FadeInUp.delay(Math.min(index, 6) * 35).duration(220)}>
          <TripRow trip={item} />
        </Animated.View>
      )}
      renderSectionHeader={({ section }) => <Text selectable style={styles.sectionTitle}>{section.title}</Text>}
      sections={sections}
      showsVerticalScrollIndicator={false}
      stickySectionHeadersEnabled={false}
    />
  );
}

function FilterButton({ active, label, onPress }: { active: boolean; label: string; onPress(): void }) {
  return (
    <Pressable
      accessibilityRole="tab"
      accessibilityState={{ selected: active }}
      onPress={onPress}
      style={({ pressed }) => [styles.filterButton, active && styles.filterButtonActive, pressed && styles.pressed]}
    >
      <Text style={[styles.filterLabel, active && styles.filterLabelActive]}>{label}</Text>
    </Pressable>
  );
}

function TripRow({ trip }: { trip: Trip }) {
  const cancelled = trip.status === 'Cancelled';
  const active = activeStatuses.has(trip.status);

  return (
    <View style={styles.tripCard}>
      <View style={styles.tripTopRow}>
        <View style={[styles.rideIconWrap, active && styles.rideIconActive]}>
          <Image
            accessibilityIgnoresInvertColors
            contentFit="contain"
            source={require('../../../assets/icons/navigation/trips.png')}
            style={[styles.rideIcon, { tintColor: active ? colors.blue : colors.textMuted }]}
          />
          {cancelled ? <View style={styles.cancelSlash} /> : null}
        </View>
        <View style={styles.tripHeading}>
          <Text selectable style={styles.tripDate}>{formatTripDate(trip)}</Text>
          <Text selectable style={[styles.status, active && styles.statusActive, cancelled && styles.statusCancelled]}>
            {formatStatus(trip.status)}
          </Text>
        </View>
        <Text selectable style={styles.fare}>{formatFare(trip)}</Text>
      </View>

      <View style={styles.route}>
        <RoutePoint color={driverTheme.colors.online} label={trip.pickupAddress} />
        <View style={styles.routeLine} />
        <RoutePoint color={colors.blue} label={trip.destinationAddress} />
      </View>

      <View style={styles.tripFooter}>
        <Text selectable style={styles.category}>{formatCategory(trip.rideCategory)}</Text>
        <Text selectable style={styles.tripId}>Trip {trip.id.slice(0, 8).toUpperCase()}</Text>
        {active ? <RydoIcon name="chevron-right" color={colors.blue} size={18} /> : null}
      </View>
    </View>
  );
}

function RoutePoint({ color, label }: { color: string; label: string }) {
  return (
    <View style={styles.routePoint}>
      <View style={[styles.routeDot, { backgroundColor: color }]} />
      <Text numberOfLines={2} selectable style={styles.address}>{label}</Text>
    </View>
  );
}

function TripsEmpty({ filter }: { filter: TripFilter }) {
  return (
    <View style={styles.emptyState}>
      <View style={styles.emptyIcon}><DriverRideIcon color={colors.blue} size={28} /></View>
      <Text style={styles.emptyTitle}>{filter === 'active' ? 'No active trips' : 'No trip history yet'}</Text>
      <Text selectable style={styles.emptyMessage}>
        {filter === 'active'
          ? 'Accepted and in-progress trips will appear here.'
          : 'Completed and cancelled trips will appear here.'}
      </Text>
    </View>
  );
}

function groupTrips(trips: Trip[]): TripSection[] {
  const grouped = new Map<string, Trip[]>();
  const sorted = [...trips].sort((left, right) => tripTime(right) - tripTime(left));
  for (const trip of sorted) {
    const title = activeStatuses.has(trip.status)
      ? 'Current trips'
      : new Intl.DateTimeFormat('en-ZA', { month: 'long', year: 'numeric' }).format(tripDate(trip));
    const items = grouped.get(title) ?? [];
    items.push(trip);
    grouped.set(title, items);
  }
  return Array.from(grouped, ([title, data]) => ({ title, data }));
}

function tripDate(trip: Trip) {
  return new Date(trip.completedAt ?? trip.cancelledAt ?? trip.startedAt ?? trip.acceptedAt ?? trip.requestedAt);
}

function tripTime(trip: Trip) {
  return tripDate(trip).getTime();
}

function formatTripDate(trip: Trip) {
  return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(tripDate(trip));
}

function formatStatus(status: Trip['status']) {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function formatCategory(category: Trip['rideCategory']) {
  if (category === 'GroupPlus') return 'Group+';
  return category ?? 'Ride';
}

function formatFare(trip: Trip) {
  const amount = trip.finalFareAmount ?? trip.estimatedFareAmount;
  if (amount == null) return '—';
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: trip.fareCurrency ?? 'ZAR', minimumFractionDigits: 2 }).format(amount);
}

const styles = StyleSheet.create({
  content: { flexGrow: 1, gap: spacing.lg, paddingHorizontal: spacing.xl, backgroundColor: driverTheme.colors.background },
  emptyContent: { minHeight: '100%' },
  header: { gap: spacing.sm, paddingBottom: spacing.sm },
  title: { color: colors.navy, fontSize: 34, lineHeight: 41, fontWeight: '900', letterSpacing: -1 },
  subtitle: { color: colors.textMuted, fontSize: 14, lineHeight: 20 },
  filterBar: { flexDirection: 'row', gap: spacing.sm, paddingTop: spacing.md },
  filterButton: { minWidth: 92, alignItems: 'center', paddingHorizontal: spacing.lg, paddingVertical: 11, borderRadius: 18, backgroundColor: driverTheme.colors.softControl },
  filterButtonActive: { backgroundColor: colors.blue },
  filterLabel: { color: colors.textMuted, fontSize: 13, fontWeight: '800' },
  filterLabelActive: { color: colors.white },
  pressed: { opacity: 0.76, transform: [{ scale: 0.97 }] },
  sectionTitle: { paddingTop: spacing.md, color: colors.navy, fontSize: 18, fontWeight: '900' },
  tripCard: { gap: spacing.lg, padding: spacing.lg, borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  tripTopRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  rideIconWrap: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center', borderRadius: 15, backgroundColor: driverTheme.colors.softControl },
  rideIconActive: { backgroundColor: colors.blueMuted },
  rideIcon: { width: 22, height: 22 },
  cancelSlash: { position: 'absolute', width: 27, height: 2, borderRadius: 1, backgroundColor: colors.textMuted, transform: [{ rotate: '45deg' }] },
  tripHeading: { minWidth: 0, flex: 1, gap: 3 },
  tripDate: { color: colors.textMuted, fontSize: 11 },
  status: { color: colors.navy, fontSize: 14, fontWeight: '900' },
  statusActive: { color: colors.blue },
  statusCancelled: { color: colors.danger },
  fare: { color: colors.navy, fontSize: 15, fontWeight: '900', fontVariant: ['tabular-nums'] },
  route: { paddingLeft: 13 },
  routePoint: { minHeight: 42, flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md },
  routeDot: { width: 10, height: 10, marginTop: 5, borderRadius: 5 },
  routeLine: { width: 2, height: 18, marginLeft: 4, marginTop: -17, marginBottom: -1, backgroundColor: driverTheme.colors.softBorder },
  address: { minWidth: 0, flex: 1, color: colors.navy, fontSize: 13, lineHeight: 18, fontWeight: '600' },
  tripFooter: { minHeight: 30, flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingTop: spacing.sm, borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: driverTheme.colors.softBorder },
  category: { color: colors.blue, fontSize: 12, fontWeight: '900' },
  tripId: { minWidth: 0, flex: 1, color: colors.textMuted, fontSize: 10 },
  emptyState: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, paddingVertical: 80 },
  emptyIcon: { width: 66, height: 66, alignItems: 'center', justifyContent: 'center', borderRadius: 33, backgroundColor: colors.blueMuted },
  emptyTitle: { paddingTop: spacing.sm, color: colors.navy, fontSize: 20, fontWeight: '900' },
  emptyMessage: { maxWidth: 270, color: colors.textMuted, fontSize: 13, lineHeight: 19, textAlign: 'center' },
});
