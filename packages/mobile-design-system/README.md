# RYDO mobile design system

Shared React Native primitives for the passenger and driver applications. The package owns brand tokens and reusable map-first UI while each app owns its navigation and domain workflow.

## Components

- `RydoButton` and `RydoTextInput`
- `MapControl` and `RydoIcon`
- `RideCard`
- `RydoBottomSheet`
- `LoadingState`, `ErrorState`, and `EmptyState`

The icon component uses SF Symbols through `expo-image` on iOS and lightweight text fallbacks elsewhere. Bottom sheets use Gorhom Bottom Sheet with interactive keyboard handling and Android resize behavior.
