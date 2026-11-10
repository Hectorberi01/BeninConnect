import { Redirect, Stack } from 'expo-router';
import { usePushNotifications } from '@/core/push';
import { useSession } from '@/core/session';
import { useColors } from '@/core/theme';
import { useRealtime } from '@/features/realtime';

/** Zone connectée : sans session, retour à la connexion. */
export default function AppLayout() {
  const user = useSession((s) => s.user);
  const c = useColors();
  if (!user) return <Redirect href="/login" />;
  return <SignedIn background={c.background} />;
}

/** Composant séparé : notifications push et temps réel ne s'activent qu'une fois connecté. */
function SignedIn({ background }: { background: string }) {
  usePushNotifications();
  useRealtime();
  return (
    <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: background } }}>
      <Stack.Screen name="(tabs)" options={{ animation: 'fade' }} />
      <Stack.Screen name="post/new" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
      <Stack.Screen name="post/send" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
      <Stack.Screen name="user/qr" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
    </Stack>
  );
}
