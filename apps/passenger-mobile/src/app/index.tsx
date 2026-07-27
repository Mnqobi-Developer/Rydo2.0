import { ScrollView, Text, View } from 'react-native';

import { colors } from '@/theme/colors';

export default function PassengerHomeScreen() {
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
            backgroundColor: colors.blueMuted,
            borderRadius: 999,
            paddingHorizontal: 14,
            paddingVertical: 8,
          }}
        >
          <Text selectable style={{ color: colors.blue, fontSize: 13, fontWeight: '700' }}>
            PASSENGER APP
          </Text>
        </View>

        <Text
          selectable
          style={{ color: colors.navy, fontSize: 36, fontWeight: '800', letterSpacing: -1 }}
        >
          Your ride starts here.
        </Text>

        <Text selectable style={{ color: colors.textMuted, fontSize: 17, lineHeight: 25 }}>
          The RYDO mobile foundation is ready. Ride planning and live trip features will be added
          through focused, tested pull requests.
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
            Foundation connected
          </Text>
          <Text selectable style={{ color: colors.textMuted, fontSize: 15, lineHeight: 22 }}>
            Expo Router, TypeScript, app identity, and brand tokens are configured.
          </Text>
        </View>
      </View>
    </ScrollView>
  );
}
