# Maps and location

RYDO uses native Google map rendering in the Passenger and Driver development
builds. Places, Routes, and reverse geocoding are called through authenticated,
rate-limited API endpoints so a Google web-service key is never bundled in
mobile JavaScript.

## Google Cloud projects and keys

Create separate credentials for development, staging, and production. For each
environment create:

1. Android SDK keys restricted to **Android apps**, the exact application ID,
   and the matching EAS/local signing certificate SHA-1. Enable only Maps SDK
   for Android.
2. iOS SDK keys restricted to **iOS apps** and the exact bundle identifier.
   Enable only Maps SDK for iOS.
3. A backend server key enabling Places API (New), Routes API, and Geocoding
   API. Restrict it to the production or staging API's fixed egress IP addresses
   once hosting is selected. Never use this key in an `EXPO_PUBLIC_` variable.

The identifiers that need restrictions are:

| App | Development | Staging | Production |
| --- | --- | --- | --- |
| Passenger | `za.co.rydo.passenger.dev` | `za.co.rydo.passenger.staging` | `za.co.rydo.passenger` |
| Driver | `za.co.rydo.driver.dev` | `za.co.rydo.driver.staging` | `za.co.rydo.driver` |

Add `GOOGLE_MAPS_ANDROID_API_KEY` and `GOOGLE_MAPS_IOS_API_KEY` to each mobile
EAS environment. Configure the backend server credential as
`GoogleMaps__ServerApiKey` in the managed API host. Native SDK keys can be
extracted from a binary; app and API restrictions are the security boundary.

## Permission behaviour

The Passenger app asks for foreground permission only after **Use my
location** is pressed. Search and manually pinned pickup/destination selection
remain available without that permission.

The Driver app asks for foreground permission first. It explains background
tracking before asking for the platform's background permission, and starts the
registered background task only after approval. Tracking stops when the driver
presses **Go offline**. Android 11 or newer may open system Settings; iOS users
must choose **Always**. Background tracking must be tested in an EAS development
build, not Expo Go.

## API endpoints

- `GET /api/v1/maps/places/autocomplete`
- `GET /api/v1/maps/places/{placeId}`
- `GET /api/v1/maps/geocode/reverse`
- `POST /api/v1/maps/routes`

All endpoints require a valid RYDO JWT and share the `maps` rate limit. Places
autocomplete and place details use one client-generated session token per
selection session to preserve Google billing semantics.
