/** @type {Record<'development' | 'staging' | 'production', { nameSuffix: string; identifierSuffix: string; schemeSuffix: string }>} */
const variants = {
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

/**
 * @param {string | undefined} value
 * @returns {'development' | 'staging' | 'production'}
 */
function resolveVariant(value) {
  const variant = value ?? 'production';

  if (!Object.hasOwn(variants, variant)) {
    throw new Error(`Unsupported RYDO Passenger app variant: ${variant}`);
  }

  return variant;
}

/** @param {import('expo/config').ConfigContext} context */
module.exports = ({ config }) => {
  const variant = resolveVariant(process.env.APP_VARIANT);
  const settings = variants[variant];
  const plugins = [
    ...(config.plugins ?? []),
    'expo-font',
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
    [
      'expo-build-properties',
      {
        android: {
          usesCleartextTraffic: variant === 'development',
        },
      },
    ],
  ];
  const androidMapsKey = process.env.GOOGLE_MAPS_ANDROID_API_KEY;
  const iosMapsKey = process.env.GOOGLE_MAPS_IOS_API_KEY;

  return {
    ...config,
    name: `RYDO Passenger${settings.nameSuffix}`,
    slug: 'rydo20',
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
