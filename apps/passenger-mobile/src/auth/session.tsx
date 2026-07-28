import type { AuthSessionSnapshot } from '@rydo/mobile-api-client';
import {
  createContext,
  type PropsWithChildren,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useSyncExternalStore,
} from 'react';

import { apiClient, queryClient } from '@/api';

interface AuthSessionContextValue extends AuthSessionSnapshot {
  logout(): Promise<void>;
  retryRestore(): Promise<void>;
}

const AuthSessionContext = createContext<AuthSessionContextValue | null>(null);

export function AuthSessionProvider({ children }: PropsWithChildren) {
  const snapshot = useSyncExternalStore(
    apiClient.auth.subscribe,
    apiClient.auth.getSnapshot,
    apiClient.auth.getSnapshot,
  );

  useEffect(() => {
    void apiClient.auth.restoreSession().catch(() => undefined);
  }, []);

  const logout = useCallback(async () => {
    try {
      await apiClient.auth.logout();
    } catch {
      // The API client clears local credentials even when revocation is offline.
    } finally {
      queryClient.clear();
    }
  }, []);

  const retryRestore = useCallback(async () => {
    await apiClient.auth.restoreSession().catch(() => undefined);
  }, []);

  const value = useMemo(
    () => ({ ...snapshot, logout, retryRestore }),
    [logout, retryRestore, snapshot],
  );

  return (
    <AuthSessionContext.Provider value={value}>
      {children}
    </AuthSessionContext.Provider>
  );
}

export function useAuthSession() {
  const session = useContext(AuthSessionContext);

  if (!session) {
    throw new Error('useAuthSession must be used inside AuthSessionProvider.');
  }

  return session;
}
