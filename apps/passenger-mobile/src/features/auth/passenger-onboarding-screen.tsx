import {
  isApiError,
  type PassengerProfile,
  type UpdatePassengerProfileRequest,
} from '@rydo/mobile-api-client';
import { RydoIcon, colors, spacing, typography } from '@rydo/mobile-design-system';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { KeyboardAwareScrollView } from 'react-native-keyboard-controller';
import Animated, { FadeInRight } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useAuthSession } from '@/auth/session';
import {
  passengerProfileKey,
  savePassengerProfile,
} from '@/features/profile/api';

type OnboardingStep = 'email' | 'name' | 'payment';
type PaymentPreference = 'cash' | 'card';

const STEP_NUMBER: Record<OnboardingStep, number> = {
  email: 1,
  name: 2,
  payment: 3,
};

export function PassengerOnboardingScreen() {
  const insets = useSafeAreaInsets();
  const session = useAuthSession();
  const queryClient = useQueryClient();
  const [step, setStep] = useState<OnboardingStep>('email');
  const [email, setEmail] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [paymentPreference, setPaymentPreference] = useState<PaymentPreference>('cash');
  const [savedProfile, setSavedProfile] = useState<PassengerProfile | null>(null);

  const saveProfile = useMutation({
    mutationFn: (request: UpdatePassengerProfileRequest) => savePassengerProfile(request),
    onSuccess: (profile) => {
      setSavedProfile(profile);
      setStep('payment');
    },
  });

  const normalizedEmail = email.trim().toLowerCase();
  const emailIsValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail);
  const nameIsValid = firstName.trim().length > 0 && lastName.trim().length > 0;
  const errorMessage = saveProfile.error
    ? isApiError(saveProfile.error)
      ? saveProfile.error.problem?.detail ?? saveProfile.error.message
      : 'Your profile could not be created. Please try again.'
    : undefined;

  function goBack() {
    saveProfile.reset();
    if (step === 'payment') {
      setStep('name');
      return;
    }
    if (step === 'name') {
      setStep('email');
      return;
    }
    void session.logout();
  }

  function saveIdentity() {
    if (!nameIsValid || !emailIsValid) return;
    saveProfile.mutate({
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      email: normalizedEmail,
    });
  }

  function finishOnboarding() {
    if (!savedProfile) return;
    queryClient.setQueryData(passengerProfileKey, savedProfile);
  }

  return (
    <KeyboardAwareScrollView
      bottomOffset={spacing.xl}
      contentContainerStyle={[
        styles.scrollContent,
        { paddingTop: insets.top + spacing.md, paddingBottom: insets.bottom + spacing.xl },
      ]}
      keyboardDismissMode="interactive"
      keyboardShouldPersistTaps="handled"
      mode="insets"
    >
      <View style={styles.topBar}>
        <Pressable
          accessibilityLabel={step === 'email' ? 'Return to sign in' : 'Previous step'}
          accessibilityRole="button"
          onPress={goBack}
          style={({ pressed }) => [styles.backButton, pressed && styles.pressed]}
        >
          <RydoIcon name="chevron-left" color={colors.navy} size={25} />
        </Pressable>
        <Text style={styles.progressLabel}>Step {STEP_NUMBER[step]} of 3</Text>
        <View style={styles.topBarSpacer} />
      </View>

      <View accessibilityLabel={`Step ${STEP_NUMBER[step]} of 3`} style={styles.progressTrack}>
        <View style={[styles.progressFill, { width: `${(STEP_NUMBER[step] / 3) * 100}%` }]} />
      </View>

      <Animated.View key={step} entering={FadeInRight.duration(220)} style={styles.stepContent}>
        {step === 'email' ? (
          <>
            <View style={styles.headingBlock}>
              <Text style={styles.eyebrow}>YOUR RYDO ACCOUNT</Text>
              <Text style={styles.heading}>What’s your email?</Text>
              <Text style={styles.subtitle}>
                We’ll use it for ride receipts, account updates, and support.
              </Text>
            </View>

            <View style={styles.formGroup}>
              <Text style={styles.label}>Email address</Text>
              <TextInput
                autoCapitalize="none"
                autoComplete="email"
                autoFocus
                keyboardType="email-address"
                onChangeText={setEmail}
                onSubmitEditing={() => emailIsValid && setStep('name')}
                placeholder="you@example.com"
                placeholderTextColor={colors.textMuted}
                returnKeyType="next"
                selectionColor={colors.blue}
                style={styles.input}
                textContentType="emailAddress"
                value={email}
              />
              {email.length > 3 && !emailIsValid ? (
                <Text style={styles.errorText}>Enter a valid email address.</Text>
              ) : null}
            </View>

            <PrimaryButton disabled={!emailIsValid} label="Continue" onPress={() => setStep('name')} />
          </>
        ) : null}

        {step === 'name' ? (
          <>
            <View style={styles.headingBlock}>
              <Text style={styles.eyebrow}>YOUR IDENTITY</Text>
              <Text style={styles.heading}>What’s your name?</Text>
              <Text style={styles.subtitle}>
                Enter your legal name so drivers and Rydo Support can identify your account.
              </Text>
            </View>

            <View style={styles.formGroup}>
              <Text style={styles.label}>First name</Text>
              <TextInput
                autoCapitalize="words"
                autoComplete="given-name"
                autoFocus
                maxLength={100}
                onChangeText={setFirstName}
                placeholder="First name"
                placeholderTextColor={colors.textMuted}
                returnKeyType="next"
                selectionColor={colors.blue}
                style={styles.input}
                textContentType="givenName"
                value={firstName}
              />
              <Text style={styles.label}>Last name</Text>
              <TextInput
                autoCapitalize="words"
                autoComplete="family-name"
                maxLength={100}
                onChangeText={setLastName}
                onSubmitEditing={saveIdentity}
                placeholder="Last name"
                placeholderTextColor={colors.textMuted}
                returnKeyType="done"
                selectionColor={colors.blue}
                style={styles.input}
                textContentType="familyName"
                value={lastName}
              />
              {errorMessage ? <Text style={styles.errorText}>{errorMessage}</Text> : null}
            </View>

            <PrimaryButton
              busy={saveProfile.isPending}
              disabled={!nameIsValid || saveProfile.isPending}
              label="Create profile"
              onPress={saveIdentity}
            />
          </>
        ) : null}

        {step === 'payment' ? (
          <>
            <View style={styles.headingBlock}>
              <Text style={styles.eyebrow}>PAYMENT PREFERENCE</Text>
              <Text style={styles.heading}>How will you pay?</Text>
              <Text style={styles.subtitle}>
                Cash is available now. Card payments will be enabled after PayFast setup is complete.
              </Text>
            </View>

            <View style={styles.paymentList}>
              <PaymentOption
                description="Pay the driver after your trip"
                icon="earnings"
                label="Cash"
                onPress={() => setPaymentPreference('cash')}
                selected={paymentPreference === 'cash'}
              />
              <PaymentOption
                description="Coming soon"
                disabled
                icon="card"
                label="Card / PayFast"
                onPress={() => setPaymentPreference('card')}
                selected={paymentPreference === 'card'}
              />
            </View>

            <View style={styles.paymentActions}>
              <PrimaryButton label="Continue with cash" onPress={finishOnboarding} />
              <Pressable
                accessibilityRole="button"
                onPress={finishOnboarding}
                style={({ pressed }) => [styles.laterButton, pressed && styles.pressed]}
              >
                <Text style={styles.laterLabel}>Set up payment later</Text>
              </Pressable>
            </View>
          </>
        ) : null}
      </Animated.View>
    </KeyboardAwareScrollView>
  );
}

function PrimaryButton({
  busy = false,
  disabled = false,
  label,
  onPress,
}: {
  busy?: boolean;
  disabled?: boolean;
  label: string;
  onPress(): void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ busy, disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.primaryButton,
        disabled && styles.disabled,
        pressed && !disabled && styles.primaryButtonPressed,
      ]}
    >
      {busy ? (
        <ActivityIndicator color={colors.white} />
      ) : (
        <>
          <Text style={styles.primaryLabel}>{label}</Text>
          <RydoIcon name="chevron-right" color={colors.white} size={24} style={styles.primaryIcon} />
        </>
      )}
    </Pressable>
  );
}

function PaymentOption({
  description,
  disabled = false,
  icon,
  label,
  onPress,
  selected,
}: {
  description: string;
  disabled?: boolean;
  icon: 'card' | 'earnings';
  label: string;
  onPress(): void;
  selected: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ checked: selected, disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.paymentOption,
        selected && styles.paymentOptionSelected,
        disabled && styles.paymentOptionDisabled,
        pressed && styles.pressed,
      ]}
    >
      <View style={[styles.paymentIcon, selected && styles.paymentIconSelected]}>
        <RydoIcon name={icon} color={selected ? colors.white : colors.blue} size={22} />
      </View>
      <View style={styles.paymentCopy}>
        <Text style={styles.paymentLabel}>{label}</Text>
        <Text style={styles.paymentDescription}>{description}</Text>
      </View>
      <View style={[styles.radioOuter, selected && styles.radioOuterSelected]}>
        {selected ? <View style={styles.radioInner} /> : null}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  scrollContent: {
    flexGrow: 1,
    paddingHorizontal: spacing.xl,
    backgroundColor: '#F8FAFD',
  },
  topBar: {
    minHeight: 48,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  backButton: {
    width: 44,
    height: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 15,
    backgroundColor: colors.white,
  },
  topBarSpacer: { width: 44 },
  progressLabel: { color: colors.textMuted, fontSize: 13, fontWeight: '700' },
  progressTrack: {
    height: 4,
    marginTop: spacing.md,
    overflow: 'hidden',
    borderRadius: 99,
    backgroundColor: '#E8EDF5',
  },
  progressFill: { height: '100%', borderRadius: 99, backgroundColor: colors.blue },
  stepContent: { flex: 1, paddingTop: 56, gap: spacing.xxl },
  headingBlock: { gap: spacing.sm },
  eyebrow: { color: colors.blue, fontSize: 12, fontWeight: '900', letterSpacing: 1.2 },
  heading: {
    color: colors.navy,
    fontSize: 34,
    lineHeight: 41,
    fontWeight: '900',
    letterSpacing: -0.8,
  },
  subtitle: { color: colors.textMuted, fontSize: 16, lineHeight: 24 },
  formGroup: { gap: spacing.sm },
  label: { marginTop: spacing.sm, color: colors.navy, fontSize: 14, fontWeight: '800' },
  input: {
    minHeight: 64,
    paddingHorizontal: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 18,
    backgroundColor: colors.white,
    color: colors.navy,
    fontSize: 17,
  },
  errorText: { color: colors.danger, fontSize: typography.size.caption, lineHeight: 18 },
  primaryButton: {
    minHeight: 62,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 18,
    backgroundColor: colors.blue,
  },
  primaryButtonPressed: { transform: [{ scale: 0.985 }], backgroundColor: colors.bluePressed },
  disabled: { opacity: 0.46 },
  primaryLabel: { color: colors.white, fontSize: 18, fontWeight: '800' },
  primaryIcon: { position: 'absolute', right: spacing.xl },
  paymentList: { gap: spacing.md },
  paymentOption: {
    minHeight: 84,
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 22,
    backgroundColor: colors.white,
  },
  paymentOptionSelected: { borderColor: colors.blue, backgroundColor: colors.blueMuted },
  paymentOptionDisabled: { opacity: 0.55 },
  paymentIcon: {
    width: 50,
    height: 50,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 16,
    backgroundColor: colors.blueMuted,
  },
  paymentIconSelected: { backgroundColor: colors.blue },
  paymentCopy: { minWidth: 0, flex: 1, gap: 3 },
  paymentLabel: { color: colors.navy, fontSize: 17, fontWeight: '800' },
  paymentDescription: { color: colors.textMuted, fontSize: 13, lineHeight: 18 },
  radioOuter: {
    width: 22,
    height: 22,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 2,
    borderColor: colors.border,
    borderRadius: 11,
  },
  radioOuterSelected: { borderColor: colors.blue },
  radioInner: { width: 10, height: 10, borderRadius: 5, backgroundColor: colors.blue },
  paymentActions: { marginTop: 'auto', gap: spacing.md },
  laterButton: { alignItems: 'center', paddingVertical: spacing.md },
  laterLabel: { color: colors.blue, fontSize: 15, fontWeight: '800' },
  pressed: { opacity: 0.74, transform: [{ scale: 0.985 }] },
});
