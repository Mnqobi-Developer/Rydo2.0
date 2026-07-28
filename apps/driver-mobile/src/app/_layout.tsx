import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router/stack';
import { StatusBar } from 'expo-status-bar';

import { queryClient } from '@/api';
import { colors } from '@/theme/colors';

export default function RootLayout() {
  return (
    <QueryClientProvider client={queryClient}>
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
    </QueryClientProvider>
  );
}
