import type { TokenPair } from './contracts';

export interface TokenStore {
  get(): Promise<TokenPair | null>;
  set(tokens: TokenPair): Promise<void>;
  clear(): Promise<void>;
}

export interface AsyncKeyValueStorage {
  getItem(key: string): Promise<string | null>;
  setItem(key: string, value: string): Promise<void>;
  removeItem(key: string): Promise<void>;
}

export function isTokenPair(value: unknown): value is TokenPair {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<TokenPair>;
  const user = candidate.user as Partial<TokenPair['user']> | undefined;

  return (
    typeof candidate.accessToken === 'string' &&
    candidate.accessToken.length > 0 &&
    typeof candidate.accessTokenExpiresAt === 'string' &&
    !Number.isNaN(Date.parse(candidate.accessTokenExpiresAt)) &&
    typeof candidate.refreshToken === 'string' &&
    candidate.refreshToken.length > 0 &&
    typeof candidate.refreshTokenExpiresAt === 'string' &&
    !Number.isNaN(Date.parse(candidate.refreshTokenExpiresAt)) &&
    typeof user?.id === 'string' &&
    typeof user.phoneNumber === 'string' &&
    (user.role === 'Passenger' || user.role === 'Driver' || user.role === 'Admin')
  );
}

export function createSerializedTokenStore(
  storage: AsyncKeyValueStorage,
  key: string,
): TokenStore {
  return {
    async get() {
      const storedValue = await storage.getItem(key);

      if (!storedValue) {
        return null;
      }

      try {
        const tokens: unknown = JSON.parse(storedValue);

        if (isTokenPair(tokens)) {
          return tokens;
        }
      } catch {
        // Corrupt secure storage is treated as a signed-out session.
      }

      await storage.removeItem(key);
      return null;
    },
    async set(tokens) {
      await storage.setItem(key, JSON.stringify(tokens));
    },
    async clear() {
      await storage.removeItem(key);
    },
  };
}
