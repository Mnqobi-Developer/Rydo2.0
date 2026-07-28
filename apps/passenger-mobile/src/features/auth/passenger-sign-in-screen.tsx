import { isApiError, type OtpRequestResult } from '@rydo/mobile-api-client';
import { RydoButton, RydoIcon, RydoTextInput, colors, radii, spacing, typography } from '@rydo/mobile-design-system';
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import Animated, { FadeInRight, FadeInUp } from 'react-native-reanimated';

import { apiClient } from '@/api';

type AuthStep = 'phone' | 'code';

export function PassengerSignInScreen() {
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

  return (
    <ScrollView
      automaticallyAdjustKeyboardInsets
      contentInsetAdjustmentBehavior="automatic"
      keyboardShouldPersistTaps="handled"
      contentContainerStyle={{ flexGrow: 1, backgroundColor: colors.surface, padding: spacing.xl }}
    >
      <View style={{ flex: 1, justifyContent: 'space-between', gap: spacing.xxl, paddingVertical: spacing.xxl }}>
        <Animated.View entering={FadeInUp.duration(260)} style={{ gap: spacing.lg }}>
          <View
            style={{
              width: 66,
              height: 66,
              borderCurve: 'continuous',
              borderRadius: 22,
              alignItems: 'center',
              justifyContent: 'center',
              backgroundColor: colors.blue,
              boxShadow: '0 12px 30px rgba(18,97,216,0.28)',
            }}
          >
            <RydoIcon name="car" color={colors.white} size={32} />
          </View>
          <View style={{ gap: spacing.sm }}>
            <Text selectable style={{ color: colors.navy, fontSize: 38, lineHeight: 43, fontWeight: '900' }}>
              Your ride,
              {'\n'}right when you need it.
            </Text>
            <Text selectable style={{ color: colors.textMuted, fontSize: typography.size.body, lineHeight: 23 }}>
              Book safe, reliable local rides with RYDO.
            </Text>
          </View>
        </Animated.View>

        <Animated.View
          key={step}
          entering={FadeInRight.duration(220)}
          style={{
            borderCurve: 'continuous',
            borderRadius: radii.sheet,
            backgroundColor: colors.surfaceElevated,
            padding: spacing.xl,
            gap: spacing.lg,
            boxShadow: '0 14px 36px rgba(11,31,58,0.12)',
          }}
        >
          <View style={{ gap: spacing.xs }}>
            <Text selectable style={{ color: colors.navy, fontSize: typography.size.title, fontWeight: typography.weight.bold }}>
              {step === 'phone' ? 'Sign in or sign up' : 'Check your phone'}
            </Text>
            <Text selectable style={{ color: colors.textMuted, lineHeight: typography.lineHeight.body }}>
              {step === 'phone'
                ? 'Enter your South African mobile number. We will send a one-time code.'
                : `Enter the code sent to ${normalizeSouthAfricanPhone(phoneNumber) ?? phoneNumber}.`}
            </Text>
          </View>

          {step === 'phone' ? (
            <RydoTextInput
              accessibilityLabel="Mobile number"
              label="Mobile number"
              value={phoneNumber}
              onChangeText={setPhoneNumber}
              placeholder="+27 82 123 4567"
              keyboardType="phone-pad"
              textContentType="telephoneNumber"
              autoComplete="tel"
              returnKeyType="send"
              error={phoneNumber.length > 3 && !normalizeSouthAfricanPhone(phoneNumber) ? 'Enter a valid South African mobile number.' : errorMessage}
              onSubmitEditing={submitPhone}
            />
          ) : (
            <View style={{ gap: spacing.md }}>
              <RydoTextInput
                accessibilityLabel="One-time code"
                label="One-time code"
                value={code}
                onChangeText={(value) => setCode(value.replace(/\D/g, '').slice(0, 6))}
                placeholder="000000"
                keyboardType="number-pad"
                textContentType="oneTimeCode"
                autoComplete="sms-otp"
                maxLength={6}
                error={errorMessage}
              />
              {challenge?.developmentCode ? (
                <Text selectable style={{ color: colors.amber, fontSize: typography.size.caption, fontWeight: '700' }}>
                  Development code: {challenge.developmentCode}
                </Text>
              ) : null}
            </View>
          )}

          <RydoButton
            label={step === 'phone' ? 'Send code' : 'Continue'}
            loading={requestOtp.isPending || verifyOtp.isPending}
            disabled={step === 'phone' ? !normalizeSouthAfricanPhone(phoneNumber) : code.length < 4}
            onPress={step === 'phone' ? submitPhone : () => verifyOtp.mutate()}
          />
          {step === 'code' ? (
            <RydoButton
              label="Use a different number"
              variant="ghost"
              onPress={() => {
                setStep('phone');
                setCode('');
                setChallenge(null);
                verifyOtp.reset();
              }}
            />
          ) : null}
        </Animated.View>
      </View>
    </ScrollView>
  );
}

function normalizeSouthAfricanPhone(value: string) {
  const compact = value.replace(/[\s()-]/g, '');
  const normalized = compact.startsWith('0') ? `+27${compact.slice(1)}` : compact;
  return /^\+27[6-8]\d{8}$/.test(normalized) ? normalized : null;
}
