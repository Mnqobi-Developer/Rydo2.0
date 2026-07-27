# RYDO Backend API

The Backend API is the trusted decision layer for RYDO. This foundation uses
ASP.NET Core, C#, Entity Framework Core, PostgreSQL/PostGIS, and SignalR while
keeping business capabilities in focused follow-up branches.

## Structure

- `src/Rydo.Api` — HTTP host, controllers, middleware, health, and SignalR.
- `src/Rydo.Application` — use-case contracts and application behavior.
- `src/Rydo.Domain` — business entities, value objects, and rules.
- `src/Rydo.Infrastructure` — EF Core persistence and external integrations.
- `tests/Rydo.Api.Tests` — API integration tests.

Dependencies point inward: API composes Application and Infrastructure;
Infrastructure depends on Application and Domain; Domain remains independent.

## Local development

Start PostgreSQL with PostGIS:

```powershell
docker compose -f apps/backend-api/compose.yaml up -d
```

Run the API from the repository root:

```powershell
dotnet restore apps/backend-api/Rydo.Backend.slnx
dotnet run --project apps/backend-api/src/Rydo.Api
```

The local launch profile serves the API on `http://localhost:5190`.

Production environments must supply the database connection securely through
`ConnectionStrings__RydoDatabase`. Never commit hosted credentials.

## Foundation endpoints

- `GET /health/live` — process liveness for CI and future hosting probes.
- `GET /health/ready` — database-aware readiness for future hosting probes.
- `GET /api/v1/system` — non-sensitive service identity and foundation status.
- `/hubs/operations` — SignalR transport endpoint reserved for authorized live
  trip and operations events.
- `/openapi/v1.json` — OpenAPI document in the Development environment only.

## Quality checks

```powershell
dotnet restore apps/backend-api/Rydo.Backend.slnx --locked-mode
dotnet build apps/backend-api/Rydo.Backend.slnx --no-restore --configuration Release
dotnet test apps/backend-api/Rydo.Backend.slnx --no-build --configuration Release
dotnet format apps/backend-api/Rydo.Backend.slnx --no-restore --verify-no-changes
dotnet list apps/backend-api/Rydo.Backend.slnx package --include-transitive --vulnerable
```

Authentication, sessions, trips, matching, payments, ratings, admin operations,
and their migrations are intentionally added through separate branches.
