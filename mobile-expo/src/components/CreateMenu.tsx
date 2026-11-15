import { Image } from 'expo-image';
import { type Href, router } from 'expo-router';
import { useEffect, useRef } from 'react';
import { Animated, Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { font, useColors, useIsDark } from '@/core/theme';
import { useVoyagesEnabled } from '@/features/queries';
import { Icon, type IconName, Muted, T } from './ui';

interface Item {
  key: string;
  icon: IconName;
  title: string;
  text: string;
  href: Href;
  accent: string;
  /** Fond clair de la carte, identique au fond de l'illustration. */
  tint: string;
  image: number;
}

const ITEMS: Item[] = [
  { key: 'post', icon: 'square-edit-outline', title: 'Publication', text: 'Texte, photos ou vidéo', href: '/post/new',
    accent: '#1F7A55', tint: '#F0FAF5', image: require('../../assets/images/Create/publication.jpg') },
  { key: 'service', icon: 'hand-heart-outline', title: 'Service', text: 'Proposer vos services aux membres', href: '/services/me',
    accent: '#EA7A1E', tint: '#FDF7EF', image: require('../../assets/images/Create/service.jpg') },
  { key: 'voyage', icon: 'airplane-takeoff', title: 'Voyage', text: 'Proposer vos kilos libres', href: '/voyages/publish',
    accent: '#1F7A6A', tint: '#EEF5FD', image: require('../../assets/images/Create/voyage.jpg') },
  { key: 'event', icon: 'calendar-star', title: 'Événement', text: 'Organiser une rencontre, un atelier ou une activité', href: '/events/new',
    accent: '#7C4DDB', tint: '#F4F1FC', image: require('../../assets/images/Create/evenement.jpg') },
  { key: 'community', icon: 'account-group', title: 'Communauté', text: 'Rassembler des membres autour d’un thème', href: '/community/new',
    accent: '#1F7A55', tint: '#F0FBF5', image: require('../../assets/images/Create/communaute.jpg') },
];

const alpha = (hex: string, a: number) => `rgba(${parseInt(hex.slice(1, 3), 16)},${parseInt(hex.slice(3, 5), 16)},${parseInt(hex.slice(5, 7), 16)},${a})`;

/** Menu du bouton central « + » : tout ce qu'un membre peut créer. */
export function CreateMenu({ open, onClose }: { open: boolean; onClose: () => void }) {
  const c = useColors();
  const dark = useIsDark();
  const insets = useSafeAreaInsets();
  const voyages = useVoyagesEnabled().enabled;
  const slide = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (open) {
      slide.setValue(0);
      Animated.spring(slide, { toValue: 1, useNativeDriver: true, damping: 20, stiffness: 180 }).start();
    }
  }, [open, slide]);

  const items = ITEMS.filter((it) => it.key !== 'voyage' || voyages);
  const go = (href: Href) => {
    onClose();
    router.push(href);
  };

  return (
    <Modal visible={open} transparent animationType="fade" onRequestClose={onClose} statusBarTranslucent>
      <Pressable accessibilityLabel="Fermer" style={[StyleSheet.absoluteFill, { backgroundColor: c.overlay }]} onPress={onClose} />
      <Animated.View style={[styles.sheet, { backgroundColor: c.surface, paddingBottom: insets.bottom + 16,
        transform: [{ translateY: slide.interpolate({ inputRange: [0, 1], outputRange: [600, 0] }) }] }]}>
        <View style={[styles.handle, { backgroundColor: c.border }]} />

        <View style={styles.header}>
          <View style={[styles.headerIcon, { backgroundColor: c.primaryLight }]}><Icon name="plus-box-outline" size={28} color={c.primary} /></View>
          <View style={{ flex: 1 }}>
            <T v="h2">Créer</T>
            <Muted>Choisissez ce que vous souhaitez publier</Muted>
          </View>
          <Pressable accessibilityRole="button" accessibilityLabel="Fermer" hitSlop={8} onPress={onClose}
            style={({ pressed }) => [styles.close, { borderColor: c.border, backgroundColor: c.surface }, pressed && { opacity: 0.7 }]}>
            <Icon name="close" size={22} color={c.text} />
          </Pressable>
        </View>

        <View style={{ gap: 10 }}>
          {items.map((it) => {
            // Mode sombre : teinte transparente ; l'illustration garde son fond clair dans une pastille arrondie
            const background = dark ? alpha(it.accent, 0.14) : it.tint;
            return (
              <Pressable key={it.key} accessibilityRole="button" accessibilityLabel={`${it.title}. ${it.text}`} onPress={() => go(it.href)}
                style={({ pressed }) => [styles.item, { backgroundColor: background, borderColor: alpha(it.accent, dark ? 0.3 : 0.1) },
                  pressed && { transform: [{ scale: 0.98 }] }]}>
                <View style={[styles.icon, { backgroundColor: alpha(it.accent, dark ? 0.25 : 0.12) }]}>
                  <Icon name={it.icon} size={24} color={dark ? '#FFFFFF' : it.accent} />
                </View>
                <View style={{ flex: 1, gap: 2 }}>
                  <Text style={[styles.title, { color: c.text }]}>{it.title}</Text>
                  <Muted lines={3} style={{ fontSize: 13, lineHeight: 17 }}>{it.text}</Muted>
                </View>
                <View style={[styles.art, { backgroundColor: it.tint }]}>
                  <Image source={it.image} style={StyleSheet.absoluteFill} contentFit="contain" accessibilityIgnoresInvertColors />
                </View>
                <Icon name="chevron-right" size={22} color={c.text} />
              </Pressable>
            );
          })}
        </View>
      </Animated.View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  sheet: { position: 'absolute', left: 0, right: 0, bottom: 0, borderTopLeftRadius: 28, borderTopRightRadius: 28, paddingHorizontal: 16, paddingTop: 10 },
  handle: { alignSelf: 'center', width: 44, height: 5, borderRadius: 3, marginBottom: 14 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: 16 },
  headerIcon: { width: 52, height: 52, borderRadius: 16, alignItems: 'center', justifyContent: 'center' },
  close: { width: 40, height: 40, borderRadius: 20, borderWidth: 1, alignItems: 'center', justifyContent: 'center' },
  item: { flexDirection: 'row', alignItems: 'center', gap: 10, minHeight: 80, paddingVertical: 10, paddingLeft: 10, paddingRight: 8, borderRadius: 22, borderWidth: 1 },
  icon: { width: 48, height: 48, borderRadius: 15, alignItems: 'center', justifyContent: 'center' },
  title: { fontFamily: font.bold, fontSize: 16 },
  art: { width: 68, height: 48, borderRadius: 14, overflow: 'hidden' },
});
