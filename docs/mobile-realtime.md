# Mobile real-time foundation

Passenger and Driver use `@microsoft/signalr` to connect to the authenticated
ASP.NET Core hub at `/hubs/operations`. The connection is active only while the
user has an authenticated RYDO session and the app is in the foreground.

## Authentication

The SignalR `accessTokenFactory` calls the shared API client's
`getAccessToken()`. That method reads credentials from Expo SecureStore and
rotates an access token before its final 60 seconds. Refresh tokens are never
returned to the real-time transport.

The factory runs whenever SignalR needs a token, including reconnect attempts.
The API accepts the WebSocket `access_token` query parameter only for the
operations hub and applies the same backend session validation as REST calls.

## Connection lifecycle

- Initial connection begins only after `AuthSessionProvider` reports an
  authenticated session.
- SignalR retries dropped connections after 0, 2, 10, and 30 seconds.
- The connection is stopped while the app is backgrounded to avoid relying on
  suspended JavaScript execution or a stale mobile socket.
- Returning to the foreground starts a fresh authenticated connection.
- Logout, revocation, or session expiry stops the connection.
- After automatic reconnect or foreground resume, active TanStack Query REST
  queries are invalidated and refetched. REST remains the authoritative state
  after events may have been missed.

## Server event contract

Event names are case-sensitive and mirror `IOperationsClient`:

- `TripUpdated`
- `TripOfferUpdated`
- `DriverAvailabilityUpdated`
- `PaymentUpdated`
- `DisputeUpdated`
- `DriverReviewUpdated`
- `AdminOperationsChanged`

The shared package exports typed payloads and event constants. Passenger and
Driver handlers invalidate the relevant query families without duplicating the
server event strings.
