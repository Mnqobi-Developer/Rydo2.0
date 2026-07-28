import { GestureHandlerRootView } from 'react-native-gesture-handler';

import { BottomSheetModalProvider } from '@gorhom/bottom-sheet';
import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router/stack';
import { StatusBar } from 'expo-status-bar';
import { KeyboardProvider } from 'react-native-keyboard-controller';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { queryClient } from '@/api';
import { AuthSessionProvider } from '@/auth/session';
import { RealtimeLifecycleProvider } from '@/realtime/provider';
import { colors } from '@/theme/colors';

export default function RootLayout() {
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <KeyboardProvider>
          <BottomSheetModalProvider>
            <QueryClientProvider client={queryClient}>
              <AuthSessionProvider>
                <RealtimeLifecycleProvider>
                  <StatusBar style="light" />
                  <Stack
                    screenOptions={{
                      contentStyle: { backgroundColor: colors.surface },
                      headerStyle: { backgroundColor: colors.navy },
                      headerTintColor: colors.white,
                      headerShadowVisible: false,
                    }}
                  >
                    <Stack.Screen name="index" options={{ title: 'RYDO Passenger' }} />
                  </Stack>
                </RealtimeLifecycleProvider>
              </AuthSessionProvider>
            </QueryClientProvider>
          </BottomSheetModalProvider>
        </KeyboardProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
