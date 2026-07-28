# RYDO

RYDO is a community-focused ride-hailing platform for passengers, drivers, and
operations teams.

## Applications

- `apps/passenger-mobile` — Passenger app built with React Native, Expo Router,
  and TypeScript.
- `apps/driver-mobile` — Driver app built with React Native, Expo Router, and
  TypeScript.
- `apps/admin-dashboard` — Desktop operations dashboard built with React,
  TypeScript, and Vite.
- `apps/backend-api` — Layered ASP.NET Core API with SignalR and
  PostgreSQL/PostGIS-ready persistence.

Shared packages live under `packages/` as the applications begin to share stable
contracts and visual primitives.

`packages/mobile-api-client` is the shared authenticated transport and TanStack
Query foundation used by both mobile applications.

## Current milestone

Passenger, Driver, Admin, and Backend application foundations are now
established. The first backend vertical slice adds Passenger and Driver phone
authentication with OTP verification and revocable token sessions. Remaining
product features continue through focused pull requests.

## Passenger app

```powershell
cd apps/passenger-mobile
npm install
npm run start
```

Scan the Metro QR code with Expo Go while the development machine and phone are
on the same network. Configure a phone-accessible API URL first; see
[API environments](docs/api-environments.md).

## Admin dashboard

```powershell
cd apps/admin-dashboard
npm install
npm run dev
```

## Backend API

```powershell
docker compose -f apps/backend-api/compose.yaml up -d
dotnet run --project apps/backend-api/src/Rydo.Api
```

## Delivery

Changes use focused branches, conventional commits, pull requests, and required
quality checks. See [CONTRIBUTING.md](CONTRIBUTING.md).

Development, staging, and production API targets are documented in
[API environments](docs/api-environments.md).
