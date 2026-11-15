import { router } from 'expo-router';
import { Alert, Platform, Pressable, StyleSheet, View } from 'react-native';
import { initialsOf, Notice, Stars } from '@/components/services';
import { CityField, CountryField, countryError } from '@/components/places';
import { Avatar, Card, Chip, Icon, type IconName, Muted, T, Tag } from '@/components/ui';
import { useColors } from '@/core/theme';
import { countryCode, countryName } from '@/data/cities';
import { type Place, tripDay } from '@/data/voyageFormat';

export { formatDay, isoDay, parseDay, type Place, placeLabel, tripDay } from '@/data/voyageFormat';
import {
  BOOKING_STATUS_LABELS, type BookingStatus, kg, PARCEL_ICONS, PARCEL_LABELS, type ParcelCategory, type TripCard, tripPrice, type VoyagePerson,
} from '@/data/types';

export const PARCEL_CATEGORIES = Object.keys(PARCEL_LABELS) as ParcelCategory[];

/** Ville et pays d'une recherche ou d'une alerte, saisis librement. */
export function PlaceFields({ label, city, country, onCity, onCountry }: {
  label: string; city: string; country: string; onCity: (t: string) => void; onCountry: (t: string) => void;
}) {
  return (
    <View style={styles.place}>
      <CityField style={{ flex: 1 }} label={label} value={city} onChangeText={onCity} placeholder="Ville"
        onPick={(c) => onCountry(countryName(c.country))} />
      <CountryField style={{ flex: 1 }} label="Pays" value={country} onChangeText={onCountry} placeholder="Pays" error={countryError(country)} />
    </View>
  );
}

/** Ville et pays saisis → filtre (pays en code, ville si au moins 2 lettres). */
export const placeFilter = (city: string, country: string): Place | null => {
  const code = countryCode(country);
  const name = city.trim().length >= 2 ? city.trim() : null;
  return code || name ? { country: code ?? '', city: name } : null;
};

export function CategoryChips({ value, onChange, allowed = PARCEL_CATEGORIES }: {
  value: ParcelCategory[]; onChange: (v: ParcelCategory[]) => void; allowed?: ParcelCategory[];
}) {
  return (
    <View style={styles.chips}>
      {allowed.map((cat) => (
        <Chip key={cat} label={PARCEL_LABELS[cat]} icon={PARCEL_ICONS[cat] as IconName} selected={value.includes(cat)}
          onPress={() => onChange(value.includes(cat) ? value.filter((x) => x !== cat) : [...value, cat])} />
      ))}
    </View>
  );
}

export function PersonAvatar({ p, size = 40 }: { p: VoyagePerson; size?: number }) {
  return <Avatar initials={initialsOf(p.name.replace(/\(.*\)/, ''))} color={p.avatarColor} image={p.avatarImage} size={size} />;
}

export function TrustedTag() {
  return <Tag label="Voyageur de confiance" />;
}

const TONES: Record<BookingStatus, 'primary' | 'warning' | 'neutral'> = {
  Pending: 'warning', Accepted: 'primary', Declined: 'neutral', Cancelled: 'neutral', Delivered: 'primary', Disputed: 'warning',
};

export const BookingStatusTag = ({ status }: { status: BookingStatus }) => <Tag label={BOOKING_STATUS_LABELS[status]} tone={TONES[status]} />;

function Route({ t, big }: { t: Pick<TripCard, 'fromCity' | 'toCity'>; big?: boolean }) {
  const c = useColors();
  return (
    <View style={styles.route}>
      <T v={big ? 'h3' : 'label'} style={!big && { fontSize: 15 }} lines={1}>{t.fromCity}</T>
      <Icon name="airplane" size={big ? 20 : 16} color={c.primary} />
      <T v={big ? 'h3' : 'label'} style={[{ flexShrink: 1 }, !big && { fontSize: 15 }]} lines={1}>{t.toCity}</T>
    </View>
  );
}

export { Route as TripRoute };

/** Carte d'un trajet dans la liste. */
export function TripCardView({ t }: { t: TripCard }) {
  const c = useColors();
  return (
    <Card style={styles.card} onPress={() => router.push(`/voyages/trip/${t.id}`)}>
      <View style={styles.row}>
        <PersonAvatar p={t.traveler} />
        <View style={{ flex: 1 }}>
          <T v="label" lines={1}>{t.traveler.name}</T>
          {t.travelerReviewCount > 0
            ? <View style={styles.row}><Stars value={t.travelerRating} size={12} /><Muted style={{ fontSize: 12 }}>{`${t.travelerRating} (${t.travelerReviewCount})`}</Muted></View>
            : <Muted style={{ fontSize: 12 }}>Nouveau voyageur</Muted>}
        </View>
        {t.trustedTraveler && <Icon name="shield-check" size={20} color={c.primary} />}
      </View>
      <View style={{ marginTop: 10 }}><Route t={t} /></View>
      <Muted style={{ marginTop: 2 }}>{`${tripDay(t.departureDate)} · ${countryName(t.fromCountry)} → ${countryName(t.toCountry)}`}</Muted>
      <View style={[styles.footer, { borderColor: c.border }]}>
        <Icon name="bag-suitcase-outline" size={16} color={c.primary} />
        <T v="label" color={c.primary}>{`${kg(t.remainingKg)} disponibles`}</T>
        <View style={{ flex: 1 }} />
        <Muted>{tripPrice(t)}</Muted>
      </View>
      <View style={styles.chips}>
        {t.acceptedCategories.map((cat) => <Tag key={cat} label={PARCEL_LABELS[cat]} tone="neutral" />)}
      </View>
    </Card>
  );
}

/** Vignette compacte (accueil, profil). */
export function TripTile({ t }: { t: TripCard }) {
  const c = useColors();
  return (
    <Card style={styles.tile} onPress={() => router.push(`/voyages/trip/${t.id}`)}>
      <View style={styles.row}>
        <PersonAvatar p={t.traveler} size={28} />
        <Muted lines={1} style={{ flex: 1 }}>{t.traveler.name}</Muted>
      </View>
      <View style={{ marginTop: 8 }}><Route t={t} /></View>
      <Muted style={{ marginTop: 2 }}>{tripDay(t.departureDate)}</Muted>
      <T v="label" color={c.primary} style={{ marginTop: 6 }}>{`${kg(t.remainingKg)} dispo.`}</T>
    </Card>
  );
}

export function ProhibitedNotice({ items }: { items: string[] }) {
  return (
    <Notice icon="alert-octagon-outline">
      {`Objets interdits : ${items.join(', ').toLowerCase()}.`}
    </Notice>
  );
}

export const VOYAGE_REPORT_REASONS = ['Annonce suspecte ou arnaque', 'Objets interdits', 'Comportement inapproprié', 'Autre raison'];

export function askVoyageReport(title: string, onReport: (reason: string) => void) {
  if (Platform.OS === 'web') {
    if (typeof window !== 'undefined' && window.confirm(`${title} ?`)) onReport(VOYAGE_REPORT_REASONS[0]!);
    return;
  }
  Alert.alert(title, 'Que se passe-t-il ?', [
    ...VOYAGE_REPORT_REASONS.map((r) => ({ text: r, onPress: () => onReport(r) })),
    { text: 'Annuler', style: 'cancel' as const },
  ]);
}

/** Choix d'un motif (refus, litige) ; false si annulé. */
export function askReason(title: string, message: string, reasons: string[], allowNone?: string): Promise<string | null | false> {
  if (Platform.OS === 'web') return Promise.resolve(typeof window !== 'undefined' && window.confirm(title) ? reasons[0] ?? null : false);
  return new Promise((resolve) => Alert.alert(title, message, [
    ...reasons.map((r) => ({ text: r, onPress: () => resolve(r) })),
    ...(allowNone ? [{ text: allowNone, style: 'destructive' as const, onPress: () => resolve(null) }] : []),
    { text: 'Annuler', style: 'cancel' as const, onPress: () => resolve(false) },
  ]));
}

export function Pill({ icon, label }: { icon: IconName; label: string }) {
  const c = useColors();
  return (
    <View style={[styles.pill, { backgroundColor: c.surfaceVariant }]}>
      <Icon name={icon} size={14} color={c.textSecondary} />
      <Muted style={{ fontSize: 12 }}>{label}</Muted>
    </View>
  );
}

export function LinkRow({ icon, label, onPress }: { icon: IconName; label: string; onPress: () => void }) {
  const c = useColors();
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={({ pressed }) => [styles.link, pressed && { opacity: 0.6 }]}>
      <Icon name={icon} size={18} color={c.primary} />
      <T v="label" color={c.primary}>{label}</T>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  place: { flexDirection: 'row', gap: 8 },
  card: { marginHorizontal: 16, marginBottom: 12, padding: 14 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  route: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  footer: { flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: 10, paddingTop: 10, borderTopWidth: StyleSheet.hairlineWidth },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10 },
  tile: { width: 180, marginRight: 12, padding: 12 },
  pill: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingHorizontal: 8, paddingVertical: 4, borderRadius: 12 },
  link: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingVertical: 8 },
});
