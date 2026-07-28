import { spawnSync } from 'node:child_process';

const applications = {
  passenger: {
    name: 'RYDO Passenger',
    identifier: 'za.co.rydo.passenger',
    scheme: 'rydo-passenger',
  },
  driver: {
    name: 'RYDO Driver',
    identifier: 'za.co.rydo.driver',
    scheme: 'rydo-driver',
  },
};

const variants = {
  development: { nameSuffix: ' Dev', identifierSuffix: '.dev', schemeSuffix: '-dev' },
  staging: {
    nameSuffix: ' Staging',
    identifierSuffix: '.staging',
    schemeSuffix: '-staging',
  },
  production: { nameSuffix: '', identifierSuffix: '', schemeSuffix: '' },
};

const applicationName = process.argv[2];
const application = applications[applicationName];

if (!application) {
  throw new Error('Expected mobile application name: passenger or driver.');
}

for (const [variant, settings] of Object.entries(variants)) {
  const apiBaseUrl =
    variant === 'development'
      ? 'http://127.0.0.1:5190'
      : `https://api-${variant}.rydo.test`;
  const result = spawnSync('npx', ['expo', 'config', '--json', '--type', 'prebuild'], {
    cwd: process.cwd(),
    encoding: 'utf8',
    shell: process.platform === 'win32',
    env: {
      ...process.env,
      APP_VARIANT: variant,
      EXPO_PUBLIC_APP_ENV: variant,
      EXPO_PUBLIC_API_BASE_URL: apiBaseUrl,
      GOOGLE_MAPS_ANDROID_API_KEY: 'test-android-maps-key',
      GOOGLE_MAPS_IOS_API_KEY: 'test-ios-maps-key',
    },
  });

  if (result.status !== 0) {
    throw new Error(
      result.error?.message || result.stderr || `Expo config failed for ${variant}.`,
    );
  }

  const config = JSON.parse(result.stdout);
  const expectedIdentifier = `${application.identifier}${settings.identifierSuffix}`;
  const expectedScheme = `${application.scheme}${settings.schemeSuffix}`;
  const expectedName = `${application.name}${settings.nameSuffix}`;
  const devClientPlugin = config.plugins?.find(
    (plugin) => Array.isArray(plugin) && plugin[0] === 'expo-dev-client',
  );
  const imagePlugin = config.plugins?.find(
    (plugin) => plugin === 'expo-image' || (Array.isArray(plugin) && plugin[0] === 'expo-image'),
  );

  assertEqual(`${variant} name`, config.name, expectedName);
  assertEqual(`${variant} Android package`, config.android?.package, expectedIdentifier);
  assertEqual(`${variant} iOS bundle identifier`, config.ios?.bundleIdentifier, expectedIdentifier);
  assertEqual(`${variant} scheme`, config.scheme, expectedScheme);
  assertEqual(`${variant} manifest marker`, config.extra?.appVariant, variant);
  assertEqual(`${variant} Android Maps key`, config.android?.config?.googleMaps?.apiKey, 'test-android-maps-key');
  assertEqual(`${variant} iOS Maps key`, config.ios?.config?.googleMapsApiKey, 'test-ios-maps-key');
  assertEqual(`${variant} Android keyboard mode`, config.android?.softwareKeyboardLayoutMode, 'resize');
  assertEqual(`${variant} Expo Image plugin`, Boolean(imagePlugin), true);
  assertEqual(
    `${variant} generated development scheme`,
    devClientPlugin?.[1]?.addGeneratedScheme,
    variant === 'development',
  );
}

console.log(`Validated ${applicationName} development, staging, and production configs.`);

function assertEqual(label, actual, expected) {
  if (actual !== expected) {
    throw new Error(`${label}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}.`);
  }
}
