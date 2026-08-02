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
import '@/location/background-location-task';

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
                  <Stack
                    screenOptions={{
                      contentStyle: { backgroundColor: colors.surface },
                      headerShown: false,
                    }}
                  >
                    <Stack.Screen name="index" />
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
