import {
  operationsEvents,
  operationsHubPath,
  type OperationsEventHandlers,
  type OperationsEventPayloads,
} from './realtime-contracts';

export type RealtimeConnectionStatus =
  | 'idle'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'suspended'
  | 'disconnected';

export type RestRefreshReason = 'foreground' | 'reconnected';

export interface RealtimeTransport {
  start(): Promise<void>;
  stop(): Promise<void>;
  on(methodName: string, handler: (payload: never) => void): void;
  off(methodName: string, handler: (payload: never) => void): void;
  onreconnecting(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void): void;
  onclose(callback: (error?: Error) => void): void;
}

export interface OperationsRealtimeClientOptions {
  baseUrl: string;
  getAccessToken(): Promise<string>;
  handlers?: OperationsEventHandlers;
  refreshRestState(reason: RestRefreshReason): Promise<void>;
  connectionFactory: (hubUrl: string, getAccessToken: () => Promise<string>) => RealtimeTransport;
}

export interface OperationsRealtimeClient {
  getStatus(): RealtimeConnectionStatus;
  subscribe(listener: () => void): () => void;
  start(): Promise<void>;
  stop(): Promise<void>;
  setForeground(isForeground: boolean): Promise<void>;
  dispose(): Promise<void>;
}

export function createOperationsRealtimeClient(
  options: OperationsRealtimeClientOptions,
): OperationsRealtimeClient {
  const hubUrl = `${normalizeBaseUrl(options.baseUrl)}${operationsHubPath}`;
  const transport = options.connectionFactory(
    hubUrl,
    options.getAccessToken,
  );
  const listeners = new Set<() => void>();
  const registeredHandlers: Array<[string, (payload: never) => void]> = [];
  let status: RealtimeConnectionStatus = 'idle';
  let shouldRun = false;
  let isForeground = true;
  let disposed = false;
  let startPromise: Promise<void> | null = null;
  let stopPromise: Promise<void> | null = null;

  const publishStatus = (nextStatus: RealtimeConnectionStatus) => {
    if (status === nextStatus) return;
    status = nextStatus;
    listeners.forEach((listener) => listener());
  };

  for (const eventName of Object.values(operationsEvents)) {
    const handler = ((payload: OperationsEventPayloads[typeof eventName]) => {
      const eventHandler = options.handlers?.[eventName] as
        | ((value: OperationsEventPayloads[typeof eventName]) => void)
        | undefined;
      eventHandler?.(payload);
    }) as (payload: never) => void;
    transport.on(eventName, handler);
    registeredHandlers.push([eventName, handler]);
  }

  transport.onreconnecting(() => {
    if (shouldRun && isForeground) publishStatus('reconnecting');
  });
  transport.onreconnected(() => {
    if (!shouldRun || !isForeground) return;
    publishStatus('connected');
    void safelyRefreshRestState(options.refreshRestState, 'reconnected');
  });
  transport.onclose(() => {
    if (!disposed && shouldRun && isForeground) publishStatus('disconnected');
  });

  async function start() {
    if (disposed) throw new Error('The realtime client has been disposed.');
    shouldRun = true;
    if (!isForeground || status === 'connected' || startPromise) {
      return startPromise ?? Promise.resolve();
    }

    startPromise = (async () => {
      if (stopPromise) await stopPromise;
      publishStatus('connecting');
      try {
        await transport.start();
        if (shouldRun && isForeground) {
          publishStatus('connected');
        } else {
          await stopTransport();
        }
      } catch (error) {
        publishStatus('disconnected');
        throw error;
      }
    })().finally(() => {
      startPromise = null;
    });

    return startPromise;
  }

  async function stopTransport() {
    if (stopPromise) return stopPromise;
    stopPromise = transport.stop().catch(() => undefined).finally(() => {
      stopPromise = null;
    });
    return stopPromise;
  }

  async function stop() {
    shouldRun = false;
    if (startPromise) await startPromise.catch(() => undefined);
    await stopTransport();
    if (!disposed) publishStatus('idle');
  }

  async function setForeground(nextForeground: boolean) {
    if (disposed || nextForeground === isForeground) return;
    isForeground = nextForeground;

    if (!nextForeground) {
      if (startPromise) await startPromise.catch(() => undefined);
      await stopTransport();
      publishStatus('suspended');
      return;
    }

    if (shouldRun) {
      await start();
      await safelyRefreshRestState(options.refreshRestState, 'foreground');
    }
  }

  async function dispose() {
    if (disposed) return;
    disposed = true;
    shouldRun = false;
    if (startPromise) await startPromise.catch(() => undefined);
    await stopTransport();
    registeredHandlers.forEach(([eventName, handler]) => transport.off(eventName, handler));
    listeners.clear();
    publishStatus('disconnected');
  }

  return {
    getStatus: () => status,
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    start,
    stop,
    setForeground,
    dispose,
  };
}

function normalizeBaseUrl(value: string) {
  const url = new URL(value);
  url.pathname = url.pathname.replace(/\/$/, '');
  url.search = '';
  url.hash = '';
  return url.toString().replace(/\/$/, '');
}

async function safelyRefreshRestState(
  refreshRestState: (reason: RestRefreshReason) => Promise<void>,
  reason: RestRefreshReason,
) {
  try {
    await refreshRestState(reason);
  } catch {
    // The next query focus/reconnect cycle retries authoritative REST state.
  }
}
