# Mobile design system and native interactions

RYDO's passenger and driver apps consume `@rydo/mobile-design-system` from `packages/mobile-design-system`. The package is the source of truth for brand tokens and reusable map-first interface primitives.

## Shared foundation

- Brand colors, typography, spacing, radii, and floating-surface shadows
- Animated primary, secondary, danger, and ghost buttons
- Standard and bottom-sheet-aware text inputs
- Accessible map controls and cross-platform icons
- Ride summary cards
- Draggable bottom sheets with interactive keyboard behavior
- Loading, error, and empty states

SF Symbols are rendered through `expo-image` on iOS. Android and web use lightweight fallbacks until product-specific vector assets are approved.

## Native interaction stack

Both mobile roots are wrapped in this order:

1. `GestureHandlerRootView`
2. `SafeAreaProvider`
3. `KeyboardProvider`
4. `BottomSheetModalProvider`
5. App data, authentication, real-time, and navigation providers

The apps use Expo SDK 57-compatible versions of Gesture Handler, Reanimated 4, Worklets, Keyboard Controller, and Gorhom Bottom Sheet v5. Expo's Babel preset configures Worklets automatically, so a custom Babel file is not required. Android uses `softwareKeyboardLayoutMode: resize`; bottom-sheet text fields use the package's keyboard-aware input.

Each app's Metro config resolves the file-linked package's peer imports from that app's `node_modules`. This preserves one React and native-module instance per application.

## Development build requirement

These packages contain native code. After pulling this change, create new passenger and driver development builds before testing:

```powershell
Set-Location apps/passenger-mobile
npx eas-cli@latest build --profile development --platform android

Set-Location ../driver-mobile
npx eas-cli@latest build --profile development --platform android
```

Use the equivalent `--platform ios` command for iOS. Existing development clients built before this dependency change will not include the new native modules.
