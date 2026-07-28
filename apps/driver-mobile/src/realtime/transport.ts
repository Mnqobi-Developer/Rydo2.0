import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { RealtimeTransport } from '@rydo/mobile-api-client';

const reconnectDelays = [0, 2_000, 10_000, 30_000];

export function createSignalRTransport(
  hubUrl: string,
  getAccessToken: () => Promise<string>,
): RealtimeTransport {
  return new HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: getAccessToken })
    .withAutomaticReconnect(reconnectDelays)
    .configureLogging(LogLevel.Warning)
    .build();
}
