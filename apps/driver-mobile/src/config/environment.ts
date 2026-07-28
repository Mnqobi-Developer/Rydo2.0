export type AppEnvironment = 'development' | 'staging' | 'production';

const supportedEnvironments = new Set<AppEnvironment>([
  'development',
  'staging',
  'production',
]);

function resolveEnvironment(value: string | undefined): AppEnvironment {
  const environment = value ?? 'development';

  if (!supportedEnvironments.has(environment as AppEnvironment)) {
    throw new Error(`Unsupported RYDO app environment: ${environment}`);
  }

  return environment as AppEnvironment;
}

function resolveApiBaseUrl(value: string | undefined, environment: AppEnvironment) {
  if (!value) {
    throw new Error(
      'EXPO_PUBLIC_API_BASE_URL is required. Copy the matching .env example to .env.local.',
    );
  }

  const url = new URL(value);

  if (environment !== 'development' && url.protocol !== 'https:') {
    throw new Error(`${environment} must use an HTTPS API URL.`);
  }

  return url.toString().replace(/\/$/, '');
}

const environment = resolveEnvironment(process.env.EXPO_PUBLIC_APP_ENV);

export const appConfig = Object.freeze({
  environment,
  apiBaseUrl: resolveApiBaseUrl(
    process.env.EXPO_PUBLIC_API_BASE_URL,
    environment,
  ),
});
