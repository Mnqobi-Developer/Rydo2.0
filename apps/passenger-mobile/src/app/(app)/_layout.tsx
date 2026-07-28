import { RydoIcon, colors } from '@rydo/mobile-design-system';
import { Tabs } from 'expo-router';

const tabBarStyle = {
  position: 'absolute' as const,
  left: 12,
  right: 12,
  bottom: 10,
  height: 68,
  borderTopWidth: 0,
  borderRadius: 24,
  backgroundColor: 'rgba(11,31,58,0.92)',
  paddingTop: 8,
  paddingBottom: 8,
  boxShadow: '0 10px 28px rgba(11,31,58,0.24)',
};

export default function PassengerTabsLayout() {
  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.surface },
        headerShadowVisible: false,
        headerTintColor: colors.navy,
        headerTitleStyle: { fontWeight: '800' },
        sceneStyle: { backgroundColor: colors.surface },
        tabBarActiveTintColor: colors.blue,
        tabBarInactiveTintColor: '#B7C3D5',
        tabBarHideOnKeyboard: true,
        tabBarLabelStyle: { fontSize: 11, fontWeight: '700' },
        tabBarStyle,
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: 'Home',
          headerShown: false,
          tabBarIcon: ({ color }) => <RydoIcon name="home" color={color} size={22} />,
        }}
      />
      <Tabs.Screen
        name="activity"
        options={{
          title: 'Activity',
          tabBarIcon: ({ color }) => <RydoIcon name="clock" color={color} size={22} />,
        }}
      />
      <Tabs.Screen
        name="saved"
        options={{
          title: 'Saved',
          tabBarIcon: ({ color }) => <RydoIcon name="bookmark" color={color} size={22} />,
        }}
      />
      <Tabs.Screen
        name="account"
        options={{
          title: 'Account',
          tabBarIcon: ({ color }) => <RydoIcon name="person" color={color} size={22} />,
        }}
      />
    </Tabs>
  );
}
