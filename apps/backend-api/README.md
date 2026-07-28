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

The local launch profile listens on port 5190 on all local interfaces. Browsers
on this computer can use `http://localhost:5190`; physical phones must use the
computer's LAN address. Run `scripts/start-lan.ps1` from the repository root to
print the correct URL. The complete environment setup is documented in
[`docs/api-environments.md`](../../docs/api-environments.md).

Production environments must supply the database connection securely through
`ConnectionStrings__RydoDatabase`. Never commit hosted credentials.

`Dockerfile` packages the API as a non-root container listening on port 8080.
Set `ASPNETCORE_ENVIRONMENT` and all server secrets in the hosting platform;
environment-specific settings are never baked into the image.

### Database access boundary

All RYDO application tables in Supabase's exposed `public` schema have PostgreSQL
row-level security enabled with no `anon` or `authenticated` policies. This is
intentional deny-by-default behavior: mobile and dashboard clients use the RYDO
API and cannot read or mutate business tables through Supabase's Data API.

The API currently connects through a trusted database-owner credential, which
PostgreSQL allows to bypass ordinary RLS. Keep that credential server-side only.
Future public tables are automatically protected by the
`rydo_enable_rls_on_public_table` event trigger. The PostGIS-owned
`public.spatial_ref_sys` table is deliberately excluded because it is managed by
the extension rather than RYDO.

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
- `POST /api/v1/trips/{tripId}/ratings` — rate the other participant after trip completion.
- `GET /api/v1/trips/{tripId}/ratings/me` — return the signed-in participant's own rating.
- `GET /api/v1/ratings/me/summary` — return aggregate ratings received by the signed-in user.
- `GET /api/v1/drivers/{driverUserId}/ratings/summary` — return a Driver's aggregate rating summary.
- `POST /api/v1/trips/{tripId}/disputes` — open a dispute for a completed or cancelled trip.
- `GET /api/v1/disputes/me` — list disputes involving the signed-in participant.
- `GET /api/v1/disputes/{disputeId}` — return an involved participant's dispute and messages.
- `POST /api/v1/disputes/{disputeId}/messages` — add a message to an open participant dispute.
- `POST /api/v1/admin/auth/login` — create a session for a configured Admin account.
- `GET /api/v1/admin/overview` — return operational counts for the Admin dashboard.
- `GET /api/v1/admin/users` — return paginated Passenger, Driver, and Admin accounts.
- `GET /api/v1/admin/drivers` — return paginated Driver review packets.
- `GET /api/v1/admin/drivers/{driverUserId}` — inspect a Driver profile, documents, and vehicle.
- `POST /api/v1/admin/drivers/{driverUserId}/review` — approve or reject submitted onboarding.
- `GET /api/v1/admin/drivers/live` — return online Driver locations for the live operations map.
- `GET /api/v1/admin/trips` — return paginated operational trip records.
- `GET /api/v1/admin/payments` — return paginated payment records.
- `GET /api/v1/admin/disputes` — return paginated dispute cases and messages.
- `POST /api/v1/admin/disputes/{disputeId}/review` — review, resolve, or reject a dispute.
- `GET /api/v1/admin/audit` — return immutable Admin action records.
- `/hubs/operations` — authenticated SignalR endpoint for live trip and
  operations events. JWT bearer tokens may be supplied through the normal
  authorization header or the SignalR `access_token` query parameter during
  WebSocket and server-sent-event connection setup.
- `/openapi/v1.json` — OpenAPI document in the Development environment only.

## Real-time events

The operations hub assigns each authenticated connection to its own user group
and role group. Clients cannot join arbitrary groups or publish events. The API
publishes these stable client method names:

- `TripUpdated`
- `TripOfferUpdated`
- `DriverAvailabilityUpdated`
- `PaymentUpdated`
- `DisputeUpdated`
- `DriverReviewUpdated`
- `AdminOperationsChanged`

Passenger and Driver clients receive only events for trips and records they are
involved in; Admin clients receive operational events through the Admin role
group. Delivery is best-effort after the database transaction commits, so REST
responses remain authoritative and reconnecting clients must refresh current
state. Multi-instance production deployments will require a shared SignalR
backplane such as Redis.

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

## Admin operations

Admin phone self-registration remains disabled. A deployment can bootstrap one
Admin account from environment secrets by setting `AdminAccess__Enabled=true`
and supplying `AdminAccess__BootstrapEmail`,
`AdminAccess__BootstrapPhoneNumber`, and `AdminAccess__BootstrapPassword`. The
password must contain at least 16 characters. It is PBKDF2-SHA256 hashed before
database storage and rotated when the configured bootstrap secret changes.

Admin login uses the same JWT and database session controls as the mobile apps,
with a separate rate limit. Admin-only endpoints provide operational reads,
Driver verification, dispute resolution, and an immutable mutation audit log.
Driver reviews atomically update the profile, current documents, vehicle, and
audit entry. Participant APIs cannot perform these operations.

## Disputes

A Passenger or assigned Driver can open one dispute for a completed or
cancelled trip. Opening details are immutable, identical retries are
idempotent, and both trip participants can view the case and add messages while
it remains open or under review. Status and resolution fields are persisted for
the Admin operations layer; participant endpoints cannot resolve or
reject their own disputes.

## Ratings

Passengers and assigned Drivers may rate each other only after a completed
trip. Each participant can submit one immutable rating per trip. Repeating the
same normalized score and comment is idempotent; attempting to replace it is a
conflict. Free-text comments are visible only to their author through the
trip-specific endpoint. Driver and personal summaries expose aggregate
scores and distributions without comments.

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
