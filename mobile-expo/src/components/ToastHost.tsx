import { Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useToast } from '@/core/toast';
import { font, useColors } from '@/core/theme';
import { Icon } from './ui';

/** Affiche le toast courant au-dessus de tous les écrans. */
export function ToastHost() {
  const { message, kind } = useToast();
  const c = useColors();
  const insets = useSafeAreaInsets();
  if (!message) return null;
  const icon = kind === 'success' ? 'check-circle' : kind === 'error' ? 'alert-circle' : 'information';
  const iconColor = kind === 'success' ? c.success : kind === 'error' ? c.error : c.primary;
  return (
    <View pointerEvents="none" accessibilityLiveRegion="polite" accessibilityRole="alert"
      style={{ position: 'absolute', left: 16, right: 16, bottom: Math.max(insets.bottom, 12) + 96, alignItems: 'center' }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10, backgroundColor: c.toast, borderRadius: 999, borderWidth: 1, borderColor: 'rgba(255,255,255,0.15)', paddingHorizontal: 16, paddingVertical: 12, maxWidth: 520, boxShadow: '0px 6px 18px rgba(0,0,0,0.22)' }}>
        <Icon name={icon} size={20} color={iconColor} />
        <Text style={{ color: c.onToast, fontFamily: font.medium, fontSize: 14, flexShrink: 1 }}>{message}</Text>
      </View>
    </View>
  );
}
