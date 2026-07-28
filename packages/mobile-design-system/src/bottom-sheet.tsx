import BottomSheet, { BottomSheetView } from '@gorhom/bottom-sheet';
import type { ReactNode } from 'react';
import { useMemo } from 'react';
import type { ViewStyle } from 'react-native';

import { colors, radii, shadows, spacing } from './tokens';

export interface RydoBottomSheetProps {
  children: ReactNode;
  index?: number;
  snapPoints?: Array<number | `${number}%`>;
  bottomInset?: number;
  contentStyle?: ViewStyle;
  onChange?(index: number): void;
}

export function RydoBottomSheet({
  children,
  index = 0,
  snapPoints = ['38%', '68%'],
  bottomInset = spacing.md,
  contentStyle,
  onChange,
}: RydoBottomSheetProps) {
  const stableSnapPoints = useMemo(() => snapPoints, [snapPoints]);

  return (
    <BottomSheet
      index={index}
      snapPoints={stableSnapPoints}
      bottomInset={bottomInset}
      detached
      enableDynamicSizing={false}
      enablePanDownToClose={false}
      keyboardBehavior="interactive"
      keyboardBlurBehavior="restore"
      android_keyboardInputMode="adjustResize"
      backgroundStyle={{ backgroundColor: colors.glass, borderRadius: radii.sheet, ...shadows.floating }}
      handleIndicatorStyle={{ width: 42, backgroundColor: colors.border }}
      style={{ marginHorizontal: spacing.md }}
      onChange={onChange}
    >
      <BottomSheetView style={[{ flex: 1, paddingHorizontal: spacing.lg, paddingBottom: spacing.lg, gap: spacing.sm }, contentStyle]}>
        {children}
      </BottomSheetView>
    </BottomSheet>
  );
}
