# Mobile development builds

RYDO uses Expo development clients as the primary native development runtime.
Expo Go remains available for simple JavaScript-only checks, but maps,
background location, notifications, permissions, and native configuration must
be verified in a RYDO development build.

## Build variants

Passenger and Driver each resolve three independently installable native
variants:

| Profile | Display suffix | Identifier suffix | Purpose |
| --- | --- | --- | --- |
| `development` | `Dev` | `.dev` | Development client on a device or Android emulator |
| `development-simulator` | `Dev` | `.dev` | Development client for the iOS Simulator |
| `staging` | `Staging` | `.staging` | Production-like internal review build |
| `production` | none | none | App-store build |

The development profile produces an installable Android APK. Staging and
production do not include the development launcher. Dynamic `app.config.ts`
files keep names, URL schemes, Android application IDs, and iOS bundle
identifiers aligned with the selected profile.

## One-time EAS linking

An Expo account owner must link each app to a separate EAS project. Run these
commands once from both `apps/passenger-mobile` and `apps/driver-mobile`:

```powershell
npx eas-cli@latest login
npx eas-cli@latest init
npx eas-cli@latest project:info
```

Do not reuse one EAS project ID for both apps. `eas init` writes
`extra.eas.projectId` to the static app config; the dynamic config preserves it.
No Expo credentials, project IDs, signing files, or service-account files should
be copied between the Passenger and Driver projects.

Each EAS project also needs its own public API URL variables:

```powershell
npx eas-cli@latest env:create --name EXPO_PUBLIC_API_BASE_URL --value https://YOUR-STABLE-DEV-TUNNEL --environment development --visibility plaintext
npx eas-cli@latest env:create --name EXPO_PUBLIC_API_BASE_URL --value https://api-staging.rydo.co.za --environment preview --visibility plaintext
npx eas-cli@latest env:create --name EXPO_PUBLIC_API_BASE_URL --value https://api.rydo.co.za --environment production --visibility plaintext
npx eas-cli@latest env:list --environment development
```

`EXPO_PUBLIC_*` values are embedded in the client and are not secrets. Use EAS
secret file variables later for files such as Firebase service configuration;
never commit those files.

## Cloud development builds

From either mobile app directory:

```powershell
# Android physical device or emulator
npx eas-cli@latest build --platform android --profile development

# Registered iPhone
npx eas-cli@latest build --platform ios --profile development

# iOS Simulator
npx eas-cli@latest build --platform ios --profile development-simulator
```

An iPhone device build requires an Apple Developer team and a device included
in the internal distribution provisioning profile. Android development builds
produce APKs that can be installed directly.

After installing the binary, start Metro from the matching app directory:

```powershell
npm run start:lan
```

Use `npm run start:tunnel` when the phone cannot reach the development machine
over the LAN. The ignored `.env.local` file must contain the phone-accessible API
URL used by the Metro bundle.

## Local native builds

On Windows, Android can be compiled locally after Android Studio and its SDK are
installed:

```powershell
npm run android
```

iOS compilation requires macOS and Xcode:

```powershell
npm run ios
```

Native directories are generated through Continuous Native Generation and stay
ignored. Run `npm run prebuild:dev` before rebuilding when switching variants or
when native configuration has changed.

Rebuild the development client after any of these changes:

- adding or upgrading a native module;
- changing app configuration or a config plugin;
- changing maps, notification, location, or background-mode permissions;
- changing Google/Firebase service files; or
- upgrading the Expo SDK.

JavaScript and TypeScript-only changes normally require only Metro reloads.
Run `npm run start:go` only for an intentional Expo Go compatibility check.
