import { Stack } from 'expo-router/stack';
import { StatusBar } from 'expo-status-bar';

import '@/config/environment';
import { colors } from '@/theme/colors';

export default function RootLayout() {
  return (
    <>
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
    </>
  );
}
