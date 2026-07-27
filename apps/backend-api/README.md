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
- `POST /api/v1/auth/otp/request` — begin Passenger or Driver phone sign-in.
- `POST /api/v1/auth/otp/verify` — verify the six-digit code and create a session.
- `POST /api/v1/auth/refresh` — rotate a refresh token and issue a new token pair.
- `GET /api/v1/auth/me` — return the authenticated user for an active session.
- `POST /api/v1/auth/sessions/revoke` — revoke the current database session.
- `GET /api/v1/passengers/me/profile` — return the signed-in Passenger's profile.
- `PUT /api/v1/passengers/me/profile` — create or update the signed-in Passenger's profile.
- `GET /api/v1/drivers/me/profile` — return the signed-in Driver's profile and onboarding state.
- `PUT /api/v1/drivers/me/profile` — create or update an editable Driver profile.
- `POST /api/v1/drivers/me/onboarding/submit` — submit a completed Driver profile for review.
- `GET /api/v1/drivers/me/documents` — list the signed-in Driver's current document metadata.
- `GET /api/v1/drivers/me/documents/{documentId}` — return owned document metadata.
- `POST /api/v1/drivers/me/documents` — register metadata for a protected Driver document.
- `GET /api/v1/drivers/me/vehicle` — return the signed-in Driver's current vehicle.
- `PUT /api/v1/drivers/me/vehicle` — create or update editable vehicle information.
- `POST /api/v1/trips` — request a trip as the signed-in Passenger.
- `GET /api/v1/trips/me` — list trips belonging to the signed-in Passenger or Driver.
- `GET /api/v1/trips/{tripId}` — return a trip visible to the signed-in participant.
- `POST /api/v1/trips/{tripId}/accept` — assign the signed-in Driver to a requested trip.
- `POST /api/v1/trips/{tripId}/matching` — offer a requested trip to nearby eligible Drivers.
- `POST /api/v1/trips/{tripId}/arrive` — mark the assigned Driver as arrived.
- `POST /api/v1/trips/{tripId}/start` — start an arrived trip.
- `POST /api/v1/trips/{tripId}/complete` — complete an in-progress trip.
- `POST /api/v1/trips/{tripId}/cancel` — cancel a trip before it starts.
- `GET /api/v1/drivers/me/availability` — return the signed-in Driver's availability.
- `POST /api/v1/drivers/me/availability/online` — go online with a current location.
- `POST /api/v1/drivers/me/availability/offline` — stop receiving trip offers.
- `POST /api/v1/drivers/me/location` — update an online Driver's current location.
- `GET /api/v1/drivers/me/trip-offers` — list current, unexpired trip offers.
- `POST /api/v1/drivers/me/trip-offers/{tripId}/decline` — decline an owned trip offer.
- `POST /api/v1/trips/{tripId}/payments` — create the trip's Cash or PayFast payment.
- `GET /api/v1/trips/{tripId}/payment` — return payment state to a trip participant.
- `POST /api/v1/payments/{paymentId}/cash/confirm` — let the assigned Driver confirm completed-trip cash.
- `POST /api/v1/payments/payfast/notify` — receive PayFast's public ITN callback.
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

From `apps/backend-api`, restore the local EF tool and verify the migration:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations script --idempotent --configuration Release --project src/Rydo.Infrastructure --startup-project src/Rydo.Api
```

Payments, ratings, and admin operations remain intentionally
separated for focused follow-up branches.

## Driver matching

Only Drivers with approved onboarding can go online. Matching considers online
Drivers whose location is no more than two minutes old, excludes Drivers with
active trips, ranks candidates by pickup distance, and offers each request to
up to five Drivers within 20 kilometres. Offers expire after 30 seconds.

Accepting an owned offer assigns the Driver through the trip state machine,
takes that Driver offline, and expires competing offers. Database concurrency
tokens and unique indexes protect matching decisions from duplicate responses.

## Payments and PayFast

Payment amounts come only from the server-finalized trip fare; mobile clients do
not submit or override charge amounts. Cash payments can be recorded now. A
PayFast payment returns a signed hosted-checkout form only when PayFast is fully
configured. PayFast is intentionally disabled by default while merchant setup
is pending.

When the Sandbox dashboard is ready, configure these through secrets or
environment variables and set `PayFast__Enabled=true`:

- `PayFast__MerchantId`
- `PayFast__MerchantKey`
- `PayFast__Passphrase`
- `PayFast__ReturnUrl`
- `PayFast__CancelUrl`
- `PayFast__NotifyUrl`

The three URLs must be public HTTPS URLs. The notify URL should point to
`https://api.rydo.co.za/api/v1/payments/payfast/notify`; PayFast cannot deliver
ITNs to localhost. Before a callback changes payment state, the API checks its
signature, merchant, source IP range, exact ZAR amount, provider transaction ID,
and PayFast server confirmation. Callback payloads are represented only by a
SHA-256 hash in the audit table rather than being stored with customer data.

## Phone authentication

Local Development responses include `developmentCode` so Passenger and Driver
apps can complete OTP flows without an SMS provider. Hosted environments do not
register this provider and must configure real delivery before OTP requests are
enabled.

Hosted environments must provide these secrets through environment variables:

- `Authentication__SigningKey` — at least 64 characters.
- `Authentication__OtpPepper` — at least 32 characters.

OTP codes and refresh tokens are never stored in plaintext. Refresh tokens are
rotated after use; replaying a consumed token revokes its full session family.
Admin is a defined authorization role but cannot self-register through phone
sign-in.
