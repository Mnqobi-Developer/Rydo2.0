import { RydoButton, colors, radii, spacing, typography } from '@rydo/mobile-design-system';
import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { passengerTripsQuery } from '@/features/activity/api';
import { passengerProfileQuery } from '@/features/profile/api';
import { RidePlannerScreen } from '@/features/ride-planning/ride-planner-screen';

export function PassengerHomeScreen() {
  const insets = useSafeAreaInsets();
  const profile = useQuery(passengerProfileQuery);
  const trips = useQuery(passengerTripsQuery);
  const activeTrip = trips.data?.find((trip) => trip.status !== 'Completed' && trip.status !== 'Cancelled') ?? null;

  return (
    <View style={{ flex: 1 }}>
      <RidePlannerScreen greetingName={profile.data?.firstName} profileReady={Boolean(profile.data)} activeTrip={activeTrip} />
      {!profile.isLoading && !profile.data ? (
        <View
          style={{
            position: 'absolute',
            top: insets.top + spacing.md,
            left: spacing.md,
            right: 76,
            borderCurve: 'continuous',
            borderRadius: radii.lg,
            backgroundColor: colors.navyGlass,
            padding: spacing.md,
            gap: spacing.sm,
          }}
        >
          <Text selectable style={{ color: colors.white, fontWeight: typography.weight.bold }}>
            Complete your passenger profile
          </Text>
          <Text selectable style={{ color: '#D7E2F2', fontSize: typography.size.caption }}>
            Add your name before requesting your first ride.
          </Text>
          <RydoButton label="Complete profile" fullWidth={false} onPress={() => router.push('/account')} />
        </View>
      ) : null}
    </View>
  );
}
