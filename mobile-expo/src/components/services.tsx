import { router } from 'expo-router';
import { Alert, Platform, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { useColors } from '@/core/theme';
import { CITIES, cityLabel, type City } from '@/data/cities';
import { servicePrice, type ProviderCard, type ServiceCategory, type VerificationLevel } from '@/data/types';
import { useToggleProviderFavorite } from '@/features/queries';
import { Avatar, Card, Icon, type IconName, IconButton, Muted, Select, T, Tag } from './ui';

/** 90 → « 1 h 30 », 45 → « 45 min ». */
export const durationText = (minutes: number) =>
  minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h${minutes % 60 ? ` ${String(minutes % 60).padStart(2, '0')}` : ''}`;

export const initialsOf = (name: string) => name.split(' ').filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('');

export function Stars({ value, size = 14 }: { value: number; size?: number }) {
  const c = useColors();
  return (
    <View style={{ flexDirection: 'row' }} accessibilityLabel={`${value.toFixed(1)} sur 5`}>
      {[1, 2, 3, 4, 5].map((i) => (
        <Icon key={i} size={size} color={c.warning} name={value >= i ? 'star' : value >= i - 0.5 ? 'star-half-full' : 'star-outline'} />
      ))}
    </View>
  );
}

export function Rating({ average, count }: { average: number; count: number }) {
  return count === 0
    ? <Muted>Nouveau</Muted>
    : <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4 }}><Stars value={average} /><Muted>{average.toFixed(1)} ({count})</Muted></View>;
}

export function VerificationTag({ level }: { level: VerificationLevel }) {
  if (level === 'None') return null;
  return <Tag label={level === 'Pro' ? '✓ Pro vérifié' : '✓ Identité vérifiée'} tone={level === 'Pro' ? 'primary' : 'neutral'} />;
}

export function ProviderCardView({ p }: { p: ProviderCard }) {
  const c = useColors();
  const favorite = useToggleProviderFavorite();
  return (
    <Card style={styles.card} onPress={() => router.push(`/services/provider/${p.id}`)}>
      <View style={styles.row}>
        <Avatar initials={initialsOf(p.displayName)} color={c.primary} image={p.photoPath} size={56} />
        <View style={{ flex: 1 }}>
          <T v="label" style={{ fontSize: 16 }} lines={1}>{p.displayName}</T>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 2 }}>
            <Icon name={p.categoryIcon as IconName} size={14} color={c.primary} />
            <Muted lines={1}>{p.categoryName}</Muted>
          </View>
          <View style={{ marginTop: 4 }}><Rating average={p.ratingAverage} count={p.ratingCount} /></View>
        </View>
        <IconButton name={p.isFavorite ? 'heart' : 'heart-outline'} color={p.isFavorite ? c.error : c.textSecondary}
          label={p.isFavorite ? 'Retirer des favoris' : 'Ajouter aux favoris'} onPress={() => favorite.mutate({ id: p.id, favorite: !p.isFavorite })} />
      </View>
      <View style={styles.tags}>
        <VerificationTag level={p.verificationLevel} />
        {p.servesAtHome && <Tag label="À domicile" tone="neutral" />}
        {p.hasPremises && <Tag label="Sur place" tone="neutral" />}
      </View>
      <View style={[styles.footer, { borderColor: c.border }]}>
        <Icon name="map-marker-outline" size={16} color={c.textSecondary} />
        <Muted style={{ flex: 1 }} lines={1}>{p.city}{p.distanceKm != null ? ` · à ${p.distanceKm} km` : ''}</Muted>
        <T v="label" color={c.primary}>{p.priceFrom != null ? servicePrice({ priceType: 'From', priceAmount: p.priceFrom, currency: p.currency }) : 'Sur devis'}</T>
      </View>
    </Card>
  );
}

/** Vignette horizontale (accueil). */
export function ProviderTile({ p }: { p: ProviderCard }) {
  const c = useColors();
  return (
    <Card style={styles.tile} onPress={() => router.push(`/services/provider/${p.id}`)}>
      <Avatar initials={initialsOf(p.displayName)} color={c.primary} image={p.photoPath} size={48} />
      <T v="label" lines={1} style={{ marginTop: 8 }}>{p.displayName}</T>
      <Muted lines={1}>{p.categoryName}</Muted>
      <View style={{ marginTop: 6 }}><Rating average={p.ratingAverage} count={p.ratingCount} /></View>
      <Muted lines={1} style={{ marginTop: 4 }}>{p.city}{p.distanceKm != null ? ` · ${p.distanceKm} km` : ''}</Muted>
    </Card>
  );
}

export function CategoryGrid({ categories, onSelect }: { categories: ServiceCategory[]; onSelect: (c: ServiceCategory) => void }) {
  const c = useColors();
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ paddingHorizontal: 16, gap: 10 }}>
      {categories.map((cat) => (
        <Pressable key={cat.id} accessibilityRole="button" accessibilityLabel={cat.name} onPress={() => onSelect(cat)}
          style={({ pressed }) => [styles.category, { backgroundColor: c.surface, borderColor: c.border, opacity: pressed ? 0.7 : 1 }]}>
          <View style={[styles.categoryIcon, { backgroundColor: c.primaryLight }]}><Icon name={cat.icon as IconName} size={24} color={c.primary} /></View>
          <T v="caption" lines={2} style={{ textAlign: 'center' }}>{cat.name}</T>
        </Pressable>
      ))}
    </ScrollView>
  );
}

const cityOptions = CITIES.map((c) => ({ value: cityLabel(c) as string | null, label: cityLabel(c) }));

/** Ville choisie dans la liste (position connue, sans géolocalisation du téléphone). */
export function CityPicker({ value, onChange, label = 'Ville', allowAll }: {
  value: City | null; onChange: (c: City | null) => void; label?: string; allowAll?: string;
}) {
  return (
    <Select label={label} value={value ? cityLabel(value) : null} placeholder="Choisir une ville"
      options={allowAll ? [{ value: null, label: allowAll }, ...cityOptions] : cityOptions}
      onChange={(v) => onChange(CITIES.find((c) => cityLabel(c) === v) ?? null)} />
  );
}

export function Notice({ icon = 'information-outline', children }: { icon?: IconName; children: React.ReactNode }) {
  const c = useColors();
  return (
    <View style={[styles.notice, { backgroundColor: c.surfaceVariant }]}>
      <Icon name={icon} size={18} color={c.textSecondary} />
      <Muted style={{ flex: 1 }}>{children}</Muted>
    </View>
  );
}

const REASONS = ['Arnaque ou demande d’argent', 'Contenu trompeur', 'Propos inappropriés', 'Autre raison'];

export function askServiceReport(title: string, onReport: (reason: string) => void) {
  if (Platform.OS === 'web') {
    if (typeof window !== 'undefined' && window.confirm(`${title} ?`)) onReport(REASONS[0]);
    return;
  }
  Alert.alert(title, 'Que se passe-t-il ?', [
    ...REASONS.map((r) => ({ text: r, onPress: () => onReport(r) })),
    { text: 'Annuler', style: 'cancel' as const },
  ]);
}

const styles = StyleSheet.create({
  card: { marginHorizontal: 16, marginBottom: 12, padding: 14 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  tags: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10 },
  footer: { flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: 10, paddingTop: 10, borderTopWidth: StyleSheet.hairlineWidth },
  tile: { width: 170, marginRight: 12, padding: 12 },
  category: { width: 92, alignItems: 'center', gap: 6, paddingVertical: 12, paddingHorizontal: 6, borderRadius: 16, borderWidth: StyleSheet.hairlineWidth },
  categoryIcon: { width: 48, height: 48, borderRadius: 24, alignItems: 'center', justifyContent: 'center' },
  notice: { flexDirection: 'row', gap: 8, padding: 12, borderRadius: 12, marginTop: 12 },
});
