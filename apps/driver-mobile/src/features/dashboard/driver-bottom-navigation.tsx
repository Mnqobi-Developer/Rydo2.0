import type { RydoIconName } from '@rydo/mobile-design-system';
import { RydoIcon, colors } from '@rydo/mobile-design-system';
import { Image } from 'expo-image';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { driverTheme } from '@/theme/driver-theme';

export type DriverTab = 'home' | 'earnings' | 'trips' | 'wallet' | 'profile';

type NavigationItem = {
  key: DriverTab;
  label: string;
  icon?: RydoIconName;
  enabled: boolean;
  customIcon?: 'earnings' | 'trips';
};

const navigationItems: NavigationItem[] = [
  { key: 'home', label: 'Home', icon: 'home', enabled: true },
  { key: 'earnings', label: 'Earnings', icon: 'earnings', customIcon: 'earnings', enabled: true },
  { key: 'trips', label: 'Trips', customIcon: 'trips', enabled: true },
  { key: 'wallet', label: 'Wallet', icon: 'card', enabled: true },
  { key: 'profile', label: 'Account', icon: 'person', enabled: true },
];

export function DriverBottomNavigation({
  activeTab,
  onSelect,
}: {
  activeTab: DriverTab;
  onSelect(tab: DriverTab): void;
}) {
  const insets = useSafeAreaInsets();

  return (
    <View style={[styles.bottomBar, { paddingBottom: Math.max(insets.bottom, 8) }]}>
      {navigationItems.map((item) => {
        const active = item.key === activeTab;
        return (
          <Pressable
            key={item.key}
            accessibilityRole="tab"
            accessibilityState={{ disabled: !item.enabled, selected: active }}
            disabled={!item.enabled}
            onPress={() => onSelect(item.key)}
            style={({ pressed }) => [styles.navItem, pressed && styles.navItemPressed]}
          >
            <View style={[styles.navIcon, active && styles.navIconActive]}>
              {item.customIcon ? (
                <Image
                  accessibilityIgnoresInvertColors
                  contentFit="contain"
                  source={item.customIcon === 'earnings'
                    ? require('../../../assets/icons/navigation/earnings.png')
                    : require('../../../assets/icons/navigation/trips.png')}
                  style={[styles.customNavIcon, { tintColor: active ? colors.blue : colors.textMuted }]}
                />
              ) : (
                <RydoIcon name={item.icon ?? 'home'} color={active ? colors.blue : colors.textMuted} size={20} />
              )}
            </View>
            <Text style={[styles.navLabel, active && styles.navLabelActive]}>{item.label}</Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  bottomBar: {
    position: 'absolute',
    right: 0,
    bottom: 0,
    left: 0,
    minHeight: 86,
    flexDirection: 'row',
    alignItems: 'flex-start',
    paddingTop: 8,
    paddingHorizontal: 8,
    borderTopWidth: 1,
    borderTopColor: driverTheme.colors.softBorder,
    backgroundColor: 'rgba(255,255,255,0.98)',
    boxShadow: '0 -8px 24px rgba(11,31,58,0.06)',
  },
  navItem: { flex: 1, alignItems: 'center', gap: 2 },
  navItemPressed: { opacity: 0.72, transform: [{ scale: 0.96 }] },
  navIcon: { width: 42, height: 34, alignItems: 'center', justifyContent: 'center', borderRadius: 14 },
  navIconActive: { backgroundColor: colors.blueMuted },
  customNavIcon: { width: 21, height: 21 },
  navLabel: { color: colors.textMuted, fontSize: 10, fontWeight: '600' },
  navLabelActive: { color: colors.blue, fontWeight: '800' },
});
