import { GestureHandlerRootView } from 'react-native-gesture-handler';

import { BottomSheetModalProvider } from '@gorhom/bottom-sheet';
import { ErrorState, LoadingState, colors } from '@rydo/mobile-design-system';
import { QueryClientProvider, useQuery } from '@tanstack/react-query';
import { Stack } from 'expo-router/stack';
import { StatusBar } from 'expo-status-bar';
import { KeyboardProvider } from 'react-native-keyboard-controller';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { queryClient } from '@/api';
import { AuthSessionProvider, useAuthSession } from '@/auth/session';
import { passengerProfileQuery } from '@/features/profile/api';
import { RealtimeLifecycleProvider } from '@/realtime/provider';

export default function RootLayout() {
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <KeyboardProvider>
          <BottomSheetModalProvider>
            <QueryClientProvider client={queryClient}>
              <AuthSessionProvider>
                <RealtimeLifecycleProvider>
                  <StatusBar style="dark" />
                  <PassengerNavigator />
                </RealtimeLifecycleProvider>
              </AuthSessionProvider>
            </QueryClientProvider>
          </BottomSheetModalProvider>
        </KeyboardProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}

function PassengerNavigator() {
  const session = useAuthSession();
  const isAuthenticated = session.status === 'authenticated' && session.user?.role === 'Passenger';
  const profile = useQuery({ ...passengerProfileQuery, enabled: isAuthenticated, retry: false });

  if (session.status === 'restoring') {
    return <LoadingState label="Restoring your secure session…" />;
  }

  if (session.status === 'unavailable') {
    return (
      <ErrorState
        title="Unable to restore your session"
        message={session.error?.message}
        onRetry={() => void session.retryRestore()}
      />
    );
  }

  if (isAuthenticated && profile.isPending) {
    return <LoadingState label="Loading your passenger profile…" />;
  }

  if (isAuthenticated && profile.isError) {
    return (
      <ErrorState
        title="Unable to load your profile"
        message={profile.error.message}
        onRetry={() => void profile.refetch()}
      />
    );
  }

  const needsOnboarding = isAuthenticated && profile.data === null;
  const canEnterApp = isAuthenticated && profile.data !== null && profile.data !== undefined;

  return (
    <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.surface } }}>
      <Stack.Protected guard={!isAuthenticated}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
      <Stack.Protected guard={needsOnboarding}>
        <Stack.Screen name="(onboarding)" />
      </Stack.Protected>
      <Stack.Protected guard={canEnterApp}>
        <Stack.Screen name="(app)" />
      </Stack.Protected>
    </Stack>
  );
}
