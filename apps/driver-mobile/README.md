# RYDO Driver

The Driver mobile application is an independent Expo project for Android and
iOS.

```powershell
npm install
npm run start
```

Available checks:

```powershell
npm run lint
npm run typecheck
npm run doctor
npm run audit:production
```

`src/api` configures the shared RYDO client with the Driver API URL, Expo
SecureStore token persistence, and TanStack Query. Feature queries must pass the
query function's `signal` to the client so obsolete requests can be cancelled.

`AuthSessionProvider` restores the encrypted session at startup and exposes
automatic token refresh, expired-session state, retryable restoration, and a
logout action that revokes the backend session before clearing local data.

Only route and layout files belong in `src/app`. Components, features, hooks,
services, and theme code live in dedicated directories under `src`.
