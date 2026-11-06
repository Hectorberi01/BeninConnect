import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useColors } from '@/core/theme';
import { Icon, T } from './ui';

/** Défilement + clavier pour les formulaires. */
export function FormScroll({ children, header, footer }: { children: ReactNode; header?: ReactNode; footer?: ReactNode }) {
  const insets = useSafeAreaInsets();
  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      {header}
      <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + 24 }]}>
        <View style={styles.inner}>{children}</View>
      </ScrollView>
      {footer}
    </KeyboardAvoidingView>
  );
}

export function FormError({ message }: { message?: string | null }) {
  const c = useColors();
  if (!message) return null;
  return (
    <View accessibilityRole="alert" style={[styles.error, { backgroundColor: c.errorLight }]}>
      <Icon name="alert-circle-outline" size={20} color={c.error} />
      <T v="caption" color={c.error} style={{ flex: 1 }}>{message}</T>
    </View>
  );
}

export function InfoBox({ children, icon = 'information-outline' }: { children: ReactNode; icon?: 'information-outline' | 'flask-outline' | 'lock-outline' }) {
  const c = useColors();
  return (
    <View style={[styles.error, { backgroundColor: c.primaryLight }]}>
      <Icon name={icon} size={20} color={c.primaryDark} />
      <View style={{ flex: 1 }}>{children}</View>
    </View>
  );
}

const styles = StyleSheet.create({
  content: { flexGrow: 1, padding: 20 },
  inner: { width: '100%', maxWidth: 560, alignSelf: 'center' },
  error: { flexDirection: 'row', gap: 10, padding: 12, borderRadius: 12, marginBottom: 14, alignItems: 'flex-start' },
});
