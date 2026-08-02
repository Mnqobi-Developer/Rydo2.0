import { Image } from 'expo-image';

export function DriverRideIcon({ color, size = 24 }: { color: string; size?: number }) {
  return (
    <Image
      accessibilityIgnoresInvertColors
      contentFit="contain"
      source={require('../../assets/icons/navigation/trips.png')}
      style={{ width: size, height: size, tintColor: color }}
    />
  );
}
