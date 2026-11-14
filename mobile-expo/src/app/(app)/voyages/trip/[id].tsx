import * as Linking from 'expo-linking';
import { router, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Notice, Stars } from '@/components/services';
import { Button, Card, Header, IconButton, Muted, QueryView, Screen, SectionTitle, T, Tag } from '@/components/ui';
import { VoyagesGate } from '@/components/VoyagesGate';
import {
  askVoyageReport, BookingStatusTag, LinkRow, PersonAvatar, Pill, ProhibitedNotice, tripDay, TripRoute, TrustedTag,
} from '@/components/voyages';
import * as fmt from '@/core/format';
import { useColors } from '@/core/theme';
import { countryName } from '@/data/cities';
import { kg, PARCEL_LABELS, type TripDetail, tripPrice, TRIP_STATUS_LABELS } from '@/data/types';
import { useReportVoyage, useTrip, useVoyageReviews, useVoyagesInfo } from '@/features/queries';

export default function TripScreenScreen() {
  return <VoyagesGate><TripScreen /></VoyagesGate>;
}

function TripScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useTrip(id);
  const report = useReportVoyage();
  return (
    <Screen>
      <Header title="Trajet" right={query.data && !query.data.trip.isMine ? (
        <IconButton name="flag-outline" label="Signaler ce trajet"
          onPress={() => askVoyageReport('Signaler ce trajet', (reason) => report.mutate({ target: 'Trip', id, reason }))} />
      ) : undefined} />
      <QueryView query={query}>{(d) => <Details d={d} />}</QueryView>
    </Screen>
  );
}

function Details({ d }: { d: TripDetail }) {
  const c = useColors();
  const t = d.trip;
  const info = useVoyagesInfo().data;
  const reviews = useVoyageReviews(t.traveler.id).data;
  const travelerReviews = (reviews?.items ?? []).filter((r) => r.aboutTraveler).slice(0, 5);

  return (
    <ScrollView contentContainerStyle={{ paddingBottom: 40 }}>
      <Card style={styles.card}>
        <TripRoute t={t} big />
        <Muted style={{ marginTop: 4 }}>{`${countryName(t.fromCountry)} → ${countryName(t.toCountry)}`}</Muted>
        <View style={styles.pills}>
          <Pill icon="calendar" label={tripDay(t.departureDate, true)} />
          <Pill icon="bag-suitcase-outline" label={`${kg(t.remainingKg)} sur ${kg(t.capacityKg)}`} />
          <Pill icon="cash" label={tripPrice(t)} />
          {t.status !== 'Open' && <Tag label={TRIP_STATUS_LABELS[t.status]} tone="neutral" />}
        </View>
        <SectionTitle title="Colis acceptés" />
        <View style={styles.pills}>{t.acceptedCategories.map((cat) => <Tag key={cat} label={PARCEL_LABELS[cat]} tone="neutral" />)}</View>
        {!!d.dropOffZones && <><Muted style={styles.label}>Zones de remise</Muted><T>{d.dropOffZones}</T></>}
        {!!d.notes && <><Muted style={styles.label}>Informations</Muted><T>{d.notes}</T></>}
        {d.hiddenReason !== 'None' && <Notice icon="eye-off-outline">Ce trajet est masqué par la modération : il n’apparaît plus dans les recherches.</Notice>}
      </Card>

      <Card style={styles.card} onPress={() => router.push(`/user/${t.traveler.id}`)}>
        <View style={styles.row}>
          <PersonAvatar p={t.traveler} size={48} />
          <View style={{ flex: 1 }}>
            <T v="label" style={{ fontSize: 16 }}>{t.traveler.name}</T>
            {t.travelerReviewCount > 0
              ? <View style={styles.row}><Stars value={t.travelerRating} /><Muted>{`${t.travelerRating} · ${fmt.plural(t.travelerReviewCount, 'avis', 'avis')}`}</Muted></View>
              : <Muted>Pas encore d’avis</Muted>}
            <Muted>{fmt.plural(d.travelerDeliveries, 'livraison réussie', 'livraisons réussies')}</Muted>
          </View>
        </View>
        {t.trustedTraveler && <View style={{ marginTop: 8 }}><TrustedTag /></View>}
      </Card>

      {d.myBooking && (
        <Card style={styles.card}>
          <View style={styles.row}>
            <T v="label" style={{ flex: 1 }}>{`Ma réservation : ${kg(d.myBooking.weightKg)}`}</T>
            <BookingStatusTag status={d.myBooking.status} />
          </View>
          <LinkRow icon="arrow-right" label="Voir mes envois" onPress={() => router.push({ pathname: '/voyages/mine', params: { tab: 'sent' } })} />
        </Card>
      )}

      <View style={{ paddingHorizontal: 16 }}>
        {t.isMine
          ? <Button title="Gérer mon trajet" icon="cog-outline" onPress={() => router.push('/voyages/mine')} />
          : d.canBook && <Button title="Réserver des kilos" icon="bag-suitcase-outline"
            onPress={() => router.push({ pathname: '/voyages/book', params: { tripId: t.id } })} />}
        {info && <ProhibitedNotice items={info.prohibitedItems} />}
        {info && <Notice icon="handshake-outline">{info.intermediation}</Notice>}
        {info?.customsLinks.map((l) => <LinkRow key={l.url} icon="open-in-new" label={l.label} onPress={() => void Linking.openURL(l.url)} />)}
      </View>

      {travelerReviews.length > 0 && (
        <>
          <SectionTitle title="Avis des expéditeurs" />
          {travelerReviews.map((r) => (
            <Card key={r.id} style={styles.card}>
              <View style={styles.row}>
                <Stars value={r.rating} />
                <Muted style={{ flex: 1 }}>{`${r.author.name} · ${fmt.relative(r.createdAt)}`}</Muted>
              </View>
              {!!r.comment && <T style={{ marginTop: 6 }}>{r.comment}</T>}
            </Card>
          ))}
        </>
      )}
      <Muted style={{ paddingHorizontal: 16, marginTop: 8, color: c.textSecondary }}>{`Publié ${fmt.relative(d.createdAt).toLowerCase()}`}</Muted>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  card: { marginHorizontal: 16, marginTop: 12, padding: 14 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  pills: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10 },
  label: { marginTop: 12, marginBottom: 2 },
});
