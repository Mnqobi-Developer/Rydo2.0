import { isApiError, type OtpRequestResult } from '@rydo/mobile-api-client';
import { RydoIcon, colors, spacing, typography } from '@rydo/mobile-design-system';
import { useMutation } from '@tanstack/react-query';
import { Image } from 'expo-image';
import { useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { KeyboardAwareScrollView } from 'react-native-keyboard-controller';
import Animated, { FadeInRight, FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiClient } from '@/api';
import { driverTheme } from '@/theme/driver-theme';

type AuthStep = 'phone' | 'code';

export function DriverSignInScreen() {
  const insets = useSafeAreaInsets();
  const [step, setStep] = useState<AuthStep>('phone');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [code, setCode] = useState('');
  const [challenge, setChallenge] = useState<OtpRequestResult | null>(null);

  const requestOtp = useMutation({
    mutationFn: (phone: string) => apiClient.auth.requestOtp({ phoneNumber: phone, role: 'Driver' }),
    onSuccess: (result) => {
      setChallenge(result);
      setStep('code');
    },
  });

  const verifyOtp = useMutation({
    mutationFn: () => apiClient.auth.verifyOtp({ challengeId: challenge!.challengeId, code: code.trim() }),
  });

  const activeError = requestOtp.error ?? verifyOtp.error;
  const errorMessage = activeError
    ? isApiError(activeError)
      ? activeError.problem?.detail ?? activeError.message
      : 'Something went wrong. Please try again.'
    : undefined;

  const normalizedPhone = normalizeSouthAfricanPhone(phoneNumber);
  const pending = requestOtp.isPending || verifyOtp.isPending;
  const disabled = step === 'phone' ? !normalizedPhone : code.length < 4;

  function submitPhone() {
    if (!normalizedPhone) return;
    requestOtp.reset();
    requestOtp.mutate(normalizedPhone);
  }

  return (
    <KeyboardAwareScrollView
      bottomOffset={spacing.xl}
      contentContainerStyle={[
        styles.content,
        { paddingTop: insets.top + spacing.xxl, paddingBottom: insets.bottom + spacing.xxl },
      ]}
      contentInsetAdjustmentBehavior="automatic"
      keyboardDismissMode="interactive"
      keyboardShouldPersistTaps="handled"
      mode="insets"
    >
      <Animated.View entering={FadeInUp.duration(260)} style={styles.brandArea}>
        <Image
          accessibilityLabel="Rydo Driver"
          contentFit="contain"
          source={require('../../../assets/images/icon.png')}
          style={styles.logo}
        />
        <Text style={styles.wordmark}>Rydo Driver</Text>
      </Animated.View>

      <Animated.View key={step} entering={FadeInRight.duration(220)} style={styles.formArea}>
        <View style={styles.headingArea}>
          <Text selectable style={styles.title}>
            {step === 'phone' ? 'Sign in or create an account' : 'Check your phone'}
          </Text>
          <Text selectable style={styles.subtitle}>
            {step === 'phone'
              ? 'Use your mobile number to access RYDO Driver. New Drivers will create a profile after verification.'
              : `Enter the code sent to ${normalizedPhone ?? phoneNumber}.`}
          </Text>
        </View>

        {step === 'phone' ? (
          <View style={styles.fieldGroup}>
            <View style={styles.phoneField}>
              <View style={styles.iconTile}><RydoIcon name="phone" color={colors.blue} size={22} /></View>
              <Text style={styles.countryCode}>+27</Text>
              <View style={styles.divider} />
              <TextInput
                accessibilityLabel="South African mobile number"
                autoComplete="tel"
                keyboardType="phone-pad"
                onChangeText={(value) => setPhoneNumber(value.replace(/\D/g, '').slice(0, 9))}
                onSubmitEditing={submitPhone}
                placeholder="Mobile number"
                placeholderTextColor="#7C8899"
                returnKeyType="send"
                selectionColor={colors.blue}
                style={styles.phoneInput}
                textContentType="telephoneNumber"
                value={phoneNumber}
              />
            </View>
            {phoneNumber.length > 3 && !normalizedPhone ? (
              <Text style={styles.errorText}>Enter a valid South African mobile number.</Text>
            ) : errorMessage ? <Text style={styles.errorText}>{errorMessage}</Text> : null}
          </View>
        ) : (
          <View style={styles.fieldGroup}>
            <View style={styles.codeField}>
              <TextInput
                autoFocus
                accessibilityLabel="One-time code"
                autoComplete="sms-otp"
                keyboardType="number-pad"
                maxLength={6}
                onChangeText={(value) => setCode(value.replace(/\D/g, '').slice(0, 6))}
                onSubmitEditing={() => code.length >= 4 && verifyOtp.mutate()}
                placeholder="000000"
                placeholderTextColor="#A5AFBC"
                returnKeyType="done"
                selectionColor={colors.blue}
                style={styles.codeInput}
                textContentType="oneTimeCode"
                value={code}
              />
            </View>
            {challenge?.developmentCode ? (
              <Text selectable style={styles.developmentCode}>Development code: {challenge.developmentCode}</Text>
            ) : null}
            {errorMessage ? <Text style={styles.errorText}>{errorMessage}</Text> : null}
          </View>
        )}

        <Pressable
          accessibilityRole="button"
          accessibilityState={{ busy: pending, disabled: disabled || pending }}
          disabled={disabled || pending}
          onPress={step === 'phone' ? submitPhone : () => verifyOtp.mutate()}
          style={({ pressed }) => [
            styles.primaryButton,
            (disabled || pending) && styles.primaryButtonDisabled,
            pressed && !disabled && styles.primaryButtonPressed,
          ]}
        >
          {pending ? <ActivityIndicator color={colors.white} /> : (
            <>
              <Text style={styles.primaryLabel}>{step === 'phone' ? 'Continue' : 'Verify and continue'}</Text>
              <Text aria-hidden style={styles.arrow}>→</Text>
            </>
          )}
        </Pressable>

        {step === 'code' ? (
          <Pressable
            accessibilityRole="button"
            onPress={() => {
              setStep('phone');
              setCode('');
              setChallenge(null);
              verifyOtp.reset();
            }}
            style={styles.changeNumber}
          >
            <Text style={styles.changeNumberText}>Use a different number</Text>
          </Pressable>
        ) : null}
      </Animated.View>

      <View style={styles.securityCard}>
        <View style={styles.securityIcon}><RydoIcon name="shield" color={colors.blue} size={22} /></View>
        <View style={styles.securityCopy}>
          <Text style={styles.securityTitle}>Secure driver access</Text>
          <Text style={styles.securityText}>Your session and driver permissions are protected by the RYDO API.</Text>
        </View>
      </View>
    </KeyboardAwareScrollView>
  );
}

function normalizeSouthAfricanPhone(value: string) {
  const digits = value.replace(/\D/g, '');
  const national = digits.length === 9
    ? digits
    : digits.length === 10 && digits.startsWith('0')
      ? digits.slice(1)
      : digits.length === 11 && digits.startsWith('27')
        ? digits.slice(2)
        : null;
  return national && /^[6-8]\d{8}$/.test(national) ? `+27${national}` : null;
}

const styles = StyleSheet.create({
  content: { flexGrow: 1, justifyContent: 'center', gap: 38, paddingHorizontal: spacing.xxl, backgroundColor: driverTheme.colors.background },
  brandArea: { alignItems: 'center', gap: spacing.md },
  logo: { width: 94, height: 94, borderRadius: 29 },
  wordmark: { color: colors.navy, fontSize: 34, lineHeight: 40, fontWeight: '900', letterSpacing: -1 },
  formArea: { gap: spacing.xl },
  headingArea: { alignItems: 'center', gap: spacing.sm },
  title: { color: colors.navy, fontSize: 27, lineHeight: 34, fontWeight: '900', textAlign: 'center' },
  subtitle: { maxWidth: 310, color: colors.textMuted, fontSize: 15, lineHeight: 22, textAlign: 'center' },
  fieldGroup: { gap: spacing.sm },
  phoneField: {
    minHeight: 74,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    borderWidth: 1,
    borderColor: driverTheme.colors.softBorder,
    borderCurve: 'continuous',
    borderRadius: driverTheme.radii.button,
    backgroundColor: colors.white,
    boxShadow: driverTheme.shadows.card,
  },
  iconTile: { width: 46, height: 46, alignItems: 'center', justifyContent: 'center', borderRadius: 14, backgroundColor: driverTheme.colors.softControl },
  countryCode: { paddingLeft: spacing.md, color: colors.navy, fontSize: 17, fontWeight: '800' },
  divider: { width: 1, height: 28, marginHorizontal: spacing.md, backgroundColor: colors.border },
  phoneInput: { minWidth: 0, flex: 1, paddingVertical: spacing.lg, color: colors.navy, fontSize: 17 },
  codeField: {
    minHeight: 74,
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: driverTheme.colors.softBorder,
    borderRadius: driverTheme.radii.button,
    backgroundColor: colors.white,
    boxShadow: driverTheme.shadows.card,
  },
  codeInput: { paddingVertical: spacing.md, color: colors.navy, fontSize: 27, fontWeight: '800', letterSpacing: 10, textAlign: 'center' },
  errorText: { color: colors.danger, fontSize: 12, lineHeight: 17 },
  developmentCode: { color: colors.amber, fontSize: 12, fontWeight: '800', textAlign: 'center' },
  primaryButton: { minHeight: 60, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', borderRadius: driverTheme.radii.button, backgroundColor: colors.blue, boxShadow: '0 8px 24px rgba(36,87,255,0.20)' },
  primaryButtonDisabled: { opacity: 0.48 },
  primaryButtonPressed: { transform: [{ scale: 0.985 }], backgroundColor: colors.bluePressed },
  primaryLabel: { color: colors.white, fontSize: 17, fontWeight: '800' },
  arrow: { position: 'absolute', right: spacing.xl, color: colors.white, fontSize: 27 },
  changeNumber: { alignSelf: 'center', padding: spacing.sm },
  changeNumberText: { color: colors.blue, fontSize: typography.size.body, fontWeight: '700' },
  securityCard: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: driverTheme.radii.banner, backgroundColor: colors.white, boxShadow: driverTheme.shadows.card },
  securityIcon: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderRadius: 14, backgroundColor: colors.blueMuted },
  securityCopy: { minWidth: 0, flex: 1, gap: 2 },
  securityTitle: { color: colors.navy, fontSize: 15, fontWeight: '800' },
  securityText: { color: colors.textMuted, fontSize: 12, lineHeight: 17 },
});
