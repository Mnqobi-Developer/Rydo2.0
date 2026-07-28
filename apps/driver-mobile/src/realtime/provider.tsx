import { type PropsWithChildren, useEffect } from 'react';
import { AppState } from 'react-native';

import { useAuthSession } from '@/auth/session';

import { getRealtimeClient } from './client';

export function RealtimeLifecycleProvider({ children }: PropsWithChildren) {
  const session = useAuthSession();

  useEffect(() => {
    const realtimeClient = getRealtimeClient();
    if (session.status === 'authenticated') {
      void realtimeClient.start().catch(() => undefined);
    } else {
      void realtimeClient.stop();
    }
  }, [session.status]);

  useEffect(() => {
    const realtimeClient = getRealtimeClient();
    void realtimeClient.setForeground(AppState.currentState === 'active');
    const subscription = AppState.addEventListener('change', (state) => {
      void realtimeClient.setForeground(state === 'active');
    });
    return () => subscription.remove();
  }, []);

  return children;
}
