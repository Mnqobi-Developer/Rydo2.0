import { Stack } from 'expo-router/stack';

export default function AuthenticationLayout() {
  return <Stack screenOptions={{ headerShown: false, animation: 'fade' }} />;
}
