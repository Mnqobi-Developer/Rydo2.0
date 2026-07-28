import { GestureHandlerRootView } from 'react-native-gesture-handler';

import { BottomSheetModalProvider } from '@gorhom/bottom-sheet';
import { ErrorState, LoadingState, colors } from '@rydo/mobile-design-system';
import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router/stack';
import { StatusBar } from 'expo-status-bar';
import { KeyboardProvider } from 'react-native-keyboard-controller';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { queryClient } from '@/api';
import { AuthSessionProvider, useAuthSession } from '@/auth/session';
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

  const isAuthenticated = session.status === 'authenticated' && session.user?.role === 'Passenger';

  return (
    <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.surface } }}>
      <Stack.Protected guard={!isAuthenticated}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
      <Stack.Protected guard={isAuthenticated}>
        <Stack.Screen name="(app)" />
      </Stack.Protected>
    </Stack>
  );
}
