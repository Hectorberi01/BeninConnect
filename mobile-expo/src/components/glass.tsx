import { BlurView } from 'expo-blur';
import { LinearGradient } from 'expo-linear-gradient';
import type { ReactNode } from 'react';
import { Platform, type StyleProp, StyleSheet, View, type ViewStyle } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useColors, useIsDark } from '@/core/theme';

/** Surface en verre dépoli : flou + voile translucide + reflet en haut. */
export function Glass({ children, style, radius = 20, intensity = 40, strong }: {
  children?: ReactNode; style?: StyleProp<ViewStyle>; radius?: number; intensity?: number; strong?: boolean;
}) {
  const c = useColors();
  const dark = useIsDark();
  return (
    <View style={[{ borderRadius: radius, overflow: 'hidden', borderWidth: 1, borderColor: c.glassBorder }, style]}>
      <BlurView intensity={intensity} tint={dark ? 'dark' : 'light'} style={StyleSheet.absoluteFill}
        experimentalBlurMethod={Platform.OS === 'android' ? 'dimezisBlurView' : undefined} />
      <View style={[StyleSheet.absoluteFill, { backgroundColor: strong ? c.glassStrong : c.glass }]} />
      <Highlight />
      {children}
    </View>
  );
}

/** Reflet lumineux du haut (donne le volume). */
export function Highlight({ height = '55%', opacity = 1 }: { height?: `${number}%` | number; opacity?: number }) {
  const dark = useIsDark();
  return (
    <LinearGradient pointerEvents="none" colors={dark ? ['rgba(255,255,255,0.10)', 'rgba(255,255,255,0)'] : ['rgba(255,255,255,0.85)', 'rgba(255,255,255,0)']}
      style={{ position: 'absolute', top: 0, left: 0, right: 0, height, opacity }} />
  );
}

/** Fond d'écran légèrement dégradé, pour que le verre se lise. */
export function Backdrop() {
  const c = useColors();
  return <LinearGradient pointerEvents="none" colors={[c.bgTop, c.bgBottom]} style={StyleSheet.absoluteFill} />;
}

/** Espace à réserver en bas des écrans à onglets (barre flottante). */
export const TAB_BAR_HEIGHT = 68;
export function useTabBarSpace() {
  const insets = useSafeAreaInsets();
  return TAB_BAR_HEIGHT + Math.max(insets.bottom, 12) + 20;
}
