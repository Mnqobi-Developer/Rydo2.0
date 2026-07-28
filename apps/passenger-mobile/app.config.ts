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
    [
      'expo-dev-client',
      {
        addGeneratedScheme: variant === 'development',
        launchMode: 'launcher',
      },
    ],
  ];

  return {
    ...config,
    name: `RYDO Passenger${settings.nameSuffix}`,
    slug: 'rydo-passenger',
    scheme: `rydo-passenger${settings.schemeSuffix}`,
    ios: {
      ...config.ios,
      bundleIdentifier: `za.co.rydo.passenger${settings.identifierSuffix}`,
    },
    android: {
      ...config.android,
      package: `za.co.rydo.passenger${settings.identifierSuffix}`,
    },
    plugins,
    extra: {
      ...config.extra,
      appVariant: variant,
    },
  };
};
