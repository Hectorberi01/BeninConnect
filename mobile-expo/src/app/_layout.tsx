import { Inter_400Regular, Inter_500Medium, Inter_600SemiBold, Inter_700Bold, useFonts } from '@expo-google-fonts/inter';
import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { ToastHost } from '@/components/ToastHost';
import { usePreferences } from '@/core/preferences';
import { queryClient } from '@/core/queryClient';
import { useColors, useIsDark } from '@/core/theme';

export default function RootLayout() {
  const [fontsLoaded] = useFonts({ Inter_400Regular, Inter_500Medium, Inter_600SemiBold, Inter_700Bold });
  const loaded = usePreferences((s) => s.loaded);
  const colors = useColors();
  const dark = useIsDark();

  useEffect(() => {
    usePreferences.getState().load();
  }, []);

  if (!fontsLoaded || !loaded) return <View style={{ flex: 1, backgroundColor: '#087F5B' }} />;

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <StatusBar style={dark ? 'light' : 'dark'} />
        <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.background }, animation: 'slide_from_right' }}>
          <Stack.Screen name="index" options={{ animation: 'fade' }} />
          <Stack.Screen name="onboarding" options={{ animation: 'fade' }} />
        </Stack>
        <ToastHost />
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}
