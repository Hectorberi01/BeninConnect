import type { BottomTabBarProps } from 'expo-router/js-tabs';
import { LinearGradient } from 'expo-linear-gradient';
import { router } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { depth, font, glow, useColors } from '@/core/theme';
import { Glass, TAB_BAR_HEIGHT } from './glass';
import { Badge, Icon, type IconName } from './ui';

const TABS: Record<string, { label: string; icon: IconName; active: IconName }> = {
  home: { label: 'Accueil', icon: 'home-outline', active: 'home' },
  communities: { label: 'Communautés', icon: 'account-group-outline', active: 'account-group' },
  messages: { label: 'Messages', icon: 'message-outline', active: 'message' },
  profile: { label: 'Profil', icon: 'account-circle-outline', active: 'account-circle' },
};

/** Barre d'onglets flottante en verre, bouton central « Publier » en relief. */
export function GlassTabBar({ state, emitter, navigateToTab, unreadMessages }: BottomTabBarProps & { unreadMessages: number }) {
  const c = useColors();
  const insets = useSafeAreaInsets();

  return (
    <View pointerEvents="box-none" style={[styles.wrap, { bottom: Math.max(insets.bottom, 12) }]}>
      <View style={[styles.shadow, depth(c, 3)]}>
        <Glass radius={TAB_BAR_HEIGHT / 2} intensity={60} style={styles.bar}>
          {state.routes.map((route, index) => {
            const focused = state.index === index;

            if (route.name === 'publish') {
              return (
                <View key={route.key} style={styles.center}>
                  <Pressable accessibilityRole="button" accessibilityLabel="Publier" onPress={() => router.push('/post/new')}
                    style={({ pressed }) => [styles.fabRing, { borderColor: c.glassBorder, backgroundColor: c.glassStrong }, glow(c.primaryGlow, 18), pressed && { transform: [{ scale: 0.94 }] }]}>
                    <LinearGradient colors={[c.primaryTop, c.primary, c.primaryDark]} start={{ x: 0.2, y: 0 }} end={{ x: 0.8, y: 1 }} style={styles.fab}>
                      <LinearGradient colors={['rgba(255,255,255,0.45)', 'rgba(255,255,255,0)']} style={styles.fabShine} pointerEvents="none" />
                      <Icon name="plus" size={30} color="#FFFFFF" />
                    </LinearGradient>
                  </Pressable>
                </View>
              );
            }

            const tab = TABS[route.name];
            if (!tab) return null;
            const color = focused ? c.primary : c.textSecondary;
            const press = () => {
              const event = emitter.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
              if (!focused && !event.defaultPrevented) navigateToTab(route.key);
            };

            return (
              <Pressable key={route.key} accessibilityRole="tab" accessibilityState={{ selected: focused }} accessibilityLabel={tab.label}
                onPress={press} style={({ pressed }) => [styles.item, pressed && { opacity: 0.6 }]}>
                <View>
                  <Icon name={focused ? tab.active : tab.icon} size={25} color={color} />
                  {route.name === 'messages' && unreadMessages > 0 && <Badge count={unreadMessages} style={styles.badge} />}
                </View>
                <Text numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.8} style={[styles.label, { color, fontFamily: focused ? font.semibold : font.medium }]}>{tab.label}</Text>
              </Pressable>
            );
          })}
        </Glass>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { position: 'absolute', left: 14, right: 14, alignItems: 'center' },
  shadow: { width: '100%', maxWidth: 520, borderRadius: TAB_BAR_HEIGHT / 2 },
  bar: { height: TAB_BAR_HEIGHT, flexDirection: 'row', alignItems: 'center', paddingHorizontal: 4 },
  item: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 3, height: '100%' },
  center: { width: 78, alignItems: 'center', justifyContent: 'center' },
  label: { fontSize: 10.5 },
  badge: { position: 'absolute', top: -5, right: -11 },
  fabRing: { width: 66, height: 66, borderRadius: 33, padding: 4, borderWidth: 1 },
  fab: { flex: 1, borderRadius: 29, alignItems: 'center', justifyContent: 'center', overflow: 'hidden' },
  fabShine: { position: 'absolute', top: 0, left: 0, right: 0, height: '50%' },
});
