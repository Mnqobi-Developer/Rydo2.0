import { isApiError, type PassengerProfile, type UpdatePassengerProfileRequest } from '@rydo/mobile-api-client';
import {
  ErrorState,
  LoadingState,
  RydoButton,
  RydoIcon,
  RydoTextInput,
  colors,
  radii,
  spacing,
  typography,
} from '@rydo/mobile-design-system';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Image } from 'expo-image';
import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { KeyboardAwareScrollView } from 'react-native-keyboard-controller';
import Animated, { FadeInRight, FadeInUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useAuthSession } from '@/auth/session';

import { passengerProfileKey, passengerProfileQuery, savePassengerProfile } from './api';

type AccountView = 'overview' | 'profile' | 'payment' | 'support' | 'safety' | 'promotions' | 'settings';
type AccountIconName = 'profile' | 'payment' | 'support' | 'safety' | 'settings' | 'promotions';

export function PassengerAccountScreen() {
  const session = useAuthSession();
  const profile = useQuery(passengerProfileQuery);

  if (profile.isLoading) return <LoadingState label="Loading your profile…" />;
  if (profile.isError) {
    return <ErrorState message={profile.error.message} onRetry={() => void profile.refetch()} />;
  }

  return (
    <AccountContent
      key={profile.data?.updatedAt ?? 'new-profile'}
      profile={profile.data}
      phoneNumber={session.user?.phoneNumber ?? ''}
      onLogout={() => void session.logout()}
    />
  );
}

function AccountContent({
  profile,
  phoneNumber,
  onLogout,
}: {
  profile: PassengerProfile | null | undefined;
  phoneNumber: string;
  onLogout(): void;
}) {
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const [activeView, setActiveView] = useState<AccountView>('overview');
  const [firstName, setFirstName] = useState(profile?.firstName ?? '');
  const [lastName, setLastName] = useState(profile?.lastName ?? '');
  const [email, setEmail] = useState(profile?.email ?? '');

  const saveProfile = useMutation({
    mutationFn: (request: UpdatePassengerProfileRequest) => savePassengerProfile(request),
    onSuccess: (result) => {
      queryClient.setQueryData(passengerProfileKey, result);
      setActiveView('overview');
    },
  });

  const errorMessage = saveProfile.error
    ? isApiError(saveProfile.error)
      ? saveProfile.error.problem?.detail ?? saveProfile.error.message
      : 'Your profile could not be saved.'
    : undefined;

  const displayName = profile ? `${profile.firstName} ${profile.lastName}`.trim() : 'Rydo passenger';
  const initials = profile
    ? `${profile.firstName.charAt(0)}${profile.lastName.charAt(0)}`.toUpperCase()
    : 'R';
  const suggestionCount = [!profile?.firstName, !profile?.lastName, !profile?.email].filter(Boolean).length;
  const canSave = firstName.trim().length > 0 && lastName.trim().length > 0;
  const greeting = getGreeting();

  return (
    <KeyboardAwareScrollView
      bottomOffset={spacing.xl}
      contentContainerStyle={[
        styles.content,
        { paddingTop: insets.top + spacing.lg, paddingBottom: 118 + insets.bottom },
      ]}
      contentInsetAdjustmentBehavior="automatic"
      keyboardDismissMode="interactive"
      keyboardShouldPersistTaps="handled"
      mode="insets"
    >
      {activeView === 'overview' ? (
        <Animated.View entering={FadeInUp.duration(240)} style={styles.screenGap}>
          <View style={styles.profileHeader}>
            <View style={styles.identityBlock}>
              <Text selectable style={styles.greeting}>{greeting},</Text>
              <Text
                adjustsFontSizeToFit
                minimumFontScale={0.76}
                numberOfLines={1}
                selectable
                style={styles.displayName}
              >
                {displayName}
              </Text>
              <View style={styles.verifiedRow}>
                <Image
                  accessibilityIgnoresInvertColors
                  contentFit="contain"
                  source={require('../../../assets/icons/account/verified.png')}
                  style={styles.verifiedBadge}
                />
                <Text selectable style={styles.verifiedText}>Verified passenger</Text>
              </View>
            </View>
            <View style={styles.headerActions}>
              <Pressable
                accessibilityLabel="Edit profile"
                accessibilityRole="button"
                onPress={() => setActiveView('profile')}
                style={({ pressed }) => [styles.avatarButton, pressed && styles.pressed]}
              >
                <Text style={styles.avatarInitials}>{initials}</Text>
                <View style={styles.statusDot} />
              </Pressable>
              <Pressable
                accessibilityLabel="Open settings"
                accessibilityRole="button"
                android_ripple={{ color: 'rgba(18,97,216,0.10)', borderless: true }}
                onPress={() => setActiveView('settings')}
                style={({ pressed }) => [styles.settingsButton, pressed && styles.pressed]}
              >
                <AccountAssetIcon name="settings" size={25} />
              </Pressable>
            </View>
          </View>

          <Animated.View entering={FadeInUp.delay(90).duration(260)}>
            <Pressable
              accessibilityRole="button"
              android_ripple={{ color: 'rgba(23,138,85,0.10)' }}
              onPress={() => setActiveView('profile')}
              style={({ pressed }) => [styles.statusCard, pressed && styles.cardPressed]}
            >
              <View style={styles.statusIcon}>
                <RydoIcon name={suggestionCount > 0 ? 'person' : 'check'} color={colors.white} size={19} />
              </View>
              <View style={styles.statusCopy}>
                <Text style={styles.statusTitle}>
                  {suggestionCount > 0 ? 'Complete your profile' : 'Profile complete'}
                </Text>
                <Text style={styles.statusSubtitle}>
                  {suggestionCount > 0
                    ? `${suggestionCount} ${suggestionCount === 1 ? 'detail' : 'details'} left to add.`
                    : 'Everything is ready to ride.'}
                </Text>
              </View>
              <RydoIcon name="chevron-right" color="#6E826F" size={20} />
            </Pressable>
          </Animated.View>

          <Animated.View entering={FadeInUp.delay(140).duration(260)} style={styles.menuCard}>
            <MenuRow icon="profile" label="Profile" onPress={() => setActiveView('profile')} />
            <MenuRow icon="payment" label="Payment" onPress={() => setActiveView('payment')} />
            <MenuRow icon="support" label="Support" onPress={() => setActiveView('support')} />
            <MenuRow icon="safety" label="Safety" onPress={() => setActiveView('safety')} />
            <MenuRow icon="settings" label="Settings" onPress={() => setActiveView('settings')} />
          </Animated.View>

          <Animated.View entering={FadeInUp.delay(190).duration(260)} style={styles.menuCard}>
            <MenuRow
              icon="promotions"
              label="Promotions"
              description="Promo codes, offers, and savings"
              onPress={() => setActiveView('promotions')}
            />
          </Animated.View>
        </Animated.View>
      ) : (
        <Animated.View key={activeView} entering={FadeInRight.duration(220)} style={styles.screenGap}>
          <DetailHeader title={detailTitles[activeView]} onBack={() => setActiveView('overview')} />

          {activeView === 'profile' ? (
            <View style={styles.detailCard}>
              <View style={styles.detailsHeading}>
                <View style={styles.detailIconCircle}>
                  <AccountAssetIcon name="profile" size={25} />
                </View>
                <View style={styles.detailHeadingCopy}>
                  <Text style={styles.detailTitle}>Personal details</Text>
                  <Text style={styles.detailDescription}>Keep your account information up to date.</Text>
                </View>
              </View>
              <RydoTextInput
                autoComplete="name-given"
                label="First name"
                onChangeText={setFirstName}
                textContentType="givenName"
                value={firstName}
              />
              <RydoTextInput
                autoComplete="name-family"
                label="Last name"
                onChangeText={setLastName}
                textContentType="familyName"
                value={lastName}
              />
              <RydoTextInput
                autoCapitalize="none"
                autoComplete="email"
                error={errorMessage}
                keyboardType="email-address"
                label="Email (optional)"
                onChangeText={setEmail}
                textContentType="emailAddress"
                value={email}
              />
              <View style={styles.readOnlyField}>
                <Text style={styles.readOnlyLabel}>Mobile number</Text>
                <Text selectable style={styles.readOnlyValue}>{phoneNumber || 'Not available'}</Text>
              </View>
              <RydoButton
                disabled={!canSave}
                label={profile ? 'Save changes' : 'Create profile'}
                loading={saveProfile.isPending}
                onPress={() => saveProfile.mutate({
                  firstName: firstName.trim(),
                  lastName: lastName.trim(),
                  email: email.trim() || null,
                })}
              />
            </View>
          ) : null}

          {activeView === 'payment' ? (
            <InfoPanel
              icon="payment"
              title="Payment methods"
              description="Cash is available for your rides. Secure card payments will appear here when PayFast activation is complete."
              eyebrow="CURRENT METHOD"
              value="Cash"
            />
          ) : null}

          {activeView === 'support' ? (
            <InfoPanel
              icon="support"
              title="How can we help?"
              description="For assistance with a ride, open the trip from your Trips tab. Your trip details help support resolve an issue faster."
              eyebrow="SUPPORT"
              value="Ride and account assistance"
            />
          ) : null}

          {activeView === 'safety' ? (
            <InfoPanel
              icon="safety"
              title="Your safety matters"
              description="RYDO keeps an authoritative trip record and verified account details. Emergency and live trip-sharing tools will be added before production launch."
              eyebrow="ACCOUNT STATUS"
              value="Phone number verified"
            />
          ) : null}

          {activeView === 'promotions' ? (
            <InfoPanel
              icon="promotions"
              title="Promotions"
              description="You do not have any active promotions yet. Eligible offers and promo-code redemption will appear here."
              eyebrow="ACTIVE OFFERS"
              value="0 promotions"
            />
          ) : null}

          {activeView === 'settings' ? (
            <View style={styles.detailCard}>
              <View style={styles.detailsHeading}>
                <View style={styles.detailIconCircle}>
                  <AccountAssetIcon name="settings" size={25} />
                </View>
                <View style={styles.detailHeadingCopy}>
                  <Text style={styles.detailTitle}>Account settings</Text>
                  <Text style={styles.detailDescription}>Manage this device and your RYDO session.</Text>
                </View>
              </View>
              <View style={styles.readOnlyField}>
                <Text style={styles.readOnlyLabel}>Signed in as</Text>
                <Text selectable style={styles.readOnlyValue}>{phoneNumber || displayName}</Text>
              </View>
              <RydoButton label="Sign out" variant="danger" onPress={onLogout} />
            </View>
          ) : null}
        </Animated.View>
      )}
    </KeyboardAwareScrollView>
  );
}

const detailTitles: Record<Exclude<AccountView, 'overview'>, string> = {
  profile: 'Profile',
  payment: 'Payment',
  support: 'Support',
  safety: 'Safety',
  promotions: 'Promotions',
  settings: 'Settings',
};

function getGreeting() {
  const hour = new Date().getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}

function MenuRow({
  icon,
  label,
  description,
  onPress,
}: {
  icon: AccountIconName;
  label: string;
  description?: string;
  onPress(): void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      android_ripple={{ color: 'rgba(18,97,216,0.08)' }}
      onPress={onPress}
      style={({ pressed }) => [styles.menuRow, pressed && styles.menuRowPressed]}
    >
      <View style={styles.menuIcon}>
        <AccountAssetIcon name={icon} size={25} />
      </View>
      <View style={styles.menuCopy}>
        <Text style={styles.menuLabel}>{label}</Text>
        {description ? <Text style={styles.menuDescription}>{description}</Text> : null}
      </View>
      <RydoIcon name="chevron-right" color="#8B95A3" size={22} />
    </Pressable>
  );
}

function DetailHeader({ title, onBack }: { title: string; onBack(): void }) {
  return (
    <View style={styles.detailHeader}>
      <Pressable
        accessibilityLabel="Back to account"
        accessibilityRole="button"
        android_ripple={{ color: 'rgba(18,97,216,0.10)', borderless: true }}
        onPress={onBack}
        style={({ pressed }) => [styles.backButton, pressed && styles.pressed]}
      >
        <Text aria-hidden style={styles.backArrow}>‹</Text>
      </Pressable>
      <Text style={styles.detailHeaderTitle}>{title}</Text>
      <View style={styles.headerBalance} />
    </View>
  );
}

function InfoPanel({
  icon,
  title,
  description,
  eyebrow,
  value,
}: {
  icon: AccountIconName;
  title: string;
  description: string;
  eyebrow: string;
  value: string;
}) {
  return (
    <View style={styles.detailCard}>
      <View style={styles.infoHeroIcon}>
        <AccountAssetIcon name={icon} size={34} />
      </View>
      <View style={styles.infoCopy}>
        <Text style={styles.infoTitle}>{title}</Text>
        <Text style={styles.infoDescription}>{description}</Text>
      </View>
      <View style={styles.infoValueCard}>
        <Text style={styles.infoEyebrow}>{eyebrow}</Text>
        <Text selectable style={styles.infoValue}>{value}</Text>
      </View>
    </View>
  );
}

const accountIconSources = {
  profile: require('../../../assets/icons/account/profile.png'),
  payment: require('../../../assets/icons/account/payment.png'),
  support: require('../../../assets/icons/account/support.png'),
  safety: require('../../../assets/icons/account/safety.png'),
  settings: require('../../../assets/icons/account/settings.png'),
  promotions: require('../../../assets/icons/account/promotions.png'),
} as const;

function AccountAssetIcon({ name, size }: { name: AccountIconName; size: number }) {
  return (
    <Image
      accessibilityIgnoresInvertColors
      contentFit="contain"
      source={accountIconSources[name]}
      style={{ width: size, height: size, tintColor: colors.blue }}
    />
  );
}

const styles = StyleSheet.create({
  content: { paddingHorizontal: spacing.xl, gap: spacing.xxl, backgroundColor: '#F8FAFD' },
  screenGap: { gap: spacing.xxl },
  profileHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md, paddingVertical: spacing.md },
  identityBlock: { minWidth: 0, flex: 1, gap: 3 },
  greeting: { color: colors.textMuted, fontSize: 14, lineHeight: 19, fontWeight: '500' },
  displayName: { color: colors.navy, fontSize: 34, lineHeight: 40, fontWeight: '900', letterSpacing: -1.1 },
  verifiedRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingTop: 3 },
  verifiedBadge: {
    width: 20,
    height: 20,
  },
  verifiedText: { color: colors.textMuted, fontSize: typography.size.body, fontWeight: '700' },
  headerActions: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  avatarButton: {
    width: 62,
    height: 62,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: '#DCE5F1',
    borderRadius: 31,
    backgroundColor: colors.blueMuted,
  },
  avatarInitials: { color: colors.navy, fontSize: 22, fontWeight: '900' },
  statusDot: {
    position: 'absolute',
    right: -2,
    bottom: 4,
    width: 14,
    height: 14,
    borderWidth: 2,
    borderColor: '#F8FAFE',
    borderRadius: 7,
    backgroundColor: colors.blue,
  },
  settingsButton: {
    width: 46,
    height: 46,
    alignItems: 'center',
    justifyContent: 'center',
    borderCurve: 'continuous',
    borderRadius: 15,
    backgroundColor: colors.white,
    boxShadow: '0 8px 24px rgba(11,31,58,0.07)',
  },
  statusCard: {
    minHeight: 76,
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
    borderCurve: 'continuous',
    borderRadius: 24,
    backgroundColor: '#E9F8F3',
  },
  statusIcon: {
    width: 38,
    height: 38,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 19,
    backgroundColor: '#1EA879',
  },
  statusCopy: { minWidth: 0, flex: 1, gap: 1 },
  statusTitle: { color: '#178F69', fontSize: 16, lineHeight: 21, fontWeight: '800' },
  statusSubtitle: { color: '#52716A', fontSize: 13, lineHeight: 18 },
  menuCard: {
    overflow: 'hidden',
    borderCurve: 'continuous',
    borderRadius: 28,
    backgroundColor: '#F2F5F9',
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.sm,
    gap: 6,
    boxShadow: '0 8px 24px rgba(11,31,58,0.07)',
  },
  menuRow: {
    minHeight: 74,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    gap: spacing.lg,
    borderCurve: 'continuous',
    borderRadius: 20,
    backgroundColor: colors.white,
  },
  menuRowPressed: { backgroundColor: '#F7F9FC', transform: [{ translateY: -2 }, { scale: 0.995 }] },
  menuIcon: {
    width: 44,
    height: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: '#EEF2F7',
    borderCurve: 'continuous',
    borderRadius: 14,
    backgroundColor: '#F7F9FC',
  },
  menuCopy: { minWidth: 0, flex: 1, gap: 2 },
  menuLabel: { color: colors.navy, fontSize: 17, lineHeight: 23, fontWeight: '600' },
  menuDescription: { color: colors.textMuted, fontSize: 14, lineHeight: 19 },
  detailHeader: { minHeight: 54, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  backButton: { width: 48, height: 48, alignItems: 'center', justifyContent: 'center', borderRadius: 24, backgroundColor: colors.white },
  backArrow: { color: colors.navy, fontSize: 38, lineHeight: 40 },
  detailHeaderTitle: { color: colors.navy, fontSize: typography.size.title, fontWeight: '900' },
  headerBalance: { width: 48 },
  detailCard: {
    gap: spacing.lg,
    padding: spacing.xl,
    borderCurve: 'continuous',
    borderRadius: 28,
    backgroundColor: colors.white,
    boxShadow: '0 8px 24px rgba(11,31,58,0.07)',
  },
  detailsHeading: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  detailIconCircle: { width: 50, height: 50, alignItems: 'center', justifyContent: 'center', borderRadius: 25, backgroundColor: colors.blueMuted },
  detailHeadingCopy: { minWidth: 0, flex: 1, gap: 2 },
  detailTitle: { color: colors.navy, fontSize: 19, lineHeight: 24, fontWeight: '800' },
  detailDescription: { color: colors.textMuted, fontSize: 13, lineHeight: 18 },
  readOnlyField: { gap: spacing.xs, padding: spacing.lg, borderRadius: radii.md, backgroundColor: '#F5F7FA' },
  readOnlyLabel: { color: colors.textMuted, fontSize: 12, fontWeight: '700' },
  readOnlyValue: { color: colors.navy, fontSize: 16, fontWeight: '700' },
  infoHeroIcon: { width: 74, height: 74, alignSelf: 'center', alignItems: 'center', justifyContent: 'center', borderRadius: 37, backgroundColor: colors.blueMuted },
  infoCopy: { alignItems: 'center', gap: spacing.sm },
  infoTitle: { color: colors.navy, fontSize: 23, lineHeight: 29, fontWeight: '900', textAlign: 'center' },
  infoDescription: { color: colors.textMuted, fontSize: typography.size.body, lineHeight: 22, textAlign: 'center' },
  infoValueCard: { gap: spacing.xs, padding: spacing.lg, borderRadius: radii.lg, backgroundColor: '#F5F7FA' },
  infoEyebrow: { color: colors.blue, fontSize: 11, fontWeight: '900', letterSpacing: 0.8 },
  infoValue: { color: colors.navy, fontSize: 16, fontWeight: '800' },
  cardPressed: { transform: [{ translateY: -3 }, { scale: 0.99 }] },
  pressed: { opacity: 0.8, transform: [{ scale: 0.96 }] },
});
