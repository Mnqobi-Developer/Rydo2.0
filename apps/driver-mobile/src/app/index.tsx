import { ScrollView, Text, View } from 'react-native';

import { colors } from '@/theme/colors';

export default function DriverHomeScreen() {
  return (
    <ScrollView
      contentInsetAdjustmentBehavior="automatic"
      style={{ flex: 1, backgroundColor: colors.surface }}
      contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: 24 }}
    >
      <View style={{ gap: 16 }}>
        <View
          style={{
            alignSelf: 'flex-start',
            backgroundColor: colors.successMuted,
            borderRadius: 999,
            paddingHorizontal: 14,
            paddingVertical: 8,
          }}
        >
          <Text selectable style={{ color: colors.success, fontSize: 13, fontWeight: '700' }}>
            DRIVER APP
          </Text>
        </View>

        <Text
          selectable
          style={{ color: colors.navy, fontSize: 36, fontWeight: '800', letterSpacing: -1 }}
        >
          Ready when the road calls.
        </Text>

        <Text selectable style={{ color: colors.textMuted, fontSize: 17, lineHeight: 25 }}>
          The RYDO Driver foundation is ready. Onboarding, availability, and active-trip controls
          will be added through focused, tested pull requests.
        </Text>

        <View
          style={{
            backgroundColor: colors.white,
            borderColor: colors.border,
            borderRadius: 24,
            borderWidth: 1,
            gap: 6,
            padding: 20,
            boxShadow: '0 10px 30px rgba(11, 31, 58, 0.08)',
          }}
        >
          <Text selectable style={{ color: colors.navy, fontSize: 18, fontWeight: '700' }}>
            Driver foundation connected
          </Text>
          <Text selectable style={{ color: colors.textMuted, fontSize: 15, lineHeight: 22 }}>
            Independent Expo identity, Router, TypeScript, and brand tokens are configured.
          </Text>
        </View>
      </View>
    </ScrollView>
  );
}
