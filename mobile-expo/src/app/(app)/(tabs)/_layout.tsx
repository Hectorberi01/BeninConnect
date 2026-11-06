import { Tabs } from 'expo-router/js-tabs';
import { GlassTabBar } from '@/components/GlassTabBar';
import { useColors } from '@/core/theme';
import { useUnreadMessages } from '@/features/queries';

export default function TabsLayout() {
  const c = useColors();
  const unread = useUnreadMessages().data ?? 0;
  return (
    <Tabs
      tabBar={(props) => <GlassTabBar {...props} unreadMessages={unread} />}
      screenOptions={{ headerShown: false, sceneStyle: { backgroundColor: c.background } }}
    >
      <Tabs.Screen name="home" />
      <Tabs.Screen name="communities" />
      <Tabs.Screen name="publish" />
      <Tabs.Screen name="messages" />
      <Tabs.Screen name="profile" />
    </Tabs>
  );
}
