import { useColorScheme } from 'react-native';
import { usePreferences } from './preferences';

// Palette de la spécification ; le mode sombre reprend les mêmes rôles.
export const lightColors = {
  primary: '#087F5B',
  primaryDark: '#065F46',
  primaryLight: '#D1FAE5',
  onPrimary: '#FFFFFF',
  background: '#F8FAFC',
  surface: '#FFFFFF',
  surfaceVariant: '#F1F5F9',
  text: '#111827',
  textSecondary: '#6B7280',
  border: '#E5E7EB',
  success: '#16A34A',
  warning: '#F59E0B',
  warningLight: '#FEF3C7',
  error: '#DC2626',
  errorLight: '#FEE2E2',
  overlay: 'rgba(17,24,39,0.6)',
  toast: '#111827',
  onToast: '#FFFFFF',
  bubbleMine: '#087F5B',
  bubbleOther: '#FFFFFF',
  // Effet verre (« glacé ») et relief.
  glass: 'rgba(255,255,255,0.72)',
  glassStrong: 'rgba(255,255,255,0.88)',
  glassBorder: 'rgba(255,255,255,0.9)',
  glassEdge: 'rgba(15,23,42,0.06)',
  shadow: '#0F2A22',
  primaryGlow: 'rgba(8,127,91,0.45)',
  primaryTop: '#16A37A',
  bgTop: '#EEF7F3',
  bgBottom: '#F4F6FA',
};

export type Colors = typeof lightColors;

export const darkColors: Colors = {
  primary: '#10B981',
  primaryDark: '#059669',
  primaryLight: '#064E3B',
  onPrimary: '#FFFFFF',
  background: '#0B1220',
  surface: '#111827',
  surfaceVariant: '#1F2937',
  text: '#F9FAFB',
  textSecondary: '#9CA3AF',
  border: '#1F2937',
  success: '#22C55E',
  warning: '#FBBF24',
  warningLight: '#422006',
  error: '#F87171',
  errorLight: '#450A0A',
  overlay: 'rgba(0,0,0,0.7)',
  toast: '#F9FAFB',
  onToast: '#111827',
  bubbleMine: '#059669',
  bubbleOther: '#1F2937',
  glass: 'rgba(17,24,39,0.62)',
  glassStrong: 'rgba(17,24,39,0.82)',
  glassBorder: 'rgba(255,255,255,0.12)',
  glassEdge: 'rgba(255,255,255,0.06)',
  shadow: '#000000',
  primaryGlow: 'rgba(16,185,129,0.45)',
  primaryTop: '#34D399',
  bgTop: '#0D1B1A',
  bgBottom: '#0B1220',
};

export const benin = { green: '#008751', yellow: '#FCD116', red: '#E8112D' };

export const font = {
  regular: 'Inter_400Regular',
  medium: 'Inter_500Medium',
  semibold: 'Inter_600SemiBold',
  bold: 'Inter_700Bold',
};

export const type = {
  h1: { fontSize: 28, fontFamily: font.bold, lineHeight: 34 },
  h2: { fontSize: 22, fontFamily: font.bold, lineHeight: 28 },
  h3: { fontSize: 18, fontFamily: font.semibold, lineHeight: 24 },
  body: { fontSize: 15, fontFamily: font.regular, lineHeight: 22 },
  label: { fontSize: 13, fontFamily: font.semibold },
  caption: { fontSize: 13, fontFamily: font.regular },
} as const;

export const radius = { card: 20, control: 14, pill: 999 };

const rgba = (color: string, alpha: number) => {
  if (!color.startsWith('#')) return color;
  const n = parseInt(color.slice(1), 16);
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${alpha})`;
};

// boxShadow plutôt que shadow* : rendu efficace même sur fond translucide (verre).
/** Ombre en relief. */
export const depth = (c: Colors, level: 1 | 2 | 3 = 1) => ({
  boxShadow: `0px ${[0, 3, 8, 14][level]}px ${[0, 10, 20, 30][level]}px ${rgba(c.shadow, [0, 0.08, 0.11, 0.18][level])}`,
});

/** Halo coloré (bouton central, puces actives). */
export const glow = (color: string, radius = 14) => ({
  boxShadow: `0px 6px ${radius * 1.4}px ${rgba(color, 0.55)}`,
});

/** Couleurs du thème actif (préférence Clair / Sombre / Système). */
export function useColors(): Colors {
  const system = useColorScheme();
  const theme = usePreferences((s) => s.theme);
  const dark = theme === 'dark' || (theme === 'system' && system === 'dark');
  return dark ? darkColors : lightColors;
}

export function useIsDark() {
  const system = useColorScheme();
  const theme = usePreferences((s) => s.theme);
  return theme === 'dark' || (theme === 'system' && system === 'dark');
}
