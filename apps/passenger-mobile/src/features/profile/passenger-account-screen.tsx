import { isApiError, type PassengerProfile, type UpdatePassengerProfileRequest } from '@rydo/mobile-api-client';
import { ErrorState, LoadingState, RydoButton, RydoTextInput, colors, radii, spacing, typography } from '@rydo/mobile-design-system';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';

import { useAuthSession } from '@/auth/session';

import { passengerProfileKey, passengerProfileQuery, savePassengerProfile } from './api';

export function PassengerAccountScreen() {
  const session = useAuthSession();
  const profile = useQuery(passengerProfileQuery);

  if (profile.isLoading) return <LoadingState label="Loading your profile…" />;
  if (profile.isError) {
    return <ErrorState message={profile.error.message} onRetry={() => void profile.refetch()} />;
  }

  return (
    <ProfileForm
      key={profile.data?.updatedAt ?? 'new-profile'}
      profile={profile.data}
      phoneNumber={session.user?.phoneNumber ?? ''}
      onLogout={() => void session.logout()}
    />
  );
}

function ProfileForm({
  profile,
  phoneNumber,
  onLogout,
}: {
  profile: PassengerProfile | null | undefined;
  phoneNumber: string;
  onLogout(): void;
}) {
  const queryClient = useQueryClient();
  const [firstName, setFirstName] = useState(profile?.firstName ?? '');
  const [lastName, setLastName] = useState(profile?.lastName ?? '');
  const [email, setEmail] = useState(profile?.email ?? '');
  const saveProfile = useMutation({
    mutationFn: (request: UpdatePassengerProfileRequest) => savePassengerProfile(request),
    onSuccess: (result) => queryClient.setQueryData(passengerProfileKey, result),
  });

  const errorMessage = saveProfile.error
    ? isApiError(saveProfile.error)
      ? saveProfile.error.problem?.detail ?? saveProfile.error.message
      : 'Your profile could not be saved.'
    : undefined;
  const canSave = firstName.trim().length > 0 && lastName.trim().length > 0;

  return (
    <ScrollView
      automaticallyAdjustKeyboardInsets
      contentInsetAdjustmentBehavior="automatic"
      keyboardShouldPersistTaps="handled"
      contentContainerStyle={{ padding: spacing.lg, paddingBottom: 110, gap: spacing.lg }}
    >
      <View
        style={{
          borderCurve: 'continuous',
          borderRadius: radii.lg,
          backgroundColor: colors.navy,
          padding: spacing.xl,
          gap: spacing.sm,
        }}
      >
        <Text selectable style={{ color: colors.white, fontSize: typography.size.title, fontWeight: typography.weight.bold }}>
          {profile ? `${profile.firstName} ${profile.lastName}` : 'Welcome to RYDO'}
        </Text>
        <Text selectable style={{ color: '#C8D5E8' }}>{phoneNumber}</Text>
      </View>

      <View style={{ gap: spacing.md }}>
        <Text selectable style={{ color: colors.navy, fontSize: typography.size.button, fontWeight: typography.weight.bold }}>
          Personal details
        </Text>
        <RydoTextInput label="First name" value={firstName} onChangeText={setFirstName} textContentType="givenName" autoComplete="name-given" />
        <RydoTextInput label="Last name" value={lastName} onChangeText={setLastName} textContentType="familyName" autoComplete="name-family" />
        <RydoTextInput label="Email (optional)" value={email} onChangeText={setEmail} keyboardType="email-address" autoCapitalize="none" textContentType="emailAddress" autoComplete="email" error={errorMessage} />
        {saveProfile.isSuccess ? (
          <Text selectable accessibilityLiveRegion="polite" style={{ color: colors.success, fontWeight: '700' }}>
            Profile saved.
          </Text>
        ) : null}
        <RydoButton
          label={profile ? 'Save changes' : 'Create profile'}
          loading={saveProfile.isPending}
          disabled={!canSave}
          onPress={() => saveProfile.mutate({ firstName: firstName.trim(), lastName: lastName.trim(), email: email.trim() || null })}
        />
      </View>

      <View style={{ height: 1, backgroundColor: colors.border }} />
      <RydoButton label="Sign out" variant="danger" onPress={onLogout} />
    </ScrollView>
  );
}
