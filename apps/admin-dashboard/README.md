# RYDO Admin Dashboard

The Admin Dashboard is an independent React, Vite, and TypeScript application
for desktop operations.

## Local development

```powershell
Copy-Item .env.example .env.local
npm install
npm run dev
```

The foundation does not call the backend yet. `VITE_API_BASE_URL` reserves the
local API entry point for a later integration branch.

## Quality checks

```powershell
npm run lint
npm run typecheck
npm run test
npm run build
npm run audit:production
```

Business modules, API integration, SignalR, Google Maps, authentication, and
admin permissions are intentionally introduced through separate pull requests.
