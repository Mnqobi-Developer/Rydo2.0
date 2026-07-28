# RYDO Admin Dashboard

The Admin Dashboard is an independent React, Vite, and TypeScript application
for desktop operations.

## Local development

```powershell
Copy-Item .env.example .env.local
npm install
npm run dev
```

The foundation does not call the backend yet. `VITE_APP_ENV` and
`VITE_API_BASE_URL` reserve the validated environment contract for the API
integration branch. Staging and production examples and deployment prerequisites
are documented in [`docs/api-environments.md`](../../docs/api-environments.md).

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
