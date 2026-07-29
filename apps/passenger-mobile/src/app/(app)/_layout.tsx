import { RydoIcon, colors } from '@rydo/mobile-design-system';
import { Tabs } from 'expo-router';
import type { ColorValue } from 'react-native';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { createPassengerTabBarStyle } from '@/theme/passenger-tab-bar';

export default function PassengerTabsLayout() {
  const insets = useSafeAreaInsets();

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.surface },
        headerShadowVisible: false,
        headerTintColor: colors.navy,
        headerTitleStyle: { fontWeight: '800' },
        sceneStyle: { backgroundColor: colors.surface },
        tabBarActiveTintColor: colors.blue,
        tabBarInactiveTintColor: colors.textMuted,
        tabBarActiveBackgroundColor: 'transparent',
        tabBarHideOnKeyboard: true,
        tabBarItemStyle: {
          borderCurve: 'continuous',
          borderRadius: 27,
          overflow: 'hidden',
          marginHorizontal: 5,
          marginVertical: 8,
        },
        tabBarIconStyle: { marginTop: 3 },
        tabBarLabelStyle: { fontSize: 12, fontWeight: '800', marginBottom: 4 },
        tabBarStyle: createPassengerTabBarStyle(insets.bottom),
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: 'Home',
          headerShown: false,
          tabBarIcon: ({ color, focused }) => <TabIcon name="home" color={color} focused={focused} />,
        }}
      />
      <Tabs.Screen
        name="activity"
        options={{
          title: 'Trips',
          headerShown: false,
          tabBarIcon: ({ color, focused }) => <TabIcon name="clock" color={color} focused={focused} />,
        }}
      />
      <Tabs.Screen
        name="saved"
        options={{
          title: 'Saved',
          href: null,
          tabBarIcon: ({ color, focused }) => <TabIcon name="bookmark" color={color} focused={focused} />,
        }}
      />
      <Tabs.Screen
        name="account"
        options={{
          title: 'Account',
          headerShown: false,
          tabBarIcon: ({ color, focused }) => <TabIcon name="person" color={color} focused={focused} />,
        }}
      />
    </Tabs>
  );
}

function TabIcon({ name, color, focused }: { name: 'home' | 'clock' | 'bookmark' | 'person'; color: ColorValue; focused: boolean }) {
  return (
    <View
      style={{
        width: 42,
        height: 32,
        borderRadius: 16,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: focused ? colors.blueMuted : 'transparent',
        transform: [{ scale: focused ? 1.08 : 1 }],
      }}
    >
      <RydoIcon name={name} color={focused ? colors.blue : color} size={21} />
    </View>
  );
}
