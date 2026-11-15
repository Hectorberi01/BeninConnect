import { type Href, router } from 'expo-router';
import { Modal, Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useColors } from '@/core/theme';
import { useVoyagesEnabled } from '@/features/queries';
import { Icon, type IconName, Muted, T } from './ui';

interface Item { icon: IconName; title: string; text: string; href: Href }

/** Menu du bouton central « + » : tout ce qu'un membre peut créer. */
export function CreateMenu({ open, onClose }: { open: boolean; onClose: () => void }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const voyages = useVoyagesEnabled().enabled;
  const items: Item[] = [
    { icon: 'square-edit-outline', title: 'Publication', text: 'Texte, photos ou vidéo', href: '/post/new' },
    { icon: 'account-wrench-outline', title: 'Service', text: 'Proposer vos services aux membres', href: '/services/me' },
    ...(voyages ? [{ icon: 'airplane-takeoff' as IconName, title: 'Voyage', text: 'Proposer vos kilos libres', href: '/voyages/publish' as Href }] : []),
    { icon: 'calendar-plus', title: 'Événement', text: 'Organiser une rencontre', href: '/events/new' },
    { icon: 'account-multiple-plus-outline', title: 'Communauté', text: 'Rassembler des membres autour d’un thème', href: '/community/new' },
  ];
  const go = (href: Href) => {
    onClose();
    router.push(href);
  };

  return (
    <Modal visible={open} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable accessibilityLabel="Fermer" style={[StyleSheet.absoluteFill, { backgroundColor: c.overlay }]} onPress={onClose} />
      <View style={[styles.sheet, { backgroundColor: c.surface, paddingBottom: insets.bottom + 16 }]}>
        <View style={[styles.handle, { backgroundColor: c.border }]} />
        <T v="h3" style={{ marginBottom: 8 }}>Créer</T>
        {items.map((it) => (
          <Pressable key={it.title} accessibilityRole="button" accessibilityLabel={it.title} onPress={() => go(it.href)}
            style={({ pressed }) => [styles.item, pressed && { backgroundColor: c.surfaceVariant }]}>
            <View style={[styles.icon, { backgroundColor: c.primaryLight }]}><Icon name={it.icon} size={22} color={c.primary} /></View>
            <View style={{ flex: 1 }}>
              <T v="label" style={{ fontSize: 15 }}>{it.title}</T>
              <Muted lines={1}>{it.text}</Muted>
            </View>
            <Icon name="chevron-right" size={22} color={c.textSecondary} />
          </Pressable>
        ))}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  sheet: { position: 'absolute', left: 0, right: 0, bottom: 0, borderTopLeftRadius: 24, borderTopRightRadius: 24, paddingHorizontal: 16, paddingTop: 10 },
  handle: { alignSelf: 'center', width: 40, height: 5, borderRadius: 3, marginBottom: 12 },
  item: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 10, paddingHorizontal: 6, borderRadius: 14 },
  icon: { width: 44, height: 44, borderRadius: 14, alignItems: 'center', justifyContent: 'center' },
});
