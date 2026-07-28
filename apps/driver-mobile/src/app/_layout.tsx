import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router/stack';
import { StatusBar } from 'expo-status-bar';

import { queryClient } from '@/api';
import { AuthSessionProvider } from '@/auth/session';
import { colors } from '@/theme/colors';

export default function RootLayout() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthSessionProvider>
        <StatusBar style="light" />
        <Stack
          screenOptions={{
            contentStyle: { backgroundColor: colors.surface },
            headerStyle: { backgroundColor: colors.navy },
            headerTintColor: colors.white,
            headerShadowVisible: false,
          }}
        >
          <Stack.Screen name="index" options={{ title: 'RYDO Driver' }} />
        </Stack>
      </AuthSessionProvider>
    </QueryClientProvider>
  );
}
