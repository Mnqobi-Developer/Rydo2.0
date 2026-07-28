import { EmptyState, RydoButton, colors, spacing } from '@rydo/mobile-design-system';
import { ScrollView } from 'react-native';

export function SavedPlacesScreen() {
  return (
    <ScrollView
      contentInsetAdjustmentBehavior="automatic"
      contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: spacing.lg, paddingBottom: 110, backgroundColor: colors.surface }}
    >
      <EmptyState
        title="No saved places"
        message="Home, work, and favourite destinations will be available once the saved-places API is added."
        action={<RydoButton label="Saved places coming soon" disabled fullWidth={false} />}
      />
    </ScrollView>
  );
}
