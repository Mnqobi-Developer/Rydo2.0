import { describe, expect, it, vi } from 'vitest';

import {
  createOperationsRealtimeClient,
  operationsEvents,
  type RealtimeTransport,
} from '../src';

describe('operations realtime client', () => {
  it('registers every backend event and starts with the authenticated hub URL', async () => {
    const transport = new FakeTransport();
    const getAccessToken = vi.fn(async () => 'access-token');
    const connectionFactory = vi.fn(() => transport);
    const client = createOperationsRealtimeClient({
      baseUrl: 'https://api.rydo.test/',
      getAccessToken,
      refreshRestState: vi.fn(async () => undefined),
      connectionFactory,
    });

    await client.start();

    expect(connectionFactory).toHaveBeenCalledWith(
      'https://api.rydo.test/hubs/operations',
      getAccessToken,
    );
    expect([...transport.eventHandlers.keys()]).toEqual(Object.values(operationsEvents));
    expect(transport.start).toHaveBeenCalledOnce();
    expect(client.getStatus()).toBe('connected');
  });

  it('suspends in the background and refreshes REST state after foreground resume', async () => {
    const transport = new FakeTransport();
    const refreshRestState = vi.fn(async () => undefined);
    const client = createClient(transport, refreshRestState);
    await client.start();

    await client.setForeground(false);
    expect(transport.stop).toHaveBeenCalledOnce();
    expect(client.getStatus()).toBe('suspended');

    await client.setForeground(true);
    expect(transport.start).toHaveBeenCalledTimes(2);
    expect(refreshRestState).toHaveBeenCalledWith('foreground');
    expect(client.getStatus()).toBe('connected');
  });

  it('stays stopped across lifecycle changes after authentication ends', async () => {
    const transport = new FakeTransport();
    const refreshRestState = vi.fn(async () => undefined);
    const client = createClient(transport, refreshRestState);
    await client.start();
    await client.stop();
    await client.setForeground(false);
    await client.setForeground(true);

    expect(transport.start).toHaveBeenCalledOnce();
    expect(refreshRestState).not.toHaveBeenCalled();
    expect(client.getStatus()).toBe('suspended');
  });

  it('reports automatic reconnect and refreshes authoritative REST state afterwards', async () => {
    const transport = new FakeTransport();
    const refreshRestState = vi.fn(async () => undefined);
    const client = createClient(transport, refreshRestState);
    await client.start();

    transport.reconnecting?.(new Error('network lost'));
    expect(client.getStatus()).toBe('reconnecting');
    transport.reconnected?.('connection-2');
    await Promise.resolve();

    expect(client.getStatus()).toBe('connected');
    expect(refreshRestState).toHaveBeenCalledWith('reconnected');
  });

  it('delivers typed backend events and removes handlers on disposal', async () => {
    const transport = new FakeTransport();
    const tripUpdated = vi.fn();
    const client = createOperationsRealtimeClient({
      baseUrl: 'https://api.rydo.test',
      getAccessToken: async () => 'token',
      handlers: { TripUpdated: tripUpdated },
      refreshRestState: vi.fn(async () => undefined),
      connectionFactory: () => transport,
    });
    const trip = { id: 'trip-1' };

    transport.emit('TripUpdated', trip);
    expect(tripUpdated).toHaveBeenCalledWith(trip);

    await client.dispose();
    expect(transport.eventHandlers.size).toBe(0);
  });
});

function createClient(transport: FakeTransport, refreshRestState: () => Promise<void>) {
  return createOperationsRealtimeClient({
    baseUrl: 'https://api.rydo.test',
    getAccessToken: async () => 'token',
    refreshRestState,
    connectionFactory: () => transport,
  });
}

class FakeTransport implements RealtimeTransport {
  readonly eventHandlers = new Map<string, (payload: never) => void>();
  readonly start = vi.fn(async () => undefined);
  readonly stop = vi.fn(async () => undefined);
  reconnecting: ((error?: Error) => void) | undefined;
  reconnected: ((connectionId?: string) => void) | undefined;
  closed: ((error?: Error) => void) | undefined;

  on(methodName: string, handler: (payload: never) => void) {
    this.eventHandlers.set(methodName, handler);
  }

  off(methodName: string, handler: (payload: never) => void) {
    if (this.eventHandlers.get(methodName) === handler) this.eventHandlers.delete(methodName);
  }

  onreconnecting(callback: (error?: Error) => void) {
    this.reconnecting = callback;
  }

  onreconnected(callback: (connectionId?: string) => void) {
    this.reconnected = callback;
  }

  onclose(callback: (error?: Error) => void) {
    this.closed = callback;
  }

  emit(methodName: string, payload: unknown) {
    this.eventHandlers.get(methodName)?.(payload as never);
  }
}
