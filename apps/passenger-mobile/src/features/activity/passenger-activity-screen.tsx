import type { Trip } from '@rydo/mobile-api-client';
import { ErrorState, LoadingState, RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import { Image } from 'expo-image';
import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import {
  Alert,
  Pressable,
  RefreshControl,
  SectionList,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import Animated, { FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { passengerTripsQuery } from './api';

type TripFilter = 'past' | 'upcoming';
type TripSection = { title: string; data: Trip[] };

const upcomingStatuses = new Set<Trip['status']>(['Requested', 'Accepted', 'DriverArrived', 'InProgress']);

export function PassengerActivityScreen() {
  const insets = useSafeAreaInsets();
  const trips = useQuery(passengerTripsQuery);
  const [filter, setFilter] = useState<TripFilter>('past');

  const sections = useMemo(
    () => groupTripsByMonth((trips.data ?? []).filter((trip) => (
      filter === 'upcoming' ? upcomingStatuses.has(trip.status) : !upcomingStatuses.has(trip.status)
    ))),
    [filter, trips.data],
  );

  if (trips.isLoading) return <LoadingState label="Loading your trips…" />;
  if (trips.isError) return <ErrorState message={trips.error.message} onRetry={() => void trips.refetch()} />;

  return (
    <SectionList<Trip, TripSection>
      contentContainerStyle={[
        styles.content,
        { paddingTop: insets.top + spacing.lg, paddingBottom: 116 + insets.bottom },
        sections.length === 0 && styles.emptyContent,
      ]}
      contentInsetAdjustmentBehavior="automatic"
      keyExtractor={(trip) => trip.id}
      ListEmptyComponent={<TripsEmptyState filter={filter} />}
      ListHeaderComponent={(
        <View style={styles.headerArea}>
          <View style={styles.titleRow}>
            <Text selectable style={styles.screenTitle}>Trips</Text>
            <Pressable
              accessibilityLabel="About your trips"
              accessibilityRole="button"
              android_ripple={{ color: 'rgba(36,87,255,0.08)', borderless: true }}
              onPress={() => Alert.alert(
                'Your trips',
                'Past trips include completed and cancelled rides. Upcoming shows rides that are currently being matched or are in progress.',
              )}
              style={({ pressed }) => [styles.infoButton, pressed && styles.controlPressed]}
            >
              <Text style={styles.infoGlyph}>i</Text>
            </Pressable>
          </View>
          <View accessibilityRole="tablist" style={styles.tabs}>
            <FilterTab active={filter === 'past'} label="Past" onPress={() => setFilter('past')} />
            <FilterTab active={filter === 'upcoming'} label="Upcoming" onPress={() => setFilter('upcoming')} />
          </View>
        </View>
      )}
      refreshControl={(
        <RefreshControl
          refreshing={trips.isRefetching}
          tintColor={colors.blue}
          onRefresh={() => void trips.refetch()}
        />
      )}
      renderItem={({ item, index }) => (
        <Animated.View entering={FadeInUp.delay(Math.min(index, 5) * 35).duration(220)}>
          <TripHistoryRow trip={item} />
        </Animated.View>
      )}
      renderSectionHeader={({ section }) => (
        <Text selectable style={styles.monthHeading}>{section.title}</Text>
      )}
      sections={sections}
      showsVerticalScrollIndicator={false}
      stickySectionHeadersEnabled={false}
    />
  );
}

function FilterTab({ active, label, onPress }: { active: boolean; label: string; onPress(): void }) {
  return (
    <Pressable
      accessibilityRole="tab"
      accessibilityState={{ selected: active }}
      onPress={onPress}
      style={({ pressed }) => [styles.tab, pressed && styles.controlPressed]}
    >
      <Text style={[styles.tabLabel, active && styles.tabLabelActive]}>{label}</Text>
      <View style={[styles.tabIndicator, active && styles.tabIndicatorActive]} />
    </Pressable>
  );
}

function TripHistoryRow({ trip }: { trip: Trip }) {
  const cancelled = trip.status === 'Cancelled';

  return (
    <Pressable
      accessibilityRole="button"
      android_ripple={{ color: 'rgba(36,87,255,0.05)' }}
      onPress={() => router.push('/')}
      style={({ pressed }) => [styles.tripRow, pressed && styles.tripRowPressed]}
    >
      <View style={styles.rideIconWrap}>
        <Image
          accessibilityIgnoresInvertColors
          contentFit="contain"
          source={require('../../../assets/icons/home/ride.png')}
          style={styles.rideIcon}
        />
        {cancelled ? <View style={styles.cancelSlash} /> : null}
      </View>

      <View style={styles.tripCopy}>
        <Text selectable style={styles.tripMeta}>{formatTripMeta(trip)}</Text>
        <Text selectable numberOfLines={2} style={styles.tripAddress}>{trip.destinationAddress}</Text>
        <Text selectable style={styles.tripFare}>{formatTripFare(trip)}</Text>
      </View>

      <Pressable
        accessibilityLabel={`Plan another ride to ${trip.destinationAddress}`}
        accessibilityRole="button"
        android_ripple={{ color: 'rgba(36,87,255,0.10)', borderless: true }}
        onPress={(event) => {
          event.stopPropagation();
          router.push('/');
        }}
        style={({ pressed }) => [styles.repeatButton, pressed && styles.controlPressed]}
      >
        <RydoIcon name="refresh" color={colors.textMuted} size={23} />
      </Pressable>
    </Pressable>
  );
}

function TripsEmptyState({ filter }: { filter: TripFilter }) {
  return (
    <View style={styles.emptyState}>
      <View style={styles.emptyIcon}>
        <Image
          accessibilityIgnoresInvertColors
          contentFit="contain"
          source={require('../../../assets/icons/home/ride.png')}
          style={styles.emptyRideIcon}
        />
      </View>
      <Text style={styles.emptyTitle}>{filter === 'past' ? 'No past trips' : 'No upcoming trips'}</Text>
      <Text style={styles.emptyMessage}>
        {filter === 'past'
          ? 'Completed and cancelled trips will appear here.'
          : 'Active and upcoming rides will appear here.'}
      </Text>
    </View>
  );
}

function groupTripsByMonth(trips: Trip[]): TripSection[] {
  const grouped = new Map<string, Trip[]>();
  const sorted = [...trips].sort((left, right) => tripTimestamp(right) - tripTimestamp(left));

  for (const trip of sorted) {
    const title = new Intl.DateTimeFormat('en-ZA', { month: 'long', year: 'numeric' }).format(tripDate(trip));
    const current = grouped.get(title) ?? [];
    current.push(trip);
    grouped.set(title, current);
  }

  return Array.from(grouped, ([title, data]) => ({ title, data }));
}

function tripDate(trip: Trip) {
  return new Date(trip.cancelledAt ?? trip.completedAt ?? trip.startedAt ?? trip.requestedAt);
}

function tripTimestamp(trip: Trip) {
  return tripDate(trip).getTime();
}

function formatTripMeta(trip: Trip) {
  const date = tripDate(trip);
  const today = new Date();
  const isToday = date.getFullYear() === today.getFullYear()
    && date.getMonth() === today.getMonth()
    && date.getDate() === today.getDate();
  const dateLabel = isToday
    ? 'Today'
    : new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short' }).format(date);
  const time = new Intl.DateTimeFormat('en-ZA', { hour: '2-digit', minute: '2-digit', hour12: false }).format(date);
  return `${dateLabel} · ${time} · ${formatStatus(trip.status)}`;
}

function formatStatus(value: Trip['status']) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function formatTripFare(trip: Trip) {
  const amount = trip.finalFareAmount ?? trip.estimatedFareAmount;
  if (amount == null) return trip.status === 'Cancelled' ? 'R 0' : 'Fare unavailable';
  return new Intl.NumberFormat('en-ZA', {
    style: 'currency',
    currency: trip.fareCurrency ?? 'ZAR',
    maximumFractionDigits: 0,
  }).format(amount);
}

const styles = StyleSheet.create({
  content: { flexGrow: 1, paddingHorizontal: spacing.xl, backgroundColor: '#FFFFFF' },
  emptyContent: { minHeight: '100%' },
  headerArea: { gap: spacing.xl, paddingBottom: spacing.xl },
  titleRow: { minHeight: 52, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  screenTitle: { color: '#161D1B', fontSize: 34, lineHeight: 41, fontWeight: '900', letterSpacing: -1 },
  infoButton: {
    width: 42,
    height: 42,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1.5,
    borderColor: '#5E6765',
    borderRadius: 21,
  },
  infoGlyph: { color: '#4E5755', fontSize: 20, lineHeight: 23, fontWeight: '900' },
  tabs: { minHeight: 54, flexDirection: 'row', alignItems: 'flex-end', gap: spacing.xl, borderBottomWidth: 1, borderBottomColor: '#E9ECEB' },
  tab: { minWidth: 62, alignItems: 'center', gap: 12 },
  tabLabel: { color: '#242C2A', fontSize: 17, lineHeight: 22, fontWeight: '500' },
  tabLabelActive: { color: colors.navy, fontWeight: '800' },
  tabIndicator: { width: '100%', height: 3, borderRadius: 2, backgroundColor: 'transparent' },
  tabIndicatorActive: { backgroundColor: colors.blue },
  monthHeading: { paddingTop: spacing.xl, paddingBottom: spacing.md, color: '#171F1D', fontSize: 23, lineHeight: 29, fontWeight: '900' },
  tripRow: {
    minHeight: 126,
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.lg,
    paddingVertical: spacing.lg,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E9E7',
  },
  tripRowPressed: { opacity: 0.82, transform: [{ scale: 0.995 }] },
  rideIconWrap: { width: 48, height: 48, alignItems: 'center', justifyContent: 'center' },
  rideIcon: { width: 28, height: 28, tintColor: '#697270' },
  cancelSlash: { position: 'absolute', width: 33, height: 2, borderRadius: 1, backgroundColor: '#697270', transform: [{ rotate: '45deg' }] },
  tripCopy: { minWidth: 0, flex: 1, gap: 5 },
  tripMeta: { color: '#66706D', fontSize: 14, lineHeight: 19 },
  tripAddress: { color: '#17201E', fontSize: 17, lineHeight: 23, fontWeight: '500' },
  tripFare: { color: '#17201E', fontSize: 15, lineHeight: 20, fontWeight: '800', fontVariant: ['tabular-nums'] },
  repeatButton: { width: 46, height: 46, alignItems: 'center', justifyContent: 'center', borderRadius: 23, backgroundColor: '#F1F3F2' },
  controlPressed: { opacity: 0.72, transform: [{ scale: 0.94 }] },
  emptyState: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, paddingVertical: 80 },
  emptyIcon: { width: 68, height: 68, alignItems: 'center', justifyContent: 'center', borderRadius: 34, backgroundColor: colors.blueMuted },
  emptyRideIcon: { width: 30, height: 30, tintColor: colors.blue },
  emptyTitle: { paddingTop: spacing.sm, color: colors.navy, fontSize: 20, lineHeight: 25, fontWeight: '800' },
  emptyMessage: { maxWidth: 270, color: colors.textMuted, fontSize: 14, lineHeight: 20, textAlign: 'center' },
});
