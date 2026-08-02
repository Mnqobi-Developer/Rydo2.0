import { isApiError } from '@rydo/mobile-api-client';
import { RydoIcon, colors, spacing } from '@rydo/mobile-design-system';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { KeyboardAwareScrollView } from 'react-native-keyboard-controller';
import Animated, { FadeInRight, FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useAuthSession } from '@/auth/session';
import { DriverRideIcon } from '@/components/driver-ride-icon';
import {
  driverProfileQuery,
  saveDriverProfile,
  type UpdateDriverProfileInput,
} from '@/features/dashboard/api';
import { driverTheme } from '@/theme/driver-theme';

export function DriverAccountCreationScreen({ embedded = false }: { embedded?: boolean }) {
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const session = useAuthSession();
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const validation = validateProfile({ firstName, lastName, email });
  const createProfile = useMutation({
    mutationFn: (input: UpdateDriverProfileInput) => saveDriverProfile(input),
    onSuccess: (profile) => {
      queryClient.setQueryData(driverProfileQuery.queryKey, profile);
    },
  });

  const errorMessage = createProfile.error
    ? isApiError(createProfile.error)
      ? createProfile.error.problem?.detail ?? createProfile.error.message
      : 'Your Driver profile could not be created. Please try again.'
    : undefined;

  function submit() {
    setSubmitted(true);
    createProfile.reset();
    if (!validation.valid) return;

    createProfile.mutate({
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      email: email.trim() || null,
    });
  }

  return (
    <KeyboardAwareScrollView
      bottomOffset={spacing.xl}
      contentContainerStyle={[
        styles.content,
        {
          paddingTop: insets.top + spacing.xl,
          paddingBottom: embedded ? 116 + insets.bottom : insets.bottom + spacing.xxl,
        },
      ]}
      contentInsetAdjustmentBehavior="automatic"
      keyboardDismissMode="interactive"
      keyboardShouldPersistTaps="handled"
      mode="insets"
    >
      <Animated.View entering={FadeInUp.duration(240)} style={styles.progressHeader}>
        <View style={styles.brandMark}><DriverRideIcon color={colors.white} size={24} /></View>
        <View style={styles.progressCopy}>
          <Text selectable style={styles.eyebrow}>DRIVER ACCOUNT</Text>
          <Text selectable style={styles.progressLabel}>Secure registration</Text>
        </View>
        <Pressable accessibilityRole="button" onPress={() => void session.logout()} style={({ pressed }) => [styles.exitButton, pressed && styles.pressed]}>
          <Text style={styles.exitLabel}>Exit</Text>
        </Pressable>
      </Animated.View>

      <Animated.View entering={FadeInRight.duration(260)} style={styles.heading}>
        <Text selectable style={styles.title}>Create your Driver profile</Text>
        <Text selectable style={styles.subtitle}>Tell us who you are. Vehicle and document verification remain separate onboarding steps.</Text>
      </Animated.View>

      <View style={styles.phoneCard}>
        <View style={styles.phoneIcon}><RydoIcon name="phone" color={colors.blue} size={20} /></View>
        <View style={styles.phoneCopy}>
          <Text style={styles.fieldCaption}>VERIFIED MOBILE NUMBER</Text>
          <Text selectable style={styles.phoneNumber}>{session.user?.phoneNumber}</Text>
        </View>
        <View style={styles.verifiedBadge}><RydoIcon name="check" color={colors.white} size={12} /></View>
      </View>

      <View style={styles.formCard}>
        <ProfileField
          autoComplete="given-name"
          error={submitted ? validation.firstName : undefined}
          label="First name"
          onChangeText={setFirstName}
          placeholder="Enter your first name"
          returnKeyType="next"
          textContentType="givenName"
          value={firstName}
        />
        <ProfileField
          autoComplete="family-name"
          error={submitted ? validation.lastName : undefined}
          label="Last name"
          onChangeText={setLastName}
          placeholder="Enter your last name"
          returnKeyType="next"
          textContentType="familyName"
          value={lastName}
        />
        <ProfileField
          autoCapitalize="none"
          autoComplete="email"
          error={submitted ? validation.email : undefined}
          keyboardType="email-address"
          label="Email address (optional)"
          onChangeText={setEmail}
          onSubmitEditing={submit}
          placeholder="name@example.com"
          returnKeyType="done"
          textContentType="emailAddress"
          value={email}
        />
      </View>

      {errorMessage ? (
        <View style={styles.errorCard}>
          <RydoIcon name="error" color={colors.danger} size={18} />
          <Text selectable style={styles.errorMessage}>{errorMessage}</Text>
        </View>
      ) : null}

      <Pressable
        accessibilityRole="button"
        accessibilityState={{ busy: createProfile.isPending, disabled: createProfile.isPending }}
        disabled={createProfile.isPending}
        onPress={submit}
        style={({ pressed }) => [styles.primaryButton, createProfile.isPending && styles.primaryDisabled, pressed && styles.primaryPressed]}
      >
        {createProfile.isPending ? <ActivityIndicator color={colors.white} /> : (
          <>
            <Text style={styles.primaryLabel}>Create Driver profile</Text>
            <RydoIcon name="chevron-right" color={colors.white} size={22} />
          </>
        )}
      </Pressable>

      <View style={styles.privacyRow}>
        <RydoIcon name="shield" color={colors.textMuted} size={16} />
        <Text selectable style={styles.privacyText}>Your information is securely sent to RYDO and used for Driver verification.</Text>
      </View>
    </KeyboardAwareScrollView>
  );
}

type ProfileFieldProps = {
  autoCapitalize?: 'none' | 'sentences' | 'words' | 'characters';
  autoComplete: 'email' | 'family-name' | 'given-name';
  error?: string;
  keyboardType?: 'default' | 'email-address';
  label: string;
  onChangeText(value: string): void;
  onSubmitEditing?: () => void;
  placeholder: string;
  returnKeyType: 'done' | 'next';
  textContentType: 'emailAddress' | 'familyName' | 'givenName';
  value: string;
};

function ProfileField({ error, label, ...inputProps }: ProfileFieldProps) {
  return (
    <View style={styles.fieldGroup}>
      <Text style={styles.fieldLabel}>{label}</Text>
      <TextInput
        {...inputProps}
        accessibilityLabel={label}
        autoCapitalize={inputProps.autoCapitalize ?? 'words'}
        maxLength={label.startsWith('Email') ? 254 : 100}
        placeholderTextColor="#8A96A8"
        selectionColor={colors.blue}
        style={[styles.input, error && styles.inputError]}
      />
      {error ? <Text selectable style={styles.fieldError}>{error}</Text> : null}
    </View>
  );
}

function validateProfile({ firstName, lastName, email }: { firstName: string; lastName: string; email: string }) {
  const cleanFirstName = firstName.trim();
  const cleanLastName = lastName.trim();
  const cleanEmail = email.trim();
  const firstNameError = cleanFirstName.length < 2 ? 'Enter at least 2 characters.' : undefined;
  const lastNameError = cleanLastName.length < 2 ? 'Enter at least 2 characters.' : undefined;
  const emailError = cleanEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(cleanEmail) ? 'Enter a valid email address.' : undefined;

  return {
    valid: !firstNameError && !lastNameError && !emailError,
    firstName: firstNameError,
    lastName: lastNameError,
    email: emailError,
  };
}

const styles = StyleSheet.create({
  content: { flexGrow: 1, gap: spacing.xl, paddingHorizontal: spacing.xl, backgroundColor: driverTheme.colors.background },
  progressHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  brandMark: { width: 48, height: 48, alignItems: 'center', justifyContent: 'center', borderRadius: 16, backgroundColor: colors.blue },
  progressCopy: { minWidth: 0, flex: 1, gap: 2 },
  eyebrow: { color: colors.blue, fontSize: 10, fontWeight: '900', letterSpacing: 1.1 },
  progressLabel: { color: colors.textMuted, fontSize: 12, fontWeight: '700' },
  exitButton: { paddingHorizontal: spacing.md, paddingVertical: spacing.sm, borderRadius: 999, backgroundColor: driverTheme.colors.softControl },
  exitLabel: { color: colors.textMuted, fontSize: 12, fontWeight: '800' },
  heading: { gap: spacing.sm },
  title: { maxWidth: 330, color: colors.navy, fontSize: 32, lineHeight: 38, fontWeight: '900', letterSpacing: -0.9 },
  subtitle: { maxWidth: 350, color: colors.textMuted, fontSize: 14, lineHeight: 21 },
  phoneCard: { minHeight: 70, flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, borderRadius: driverTheme.radii.control, backgroundColor: colors.blueMuted },
  phoneIcon: { width: 40, height: 40, alignItems: 'center', justifyContent: 'center', borderRadius: 14, backgroundColor: colors.white },
  phoneCopy: { minWidth: 0, flex: 1, gap: 3 },
  fieldCaption: { color: colors.textMuted, fontSize: 9, fontWeight: '900', letterSpacing: 0.7 },
  phoneNumber: { color: colors.navy, fontSize: 15, fontWeight: '900' },
  verifiedBadge: { width: 22, height: 22, alignItems: 'center', justifyContent: 'center', borderRadius: 11, backgroundColor: driverTheme.colors.online },
  formCard: { gap: spacing.lg, padding: spacing.lg, borderRadius: driverTheme.radii.card, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  fieldGroup: { gap: spacing.sm },
  fieldLabel: { color: colors.navy, fontSize: 13, fontWeight: '800' },
  input: { minHeight: 56, paddingHorizontal: spacing.lg, borderWidth: 1, borderColor: driverTheme.colors.softBorder, borderRadius: driverTheme.radii.control, backgroundColor: driverTheme.colors.softControl, color: colors.navy, fontSize: 16 },
  inputError: { borderColor: colors.danger, backgroundColor: '#FFF8F8' },
  fieldError: { color: colors.danger, fontSize: 11, lineHeight: 16 },
  errorCard: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.sm, padding: spacing.md, borderRadius: driverTheme.radii.control, backgroundColor: '#FFF0F1' },
  errorMessage: { minWidth: 0, flex: 1, color: colors.danger, fontSize: 12, lineHeight: 18 },
  primaryButton: { minHeight: 60, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, borderRadius: driverTheme.radii.button, backgroundColor: colors.blue, boxShadow: '0 8px 24px rgba(36,87,255,0.20)' },
  primaryLabel: { color: colors.white, fontSize: 16, fontWeight: '900' },
  primaryDisabled: { opacity: 0.55 },
  primaryPressed: { transform: [{ scale: 0.985 }], backgroundColor: colors.bluePressed },
  privacyRow: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'center', gap: spacing.sm, paddingHorizontal: spacing.lg },
  privacyText: { maxWidth: 300, color: colors.textMuted, fontSize: 11, lineHeight: 17, textAlign: 'center' },
  pressed: { opacity: 0.7, transform: [{ scale: 0.97 }] },
});
