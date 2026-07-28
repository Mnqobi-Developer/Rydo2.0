# RYDO Passenger

The Passenger mobile application is an independent Expo project for Android and
iOS.

## Implemented passenger journey

- OTP sign-in/sign-up with protected routes and secure session restoration
- Map-first Home tab with location, place search, route preview, cash selection,
  trip creation, and driver matching
- Live active-trip status backed by SignalR query refreshes
- Activity tab backed by passenger trip history
- Passenger profile creation/editing and backend session logout
- Saved Places surface ready for the future saved-places API

The Card option remains visibly unavailable until the PayFast dashboard and
production callback configuration are completed.

```powershell
npm install
npm run start
```

`npm run start` targets the RYDO Passenger development client. Follow
[Mobile development builds](../../docs/mobile-development-builds.md) to link the
Passenger EAS project and create the first Android or iOS binary. Use
`npm run start:go` only for an intentional Expo Go compatibility check.

Available checks:

```powershell
npm run lint
npm run typecheck
npm run doctor
npm run config:validate
npm run audit:production
```

`src/api` configures the shared RYDO client with the Passenger API URL,
Expo SecureStore token persistence, and TanStack Query. Feature queries must
pass the query function's `signal` to the client so obsolete requests can be
cancelled.

`AuthSessionProvider` restores the encrypted session at startup and exposes
automatic token refresh, expired-session state, retryable restoration, and a
logout action that revokes the backend session before clearing local data.

Only route and layout files belong in `src/app`. Components, features, hooks,
services, and theme code live in dedicated directories under `src`.

See [Maps and location](../../docs/maps-and-location.md) for Google key
restrictions, EAS variables, and the explicit foreground permission flow.

The [mobile real-time foundation](../../docs/mobile-realtime.md) connects the
authenticated operations hub while the app is foregrounded and reconciles
active REST queries after reconnecting.
