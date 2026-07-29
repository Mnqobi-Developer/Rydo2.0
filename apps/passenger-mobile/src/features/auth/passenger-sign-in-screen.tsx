import { isApiError, type OtpRequestResult } from '@rydo/mobile-api-client';
import { RydoIcon, colors, spacing, typography } from '@rydo/mobile-design-system';
import { useMutation } from '@tanstack/react-query';
import { Image } from 'expo-image';
import { useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';
import { KeyboardAwareScrollView } from 'react-native-keyboard-controller';
import Animated, { FadeInRight, FadeInUp } from 'react-native-reanimated';

import { apiClient } from '@/api';

type AuthStep = 'phone' | 'code';

export function PassengerSignInScreen() {
  const { height } = useWindowDimensions();
  const compact = height < 760;
  const [step, setStep] = useState<AuthStep>('phone');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [code, setCode] = useState('');
  const [challenge, setChallenge] = useState<OtpRequestResult | null>(null);

  const requestOtp = useMutation({
    mutationFn: (phone: string) => apiClient.auth.requestOtp({ phoneNumber: phone, role: 'Passenger' }),
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

  function submitPhone() {
    const normalized = normalizeSouthAfricanPhone(phoneNumber);
    if (!normalized) return;
    requestOtp.reset();
    requestOtp.mutate(normalized);
  }

  const isPending = requestOtp.isPending || verifyOtp.isPending;
  const isDisabled = step === 'phone' ? !normalizeSouthAfricanPhone(phoneNumber) : code.length < 4;

  return (
    <KeyboardAwareScrollView
      bottomOffset={spacing.xl}
      contentContainerStyle={styles.scrollContent}
      contentInsetAdjustmentBehavior="automatic"
      keyboardDismissMode="interactive"
      keyboardShouldPersistTaps="handled"
      mode="insets"
    >
      <View style={[styles.page, compact && styles.pageCompact]}>
        <Animated.View entering={FadeInUp.duration(280)} style={styles.brandBlock}>
          <Image
            accessibilityLabel="Rydo"
            contentFit="contain"
            source={require('../../../assets/images/icon.png')}
            style={[styles.logo, compact && styles.logoCompact]}
          />
          <Text style={[styles.wordmark, compact && styles.wordmarkCompact]}>Rydo</Text>
        </Animated.View>

        <Animated.View key={step} entering={FadeInRight.duration(220)} style={styles.formArea}>
          <View style={styles.headingBlock}>
            <Text selectable style={[styles.heading, compact && styles.headingCompact]}>
              {step === 'phone' ? 'Let’s get you moving' : 'Check your phone'}
            </Text>
            <Text selectable style={styles.subtitle}>
              {step === 'phone'
                ? 'Enter your mobile number to continue'
                : `Enter the code sent to ${normalizeSouthAfricanPhone(phoneNumber) ?? phoneNumber}.`}
            </Text>
          </View>

          {step === 'phone' ? (
            <View style={styles.fieldGroup}>
              <View style={styles.phoneField}>
                <View style={styles.fieldIcon}>
                  <RydoIcon name="phone" color={colors.blue} size={23} />
                </View>
                <Text style={styles.countryCode}>+27</Text>
                <Text aria-hidden style={styles.chevron}>⌄</Text>
                <View style={styles.divider} />
                <TextInput
                  accessibilityLabel="South African mobile number"
                  autoComplete="tel"
                  keyboardType="phone-pad"
                  onChangeText={(value) => setPhoneNumber(value.replace(/\D/g, '').slice(0, 9))}
                  onSubmitEditing={submitPhone}
                  placeholder="Mobile number"
                  placeholderTextColor="#7B879A"
                  returnKeyType="send"
                  selectionColor={colors.blue}
                  style={styles.phoneInput}
                  textContentType="telephoneNumber"
                  value={phoneNumber}
                />
              </View>
              {phoneNumber.length > 3 && !normalizeSouthAfricanPhone(phoneNumber) ? (
                <Text style={styles.errorText}>Enter a valid South African mobile number.</Text>
              ) : errorMessage ? (
                <Text style={styles.errorText}>{errorMessage}</Text>
              ) : null}
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
                  placeholderTextColor="#A2ACBA"
                  returnKeyType="done"
                  selectionColor={colors.blue}
                  style={styles.codeInput}
                  textContentType="oneTimeCode"
                  value={code}
                />
              </View>
              {errorMessage ? <Text style={styles.errorText}>{errorMessage}</Text> : null}
              {challenge?.developmentCode ? (
                <Text selectable style={styles.developmentCode}>
                  Development code: {challenge.developmentCode}
                </Text>
              ) : null}
            </View>
          )}

          <Pressable
            accessibilityRole="button"
            accessibilityState={{ busy: isPending, disabled: isDisabled || isPending }}
            disabled={isDisabled || isPending}
            onPress={step === 'phone' ? submitPhone : () => verifyOtp.mutate()}
            style={({ pressed }) => [
              styles.continueButton,
              (isDisabled || isPending) && styles.continueButtonDisabled,
              pressed && !isDisabled && styles.continueButtonPressed,
            ]}
          >
            {isPending ? (
              <ActivityIndicator color={colors.white} />
            ) : (
              <>
                <Text style={styles.continueLabel}>{step === 'phone' ? 'Continue' : 'Verify code'}</Text>
                <Text aria-hidden style={styles.arrow}>→</Text>
              </>
            )}
          </Pressable>

          <View style={styles.securityNote}>
            <RydoIcon name="shield" color={colors.blue} size={18} />
            <Text style={styles.securityText}>
              {step === 'phone' ? 'We’ll send you a code to verify your number' : 'Your verification code expires shortly'}
            </Text>
          </View>

          {step === 'code' ? (
            <Pressable
              accessibilityRole="button"
              onPress={() => {
                setStep('phone');
                setCode('');
                setChallenge(null);
                verifyOtp.reset();
              }}
              style={styles.changeNumberButton}
            >
              <Text style={styles.changeNumberText}>Use a different number</Text>
            </Pressable>
          ) : null}
        </Animated.View>

        {step === 'phone' ? (
          <Animated.View entering={FadeInUp.delay(100).duration(300)} style={styles.illustrationFrame}>
            <Image
              accessibilityIgnoresInvertColors
              contentFit="cover"
              contentPosition="bottom"
              source={require('../../../assets/images/sign-in-city-car.png')}
              style={[styles.illustration, compact && styles.illustrationCompact]}
            />
          </Animated.View>
        ) : (
          <View style={styles.otpSpacer} />
        )}
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
  scrollContent: { flexGrow: 1, backgroundColor: '#F8FAFE' },
  page: { flex: 1, minHeight: 760, paddingHorizontal: spacing.xxl, paddingTop: 50 },
  pageCompact: { minHeight: 680, paddingTop: spacing.xl },
  brandBlock: { alignItems: 'center' },
  logo: { width: 116, height: 116, borderRadius: 34 },
  logoCompact: { width: 88, height: 88, borderRadius: 27 },
  wordmark: {
    marginTop: spacing.sm,
    color: colors.navy,
    fontSize: 48,
    lineHeight: 54,
    fontWeight: '900',
    letterSpacing: -1.5,
  },
  wordmarkCompact: { fontSize: 38, lineHeight: 43 },
  formArea: { marginTop: spacing.xxl, gap: spacing.xl },
  headingBlock: { alignItems: 'center', gap: spacing.sm },
  heading: {
    color: colors.navy,
    fontSize: 29,
    lineHeight: 36,
    fontWeight: '900',
    letterSpacing: -0.6,
    textAlign: 'center',
  },
  headingCompact: { fontSize: 25, lineHeight: 31 },
  subtitle: { color: colors.textMuted, fontSize: typography.size.body, lineHeight: 22, textAlign: 'center' },
  fieldGroup: { gap: spacing.sm },
  phoneField: {
    minHeight: 76,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 20,
    backgroundColor: colors.white,
    boxShadow: '0 8px 24px rgba(11,31,58,0.07)',
  },
  fieldIcon: {
    width: 48,
    height: 48,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 14,
    backgroundColor: colors.blueMuted,
  },
  countryCode: { marginLeft: spacing.lg, color: colors.navy, fontSize: 18, fontWeight: '800' },
  chevron: { marginLeft: spacing.sm, color: colors.navy, fontSize: 20, fontWeight: '700' },
  divider: { width: 1, height: 28, marginHorizontal: spacing.md, backgroundColor: colors.border },
  phoneInput: { minWidth: 0, flex: 1, paddingVertical: spacing.lg, color: colors.navy, fontSize: 17 },
  codeField: {
    minHeight: 76,
    justifyContent: 'center',
    paddingHorizontal: spacing.xl,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 20,
    backgroundColor: colors.white,
    boxShadow: '0 8px 24px rgba(11,31,58,0.07)',
  },
  codeInput: {
    paddingVertical: spacing.md,
    color: colors.navy,
    fontSize: 28,
    fontWeight: '800',
    letterSpacing: 10,
    textAlign: 'center',
  },
  errorText: { color: colors.danger, fontSize: typography.size.caption, lineHeight: typography.lineHeight.caption },
  developmentCode: { color: colors.amber, fontSize: typography.size.caption, fontWeight: '700', textAlign: 'center' },
  continueButton: {
    minHeight: 62,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 16,
    backgroundColor: colors.blue,
    boxShadow: '0 10px 24px rgba(18,97,216,0.22)',
  },
  continueButtonDisabled: { opacity: 0.48 },
  continueButtonPressed: { transform: [{ scale: 0.985 }], backgroundColor: colors.bluePressed },
  continueLabel: { color: colors.white, fontSize: 18, fontWeight: '800' },
  arrow: { position: 'absolute', right: spacing.xl, color: colors.white, fontSize: 28, lineHeight: 30 },
  securityNote: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm },
  securityText: {
    color: colors.textMuted,
    fontSize: typography.size.caption,
    lineHeight: typography.lineHeight.caption,
    textAlign: 'center',
  },
  changeNumberButton: { alignSelf: 'center', paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  changeNumberText: { color: colors.blue, fontSize: typography.size.body, fontWeight: '700' },
  illustrationFrame: {
    flex: 1,
    minHeight: 190,
    marginTop: spacing.lg,
    marginHorizontal: -spacing.xxl,
    overflow: 'hidden',
  },
  illustration: { width: '100%', height: '100%', minHeight: 230 },
  illustrationCompact: { minHeight: 190 },
  otpSpacer: { flex: 1, minHeight: 80 },
});
