# Frontend architecture

RYDO has three intentionally separate user surfaces:

- Passenger mobile app for authentication, journey planning, ride requests,
  driver tracking, trip completion, and ratings.
- Driver mobile app for onboarding, availability, ride decisions, navigation,
  active-trip controls, and trip history.
- Admin web dashboard for users, drivers, verification, trips, payments,
  disputes, live activity, and operational data.

The mobile apps use React Native, Expo, Expo Router, and TypeScript. They follow
a map-first interaction model with floating controls, draggable bottom sheets,
large touch targets, restrained motion, and a shared RYDO design system.

Passenger and Driver remain separate applications with independent identities,
permissions, builds, and releases. Reuse is limited to deliberate packages such
as design tokens, UI primitives, API clients, and shared TypeScript contracts.
