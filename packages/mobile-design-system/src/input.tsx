import { forwardRef, type ComponentProps } from 'react';
import { Text, TextInput, View } from 'react-native';

import { BottomSheetCompatibleTextInput } from './bottom-sheet-text-input';
import { colors, radii, spacing, typography } from './tokens';

export interface RydoTextInputProps extends ComponentProps<typeof TextInput> {
  label?: string;
  error?: string;
  helperText?: string;
}

export const RydoTextInput = forwardRef<TextInput, RydoTextInputProps>(function RydoTextInput(
  { label, error, helperText, style, editable = true, ...props },
  ref,
) {
  const supportingText = error ?? helperText;

  return (
    <View style={{ gap: spacing.xs }}>
      {label ? (
        <Text selectable style={{ color: colors.text, fontSize: typography.size.caption, fontWeight: typography.weight.bold }}>
          {label}
        </Text>
      ) : null}
      <TextInput
        ref={ref}
        editable={editable}
        placeholderTextColor={colors.textMuted}
        selectionColor={colors.blue}
        style={[
          {
            minHeight: 50,
            borderCurve: 'continuous',
            borderRadius: radii.md,
            borderWidth: 1,
            borderColor: error ? colors.danger : colors.border,
            backgroundColor: colors.surfaceElevated,
            color: colors.text,
            fontSize: typography.size.body,
            paddingHorizontal: spacing.lg,
            paddingVertical: spacing.md,
            opacity: editable ? 1 : 0.55,
          },
          style,
        ]}
        {...props}
      />
      {supportingText ? (
        <Text selectable style={{ color: error ? colors.danger : colors.textMuted, fontSize: typography.size.caption }}>
          {supportingText}
        </Text>
      ) : null}
    </View>
  );
});

export function RydoBottomSheetTextInput({
  label,
  error,
  helperText,
  style,
  editable = true,
  ...props
}: RydoTextInputProps) {
  const supportingText = error ?? helperText;

  return (
    <View style={{ gap: spacing.xs }}>
      {label ? (
        <Text selectable style={{ color: colors.text, fontSize: typography.size.caption, fontWeight: typography.weight.bold }}>
          {label}
        </Text>
      ) : null}
      <BottomSheetCompatibleTextInput
        editable={editable}
        placeholderTextColor={colors.textMuted}
        selectionColor={colors.blue}
        style={[
          {
            minHeight: 50,
            borderRadius: radii.md,
            borderWidth: 1,
            borderColor: error ? colors.danger : colors.border,
            backgroundColor: colors.surfaceElevated,
            color: colors.text,
            fontSize: typography.size.body,
            paddingHorizontal: spacing.lg,
            paddingVertical: spacing.md,
            opacity: editable ? 1 : 0.55,
          },
          style,
        ]}
        {...props}
      />
      {supportingText ? (
        <Text selectable style={{ color: error ? colors.danger : colors.textMuted, fontSize: typography.size.caption }}>
          {supportingText}
        </Text>
      ) : null}
    </View>
  );
}
