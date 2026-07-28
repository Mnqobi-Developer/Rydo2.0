# API environments

RYDO clients use one public API base URL per environment. Database credentials,
JWT signing keys, payment secrets, and other server-only values must never use
`EXPO_PUBLIC_` or `VITE_` variables because those values are embedded in client
bundles.

| Environment | API base URL | Intended use |
| --- | --- | --- |
| Development | `http://<LAN-IP>:5190` or an HTTPS tunnel | Expo Go and local dashboard work |
| Staging | `https://api-staging.rydo.co.za` | Internal builds and pre-production testing |
| Production | `https://api.rydo.co.za` | Store builds and the production dashboard |

Staging and production URLs are reserved targets. They become reachable only
after the API is deployed, DNS records point to the selected hosting provider,
and managed TLS certificates are active.

## Use the API from a physical phone

Keep the development computer and phone on the same private network. From the
repository root, with `ConnectionStrings__RydoDatabase` configured in the same
PowerShell session, run:

```powershell
.\apps\backend-api\scripts\start-lan.ps1
```

The script binds Kestrel to all local interfaces and prints the phone-safe URL,
for example `http://192.168.1.42:5190`. If automatic network selection chooses a
VPN or virtual adapter, pass the Wi-Fi address explicitly:

```powershell
.\apps\backend-api\scripts\start-lan.ps1 -LanAddress 192.168.1.42
```

Confirm it from the phone's browser with
`http://<LAN-IP>:5190/health/ready`. If Windows Firewall blocks the request,
allow the .NET host on the Private network profile. Do not expose port 5190 on a
public network or router.

Copy the mobile example and replace its sample address with the printed URL:

```powershell
Copy-Item apps/passenger-mobile/.env.example apps/passenger-mobile/.env.local
Copy-Item apps/driver-mobile/.env.example apps/driver-mobile/.env.local
```

Then start either Expo app in LAN mode:

```powershell
Set-Location apps/passenger-mobile
npm run start:lan
```

Expo replaces `EXPO_PUBLIC_` references in the app bundle. After changing one
of these variables, restart Expo and clear its cache if the old value remains:

```powershell
npx expo start --clear --lan
```

## Use an HTTPS development tunnel

A tunnel is useful when the phone is not on the same network and for external
callbacks. Start the API, then use one of these separately installed tools:

```powershell
cloudflared tunnel --url http://localhost:5190
```

```powershell
ngrok http 5190
```

Set `EXPO_PUBLIC_API_BASE_URL` in each app's ignored `.env.local` file to the
temporary HTTPS URL. Tunnel URLs are ephemeral unless a reserved hostname is
configured, so they are development-only and must not be baked into staging or
production builds.

## Mobile build environments

Both Expo apps contain `development`, `staging`, and `production` EAS build
profiles. EAS provides environment sets named `development`, `preview`, and
`production`, so the RYDO `staging` profile intentionally reads EAS's `preview`
set.

After each app is linked to its own EAS project with `npx eas-cli@latest init`,
set the public API URL from that app directory:

```powershell
npx eas-cli@latest env:set --name EXPO_PUBLIC_API_BASE_URL --value https://api-staging.rydo.co.za --environment preview --visibility plaintext
npx eas-cli@latest env:set --name EXPO_PUBLIC_API_BASE_URL --value https://api.rydo.co.za --environment production --visibility plaintext
```

Create internal staging and store-ready production builds with:

```powershell
npx eas-cli@latest build --profile staging
npx eas-cli@latest build --profile production
```

The checked-in `.env.*.example` files document the same targets for local
builds. Copy the desired example to `.env.local`; never rename an example into a
tracked secrets file.

## Admin dashboard environments

For local work, copy `apps/admin-dashboard/.env.example` to `.env.local`. The
dashboard can continue using `http://localhost:5190` because it runs in the
development computer's browser.

For hosted builds, provide `VITE_APP_ENV` and `VITE_API_BASE_URL` in the hosting
platform. The checked-in staging and production examples document the required
values. To validate a staging bundle locally, first copy
`.env.staging.example` to the ignored `.env.staging`, then run
`npm run build:staging`. The normal `npm run build` command uses Vite's
production mode.

## API hosting contract

Run the hosted API with `ASPNETCORE_ENVIRONMENT=Staging` or `Production`, and
provide at least these server-side environment variables through the host's
secret manager:

- `ConnectionStrings__RydoDatabase`
- `Authentication__SigningKey`
- `Authentication__OtpPepper`

The checked-in ASP.NET Core environment files restrict host names and reserve
`https://admin-staging.rydo.co.za` and `https://admin.rydo.co.za` as dashboard
CORS origins. Override `Clients__AllowedOrigins__0` in the host configuration if
the dashboard is deployed at a different URL.

The hosting platform must terminate HTTPS, forward traffic to the API's HTTP
port 8080, and probe `/health/live` for liveness and `/health/ready` for database
readiness. `apps/backend-api/Dockerfile` packages this contract and is built by
the pull-request quality gates. Complete the environment in this order:

1. Choose and provision the managed API host.
2. Deploy `apps/backend-api` to separate staging and production services.
3. Add DNS records for `api-staging.rydo.co.za` and `api.rydo.co.za`.
4. Enable managed TLS and verify both health endpoints over HTTPS.
5. Configure EAS and dashboard variables only after those endpoints are live.
