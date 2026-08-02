# RYDO Admin Dashboard

The independent React, Vite, and TypeScript operations surface for RYDO.

## Available operations

- Secure administrator login with JWT refresh rotation and session revocation.
- Overview metrics and SignalR-driven refresh.
- Driver application, vehicle, and protected document inspection.
- Individual document approval or actionable rejection feedback.
- Final driver application approval or rejection.
- Live online-driver and active-trip map.
- User, trip, payment, dispute, and audit visibility.
- Dispute review, resolution, and rejection.

## Local development

Copy `.env.example` to `.env.local`. The Google Maps browser key is optional; without
it, the dashboard still reports connected live coordinates but does not render the
Google base map.

```powershell
Copy-Item .env.example .env.local
npm install
npm run dev
```

The backend must have `AdminAccess` enabled and a bootstrap administrator configured.
Never put the administrator password or a Google Maps key in a committed file.

## Quality checks

```powershell
npm run lint
npm run typecheck
npm run test
npm run build
npm run audit:production
```
