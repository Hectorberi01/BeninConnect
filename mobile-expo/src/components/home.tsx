import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { type Href, router } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import * as WebBrowser from 'expo-web-browser';
import { Animated, Pressable, ScrollView, StyleSheet, Text, useWindowDimensions, View, type StyleProp, type ViewStyle } from 'react-native';
import { imageSource } from '@/core/images';
import { useMe } from '@/core/session';
import { depth, font, useColors } from '@/core/theme';
import type { Community, HomeBanner, User } from '@/data/types';
import { initials } from '@/data/types';
import { MembershipButton } from './cards';
import { Avatar, Cover, Icon, type IconName, Muted, T } from './ui';

const HERO = require('../../assets/images/Home/hero.jpg');
const VOYAGES = require('../../assets/images/Home/voyages.jpg');
const BRAND = ['#12945F', '#087F5B', '#05553F'] as const;
const YELLOW = '#FCD116';
const alpha = (hex: string, a: number) => `rgba(${parseInt(hex.slice(1, 3), 16)},${parseInt(hex.slice(3, 5), 16)},${parseInt(hex.slice(5, 7), 16)},${a})`;

// --- Chargement ------------------------------------------------------------------------------

/** Bloc gris qui pulse : réserve la place du contenu pendant le chargement. */
export function Skeleton({ width, height, radius = 12, style }: { width?: number | `${number}%`; height: number; radius?: number; style?: StyleProp<ViewStyle> }) {
  const c = useColors();
  const pulse = useRef(new Animated.Value(0.55)).current;
  useEffect(() => {
    const loop = Animated.loop(Animated.sequence([
      Animated.timing(pulse, { toValue: 1, duration: 700, useNativeDriver: true }),
      Animated.timing(pulse, { toValue: 0.55, duration: 700, useNativeDriver: true }),
    ]));
    loop.start();
    return () => loop.stop();
  }, [pulse]);
  return <Animated.View style={[{ width, height, borderRadius: radius, backgroundColor: c.surfaceVariant, opacity: pulse }, style]} />;
}

/** Rangée de cartes en chargement (même hauteur que les vraies). */
export function SkeletonRow({ width, height }: { width: number; height: number }) {
  return (
    <ScrollView horizontal scrollEnabled={false} showsHorizontalScrollIndicator={false} contentContainerStyle={styles.hList}>
      {[0, 1, 2].map((i) => <Skeleton key={i} width={width} height={height} radius={18} />)}
    </ScrollView>
  );
}

export function PostSkeleton() {
  const c = useColors();
  return (
    <View style={[styles.flat, { backgroundColor: c.surface, borderColor: c.border, marginHorizontal: 16, marginBottom: 12, padding: 14, gap: 10 }]}>
      <View style={{ flexDirection: 'row', gap: 10, alignItems: 'center' }}>
        <Skeleton width={40} height={40} radius={20} />
        <View style={{ gap: 6, flex: 1 }}><Skeleton width="45%" height={12} /><Skeleton width="30%" height={10} /></View>
      </View>
      <Skeleton width="90%" height={12} />
      <Skeleton height={180} radius={14} />
    </View>
  );
}

// --- En-tête ---------------------------------------------------------------------------------

function RoundButton({ icon, label, badge, onPress }: { icon: IconName; label: string; badge?: number; onPress: () => void }) {
  const c = useColors();
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} onPress={onPress} hitSlop={6}
      style={({ pressed }) => [styles.round, { backgroundColor: c.surface, borderColor: c.border }, depth(c, 1), pressed && { opacity: 0.7 }]}>
      <Icon name={icon} size={24} color={c.text} />
      {!!badge && badge > 0 && (
        <View style={[styles.dot, { backgroundColor: c.error, borderColor: c.surface }]}>
          <Text style={styles.dotText}>{badge > 9 ? '9+' : badge}</Text>
        </View>
      )}
    </Pressable>
  );
}

export function HomeHeader({ unread, top }: { unread: number; top: number }) {
  const c = useColors();
  const me = useMe();
  return (
    <View style={[styles.header, { paddingTop: top + 8 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Mon profil" onPress={() => router.navigate('/profile')}>
        <Avatar initials={initials(me)} color={me.avatarColor} image={me.avatarImage} size={48} />
        <View style={[styles.online, { backgroundColor: c.success, borderColor: c.background }]} />
      </Pressable>
      <View style={{ flex: 1, marginLeft: 12 }}>
        <T v="h3" lines={1}>{`Bonjour ${me.firstName} 👋`}</T>
        <Muted lines={1}>Bienvenue sur BéninConnect</Muted>
      </View>
      <View style={{ flexDirection: 'row', gap: 10 }}>
        <RoundButton icon="magnify" label="Rechercher" onPress={() => router.push('/search')} />
        <RoundButton icon="bell-outline" label="Notifications" badge={unread} onPress={() => router.push('/notifications')} />
      </View>
    </View>
  );
}

// --- Bannière --------------------------------------------------------------------------------

function BeninFlag() {
  return (
    <View style={styles.flag}>
      <View style={{ width: '40%', backgroundColor: '#008751' }} />
      <View style={{ flex: 1 }}>
        <View style={{ flex: 1, backgroundColor: YELLOW }} />
        <View style={{ flex: 1, backgroundColor: '#E8112D' }} />
      </View>
    </View>
  );
}

function HeroChip({ icon, label, onPress }: { icon: IconName; label: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} onPress={onPress}
      style={({ pressed }) => [styles.chip, pressed && { transform: [{ scale: 0.96 }] }]}>
      <Icon name={icon} size={20} color="#087F5B" />
      <Text numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.8} style={styles.chipText}>{label}</Text>
    </Pressable>
  );
}

/** Pictogrammes décoratifs des diapositives sans photo. */
function IconCluster({ icons }: { icons: IconName[] }) {
  return (
    <View style={styles.cluster} pointerEvents="none">
      {icons.map((name, i) => (
        <View key={name} style={[styles.clusterItem, { width: [74, 56, 46][i], height: [74, 56, 46][i], top: [6, 62, 18][i], right: [18, 84, 108][i] }]}>
          <Icon name={name} size={[38, 28, 22][i]} color="#FFFFFF" />
        </View>
      ))}
    </View>
  );
}

/** Destinations des boutons de bannière ; une clé inconnue (application plus ancienne) est ignorée. */
const SCREENS: Record<string, Href> = {
  communities: '/communities',
  'community.new': '/community/new',
  jobs: '/jobs',
  events: '/events',
  'event.new': '/events/new',
  services: '/services',
  'services.offer': '/services/me',
  voyages: '/voyages',
  'voyages.publish': '/voyages/publish',
  'post.new': '/post/new',
  search: '/search',
  notifications: '/notifications',
};

export function bannerAction(target: string, voyages: boolean): (() => void) | null {
  if (/^https:\/\/\S+$/.test(target)) return () => void WebBrowser.openBrowserAsync(target).catch(() => undefined);
  if (target.startsWith('voyages') && !voyages) return null;
  const href = SCREENS[target];
  return href ? () => router.push(href) : null;
}

interface Slide {
  key: string;
  title: React.ReactNode;
  text?: string | null;
  more?: string;
  image?: number;
  icons?: IconName[];
  actions: { icon: IconName; label: string; onPress: () => void }[];
}

/** Bannières par défaut, tant qu'aucune n'est en ligne dans la console. */
function defaultSlides(voyages: boolean): Slide[] {
  const go = (href: Href) => () => router.push(href);
  return [
    {
      key: 'intro',
      title: <>La communauté des <Text style={{ color: YELLOW }}>Béninois</Text> du monde entier</>,
      text: 'Échangez · Entraidez-vous',
      more: voyages ? 'Voyages · Services · Emplois' : 'Services · Événements · Emplois',
      image: HERO,
      actions: [
        { icon: 'account-group-outline', label: 'Communautés', onPress: go('/communities') },
        { icon: 'briefcase-outline', label: 'Emplois', onPress: go('/jobs') },
        ...(voyages ? [{ icon: 'airplane' as IconName, label: 'Voyages', onPress: go('/voyages') }] : []),
        { icon: 'calendar-month-outline', label: 'Événements', onPress: go('/events') },
      ],
    },
    {
      key: 'services',
      title: <>Besoin d'un{'\n'}coup de main ?</>,
      text: 'Coiffure, transport, cours… trouvez un membre de confiance près de chez vous.',
      icons: ['hand-heart-outline', 'truck-outline', 'school-outline'],
      actions: [
        { icon: 'magnify', label: 'Trouver un service', onPress: go('/services') },
        { icon: 'account-wrench-outline', label: 'Proposer', onPress: go('/services/me') },
      ],
    },
    voyages
      ? {
        key: 'voyages',
        title: <>Qui part{'\n'}au pays ?</>,
        text: "Envoyez un colis avec un membre qui voyage entre l'Europe et le Bénin.",
        icons: ['airplane', 'bag-suitcase-outline', 'package-variant-closed'],
        actions: [
          { icon: 'airplane-search', label: 'Voir les trajets', onPress: go('/voyages') },
          { icon: 'airplane-takeoff', label: 'Proposer', onPress: go('/voyages/publish') },
        ],
      }
      : {
        key: 'events',
        title: <>Ne manquez{'\n'}aucun rendez-vous</>,
        text: 'Fêtes, rencontres, conférences : les événements de la communauté près de chez vous.',
        icons: ['calendar-star', 'party-popper', 'map-marker-outline'],
        actions: [{ icon: 'calendar-month-outline', label: 'Voir les événements', onPress: go('/events') }],
      },
  ];
}

/** Bannière de la console : image 16:9 (texte compris), boutons blancs posés en bas, lien facultatif sur l'image. */
function ImageBanner({ banner, width, voyages }: { banner: HomeBanner; width: number; voyages: boolean }) {
  const open = banner.target ? bannerAction(banner.target, voyages) : null;
  const actions = banner.actions.flatMap((a) => {
    const onPress = bannerAction(a.target, voyages);
    return onPress ? [{ icon: a.icon as IconName, label: a.label, onPress }] : [];
  }).slice(0, 4);
  return (
    <View style={[styles.imageBanner, { width, height: (width * 9) / 16 }]}>
      {/* Image et boutons côte à côte (pas de bouton dans un bouton) */}
      <Pressable accessibilityRole={open ? 'button' : 'image'} accessibilityLabel={banner.title} disabled={!open} onPress={open ?? undefined}
        style={StyleSheet.absoluteFill}>
        <Image source={imageSource(banner.imagePath)} style={StyleSheet.absoluteFill} contentFit="cover" transition={150} accessibilityIgnoresInvertColors />
      </Pressable>
      {actions.length > 0 && (
        <View style={styles.imageChips}>
          {actions.map((a) => <HeroChip key={a.label} {...a} />)}
        </View>
      )}
    </View>
  );
}

/**
 * Bannière d'accueil : celles de la console si au moins une est en ligne, sinon trois diapositives par défaut.
 * Haute à la première visite, plus compacte ensuite pour laisser la place au contenu.
 */
export function HomeHero({ compact, voyages, banners }: { compact: boolean; voyages: boolean; banners?: HomeBanner[] }) {
  const { width } = useWindowDimensions();
  const [index, setIndex] = useState(0);
  const c = useColors();
  const w = Math.min(width, 640) - 32;
  const height = compact ? 186 : 234;
  const custom = banners && banners.length > 0 ? banners : null;
  const slides = custom ? [] : defaultSlides(voyages);
  const count = custom ? custom.length : slides.length;

  return (
    <View>
      <ScrollView horizontal decelerationRate="fast" snapToInterval={w + 12} snapToAlignment="start"
        showsHorizontalScrollIndicator={false} contentContainerStyle={{ paddingHorizontal: 16, gap: 12 }}
        onMomentumScrollEnd={(e) => setIndex(Math.round(e.nativeEvent.contentOffset.x / (w + 12)))}>
        {custom
          ? custom.map((b) => <ImageBanner key={b.id} banner={b} width={w} voyages={voyages} />)
          : slides.map((s) => (
            <LinearGradient key={s.key} colors={BRAND} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={[styles.hero, { width: w, height }]}>
              {s.image && (
                <View style={[styles.heroImage, { width: w * 0.52, height: w * 0.52 * 0.633 }]} pointerEvents="none">
                  <Image source={s.image} style={StyleSheet.absoluteFill} contentFit="cover" accessibilityIgnoresInvertColors />
                  <LinearGradient colors={[BRAND[1], 'rgba(8,127,91,0)', 'rgba(8,127,91,0)']} locations={[0, 0.25, 1]} start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }} style={StyleSheet.absoluteFill} />
                  <LinearGradient colors={['rgba(8,127,91,0.95)', 'rgba(8,127,91,0)', 'rgba(8,127,91,0)']} locations={[0, 0.3, 1]} start={{ x: 0, y: 0 }} end={{ x: 0, y: 1 }} style={StyleSheet.absoluteFill} />
                </View>
              )}
              {s.icons && <IconCluster icons={s.icons} />}
              {!compact && s.key === 'intro' && (
                <View style={styles.brand}><BeninFlag /><Text style={styles.brandText}>BéninConnect</Text></View>
              )}
              <Text style={[styles.heroTitle, { maxWidth: w * (s.image ? 0.6 : 0.58) }]} numberOfLines={3}>{s.title}</Text>
              {!!s.text && <Text style={[styles.heroText, { maxWidth: w * 0.6 }]} numberOfLines={s.image ? 1 : 2}>{s.text}</Text>}
              {!compact && s.more && <Text style={[styles.heroText, { maxWidth: w * 0.6 }]} numberOfLines={1}>{s.more}</Text>}
              <View style={styles.chips}>
                {s.actions.map((a) => <HeroChip key={a.label} {...a} />)}
              </View>
            </LinearGradient>
          ))}
      </ScrollView>
      {count > 1 && (
        <View style={styles.dots}>
          {Array.from({ length: count }, (_, i) => (
            <View key={i} style={[styles.pageDot, { backgroundColor: i === index ? c.primary : c.border, width: i === index ? 18 : 7 }]} />
          ))}
        </View>
      )}
    </View>
  );
}

// --- Actions rapides -------------------------------------------------------------------------

/** Deux raccourcis : offres d'emploi (mis en avant) et services entre membres. */
export function QuickActions() {
  const c = useColors();
  const items: { icon: IconName; label: string; href: Href }[] = [
    { icon: 'briefcase-outline', label: "Voir les offres d'emploi", href: '/jobs' },
    { icon: 'hand-heart-outline', label: 'Voir les services', href: '/services' },
  ];
  return (
    <View style={styles.quick}>
      {items.map((a, i) => {
        const primary = i === 0;
        const color = primary ? '#FFFFFF' : c.primary;
        return (
          <Pressable key={a.label} accessibilityRole="button" accessibilityLabel={a.label} onPress={() => router.push(a.href)}
            style={({ pressed }) => [styles.quickItem, styles.flat,
              { backgroundColor: primary ? c.primary : c.primaryLight, borderColor: primary ? c.primary : alpha(c.primary, 0.18) },
              pressed && { opacity: 0.85 }]}>
            <Icon name={a.icon} size={26} color={color} />
            <Text numberOfLines={2} adjustsFontSizeToFit minimumFontScale={0.85} style={[styles.quickText, { color: primary ? '#FFFFFF' : c.text }]}>{a.label}</Text>
            <Icon name="arrow-right" size={18} color={color} />
          </Pressable>
        );
      })}
    </View>
  );
}

// --- Communautés -----------------------------------------------------------------------------

/** Communauté suggérée : couverture, membres et bouton Rejoindre. */
export function HomeCommunityCard({ community, members }: { community: Community; members: User[] }) {
  const c = useColors();
  const n = community.memberIds.length;
  return (
    <Pressable onPress={() => router.push(`/community/${community.id}`)}
      style={({ pressed }) => [styles.community, styles.flat, { backgroundColor: c.surface, borderColor: c.border }, depth(c, 1), pressed && { opacity: 0.9 }]}>
      <Cover path={community.coverImage} height={96} />
      <View style={{ padding: 12, gap: 6 }}>
        <T v="label" lines={1} style={{ fontSize: 15 }}>{community.name}</T>
        <View style={styles.row}>
          <Icon name="account-group-outline" size={15} color={c.textSecondary} />
          <Muted lines={1}>{n === 0 ? 'Aucun membre' : `${n.toLocaleString('fr-FR')} membre${n > 1 ? 's' : ''}`}</Muted>
        </View>
        <View style={[styles.row, { justifyContent: 'space-between', marginTop: 4 }]}>
          <View style={styles.row}>
            {members.slice(0, 3).map((u, i) => (
              <View key={u.id} style={{ marginLeft: i === 0 ? 0 : -8, borderRadius: 14, borderWidth: 2, borderColor: c.surface }}>
                <Avatar initials={initials(u)} color={u.avatarColor} image={u.avatarImage} size={24} />
              </View>
            ))}
          </View>
          <MembershipButton community={community} small />
        </View>
      </View>
    </Pressable>
  );
}

/** Communautés déjà rejointes : accès direct, avant les suggestions. */
export function MyCommunities({ communities }: { communities: Community[] }) {
  const c = useColors();
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={[styles.hList, { gap: 10 }]}>
      {communities.map((x) => (
        <Pressable key={x.id} accessibilityRole="button" accessibilityLabel={x.name} onPress={() => router.push(`/community/${x.id}`)}
          style={({ pressed }) => [styles.mine, styles.flat, { backgroundColor: c.surface, borderColor: c.border }, pressed && { opacity: 0.8 }]}>
          <Cover path={x.coverImage} height={40} style={{ width: 40, borderRadius: 12 }} />
          <T v="label" lines={2} style={{ flex: 1 }}>{x.name}</T>
        </Pressable>
      ))}
    </ScrollView>
  );
}

// --- Voyages ---------------------------------------------------------------------------------

/** Section voyages : visuel « Tu voyages ? » (texte compris), toute la carte ouvre les trajets. */
export function VoyagesPromo() {
  // Taille calculée comme la bannière du haut (aspectRatio seul donne une carte trop étroite sur iOS)
  const { width } = useWindowDimensions();
  const w = Math.min(width, 640) - 32;
  return (
    <Pressable accessibilityRole="button" accessibilityLabel="Tu voyages ? Rentabilise tes kilos disponibles. Voir les trajets"
      onPress={() => router.push('/voyages')} style={({ pressed }) => [styles.promo, { width: w, height: (w * 470) / 1500 }, pressed && { opacity: 0.9 }]}>
      <Image source={VOYAGES} style={StyleSheet.absoluteFill} contentFit="cover" accessibilityIgnoresInvertColors />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  hList: { paddingHorizontal: 16, gap: 12 },
  flat: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 18 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  header: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingBottom: 14 },
  online: { position: 'absolute', right: 0, bottom: 1, width: 13, height: 13, borderRadius: 7, borderWidth: 2 },
  round: { width: 46, height: 46, borderRadius: 23, alignItems: 'center', justifyContent: 'center', borderWidth: StyleSheet.hairlineWidth },
  dot: { position: 'absolute', top: -3, right: -3, minWidth: 19, height: 19, borderRadius: 10, paddingHorizontal: 4, borderWidth: 2, alignItems: 'center', justifyContent: 'center' },
  dotText: { color: '#FFFFFF', fontFamily: font.bold, fontSize: 10 },
  hero: { borderRadius: 24, padding: 16, overflow: 'hidden' },
  heroImage: { position: 'absolute', right: 0, bottom: 0 },
  imageBanner: { borderRadius: 24, overflow: 'hidden', backgroundColor: '#087F5B' },
  imageChips: { position: 'absolute', left: 12, right: 12, bottom: 12, flexDirection: 'row', gap: 8 },
  brand: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 6 },
  flag: { width: 20, height: 14, borderRadius: 3, overflow: 'hidden', flexDirection: 'row' },
  brandText: { color: '#FFFFFF', fontFamily: font.semibold, fontSize: 15 },
  heroTitle: { color: '#FFFFFF', fontFamily: font.bold, fontSize: 19, lineHeight: 24 },
  heroText: { color: 'rgba(255,255,255,0.88)', fontFamily: font.medium, fontSize: 12.5, lineHeight: 18, marginTop: 4 },
  chips: { flexDirection: 'row', gap: 8, marginTop: 'auto' },
  chip: { flex: 1, paddingHorizontal: 4, height: 50, borderRadius: 14, backgroundColor: '#FFFFFF', alignItems: 'center', justifyContent: 'center', gap: 2 },
  chipText: { color: '#111827', fontFamily: font.semibold, fontSize: 10.5 },
  cluster: { position: 'absolute', top: 14, right: 0, width: 190, height: 140 },
  clusterItem: { position: 'absolute', borderRadius: 999, backgroundColor: 'rgba(255,255,255,0.16)', alignItems: 'center', justifyContent: 'center' },
  dots: { flexDirection: 'row', justifyContent: 'center', gap: 6, marginTop: 10 },
  pageDot: { height: 7, borderRadius: 4 },
  quick: { flexDirection: 'row', gap: 8, paddingHorizontal: 16, marginTop: 14 },
  quickItem: { flex: 1, height: 80, flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 12 },
  quickText: { flex: 1, fontFamily: font.semibold, fontSize: 14, lineHeight: 18 },
  community: { width: 250, overflow: 'hidden' },
  mine: { width: 190, flexDirection: 'row', alignItems: 'center', gap: 10, padding: 10 },
  // Visuel 1500 × 470 : taille fixée dans VoyagesPromo, coins arrondis comme sur l'image
  promo: { marginHorizontal: 16, marginTop: 16, borderRadius: 20, overflow: 'hidden', backgroundColor: '#DDF9F0' },
});
