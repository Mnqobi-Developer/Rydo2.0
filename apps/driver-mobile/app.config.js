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

function resolveVariant(value) {
  const variant = value ?? 'production';

  if (!Object.hasOwn(variants, variant)) {
    throw new Error(`Unsupported RYDO Driver app variant: ${variant}`);
  }

  return variant;
}

module.exports = ({ config }) => {
  const variant = resolveVariant(process.env.APP_VARIANT);
  const settings = variants[variant];
  const plugins = [
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
          'RYDO uses your location for driver positioning and ride matching.',
        locationAlwaysAndWhenInUsePermission:
          'RYDO uses your location while you are online so passengers can track active trips.',
        isIosBackgroundLocationEnabled: true,
        isAndroidBackgroundLocationEnabled: true,
        isAndroidForegroundServiceEnabled: true,
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
    name: `RYDO Driver${settings.nameSuffix}`,
    slug: 'rydo20-driver',
    owner: 'lenkantereke25',
    scheme: `rydo-driver${settings.schemeSuffix}`,
    ios: {
      ...config.ios,
      bundleIdentifier: `za.co.rydo.driver${settings.identifierSuffix}`,
      config: {
        ...config.ios?.config,
        ...(iosMapsKey ? { googleMapsApiKey: iosMapsKey } : {}),
      },
    },
    android: {
      ...config.android,
      package: `za.co.rydo.driver${settings.identifierSuffix}`,
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
