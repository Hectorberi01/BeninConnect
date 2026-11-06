import { Redirect, Stack } from 'expo-router';
import { useSession } from '@/core/session';
import { useColors } from '@/core/theme';

/** Zone connectée : sans session, retour à la connexion. */
export default function AppLayout() {
  const user = useSession((s) => s.user);
  const c = useColors();
  if (!user) return <Redirect href="/login" />;
  return (
    <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: c.background } }}>
      <Stack.Screen name="(tabs)" options={{ animation: 'fade' }} />
      <Stack.Screen name="post/new" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
    </Stack>
  );
}
