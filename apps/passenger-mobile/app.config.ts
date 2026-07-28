import type { ConfigContext, ExpoConfig } from 'expo/config';

type AppVariant = 'development' | 'staging' | 'production';

const variants: Record<
  AppVariant,
  { nameSuffix: string; identifierSuffix: string; schemeSuffix: string }
> = {
  development: {
    nameSuffix: ' Dev',
    identifierSuffix: '.dev',
    schemeSuffix: '-dev',
  },
  staging: {
    nameSuffix: ' Staging',
    identifierSuffix: '.staging',
    schemeSuffix: '-staging',
  },
  production: { nameSuffix: '', identifierSuffix: '', schemeSuffix: '' },
};

function resolveVariant(value: string | undefined): AppVariant {
  const variant = value ?? 'production';

  if (!Object.hasOwn(variants, variant)) {
    throw new Error(`Unsupported RYDO Passenger app variant: ${variant}`);
  }

  return variant as AppVariant;
}

export default ({ config }: ConfigContext): ExpoConfig => {
  const variant = resolveVariant(process.env.APP_VARIANT);
  const settings = variants[variant];
  const plugins: NonNullable<ExpoConfig['plugins']> = [
    ...(config.plugins ?? []),
    'expo-image',
    [
      'expo-dev-client',
      {
        addGeneratedScheme: variant === 'development',
        launchMode: 'launcher',
      },
    ],
    [
      'expo-location',
      {
        locationWhenInUsePermission:
          'RYDO uses your location to choose a pickup point and show nearby ride options.',
      },
    ],
  ];
  const androidMapsKey = process.env.GOOGLE_MAPS_ANDROID_API_KEY;
  const iosMapsKey = process.env.GOOGLE_MAPS_IOS_API_KEY;

  return {
    ...config,
    name: `RYDO Passenger${settings.nameSuffix}`,
    slug: 'rydo-passenger',
    scheme: `rydo-passenger${settings.schemeSuffix}`,
    ios: {
      ...config.ios,
      bundleIdentifier: `za.co.rydo.passenger${settings.identifierSuffix}`,
      config: {
        ...config.ios?.config,
        ...(iosMapsKey ? { googleMapsApiKey: iosMapsKey } : {}),
      },
    },
    android: {
      ...config.android,
      package: `za.co.rydo.passenger${settings.identifierSuffix}`,
      softwareKeyboardLayoutMode: 'resize',
      config: {
        ...config.android?.config,
        ...(androidMapsKey ? { googleMaps: { apiKey: androidMapsKey } } : {}),
      },
    },
    plugins,
    extra: {
      ...config.extra,
      appVariant: variant,
    },
  };
};
