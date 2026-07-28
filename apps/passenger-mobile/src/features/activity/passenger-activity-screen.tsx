import type { Trip } from '@rydo/mobile-api-client';
import { EmptyState, ErrorState, LoadingState, RideCard, colors, spacing } from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import { FlatList, RefreshControl } from 'react-native';

import { passengerTripsQuery } from './api';

export function PassengerActivityScreen() {
  const trips = useQuery(passengerTripsQuery);

  if (trips.isLoading) return <LoadingState label="Loading your rides…" />;
  if (trips.isError) return <ErrorState message={trips.error.message} onRetry={() => void trips.refetch()} />;

  return (
    <FlatList
      contentInsetAdjustmentBehavior="automatic"
      data={trips.data ?? []}
      keyExtractor={(trip) => trip.id}
      contentContainerStyle={{ flexGrow: 1, padding: spacing.lg, paddingBottom: 110, gap: spacing.md }}
      refreshControl={<RefreshControl refreshing={trips.isRefetching} tintColor={colors.blue} onRefresh={() => void trips.refetch()} />}
      ListEmptyComponent={<EmptyState title="No rides yet" message="Your completed and upcoming rides will appear here." />}
      renderItem={({ item }) => (
        <RideCard
          title={formatStatus(item.status)}
          pickup={item.pickupAddress}
          destination={item.destinationAddress}
          metadata={formatDate(item.requestedAt)}
          fare={item.finalFareAmount == null ? undefined : formatCurrency(item.finalFareAmount)}
        />
      )}
    />
  );
}

function formatStatus(value: Trip['status']) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' });
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' }).format(value);
}
