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
- `apps/backend-api` — ASP.NET Core API and SignalR service. Added in its own
  foundation branch.

Shared packages will live under `packages/` as the applications begin to share
stable contracts and visual primitives.

## Current milestone

Passenger, Driver, and Admin application foundations are now established.
Product features are intentionally delivered later through focused pull
requests.

## Passenger app

```powershell
cd apps/passenger-mobile
npm install
npm run start
```

Scan the Metro QR code with Expo Go while the development machine and phone are
on the same network.

## Admin dashboard

```powershell
cd apps/admin-dashboard
npm install
npm run dev
```

## Delivery

Changes use focused branches, conventional commits, pull requests, and required
quality checks. See [CONTRIBUTING.md](CONTRIBUTING.md).
