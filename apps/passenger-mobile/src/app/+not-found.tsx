import { RydoButton, colors, spacing } from '@rydo/mobile-design-system';
import { router, Stack } from 'expo-router';
import { ScrollView, Text } from 'react-native';

export default function NotFoundRoute() {
  return (
    <ScrollView
      contentInsetAdjustmentBehavior="automatic"
      contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', alignItems: 'center', padding: spacing.xl, gap: spacing.lg }}
    >
      <Stack.Screen options={{ title: 'Not found', headerShown: true }} />
      <Text selectable style={{ color: colors.navy, fontSize: 22, fontWeight: '800' }}>This screen does not exist.</Text>
      <RydoButton label="Return home" fullWidth={false} onPress={() => router.replace('/')} />
    </ScrollView>
  );
}
