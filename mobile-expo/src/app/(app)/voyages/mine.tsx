import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Alert, FlatList, Platform, RefreshControl, Share, StyleSheet, View } from 'react-native';
import { Button, Card, EmptyState, Header, Muted, QueryView, Screen, Segmented, T, Tag } from '@/components/ui';
import { VoyagesGate } from '@/components/VoyagesGate';
import { askReason, BookingStatusTag, PersonAvatar, tripDay, TripRoute } from '@/components/voyages';
import { confirm } from '@/core/dialog';
import * as fmt from '@/core/format';
import { useColors } from '@/core/theme';
import { type Booking, kg, type MyTrip, PARCEL_LABELS, TRIP_STATUS_LABELS } from '@/data/types';
import {
  useCancelTrip, useDisputeBooking, useIssueDeliveryCode, useMyBookings, useMyTrips, useRespondBooking,
} from '@/features/queries';
import { usePullRefresh } from '@/core/refresh';

type Tab = 'trips' | 'sent';

const DISPUTE_REASONS = ['Colis non remis ou non livré', 'Contenu différent de l’annonce', 'L’autre membre ne répond plus', 'Autre problème'];

/** Mes voyages (réservations reçues) et mes envois (code de remise, avis). */
export default function MyVoyagesScreen() {
  return <VoyagesGate><MyVoyages /></VoyagesGate>;
}

function MyVoyages() {
  const c = useColors();
  const params = useLocalSearchParams<{ tab?: Tab }>();
  const [tab, setTab] = useState<Tab>(params.tab === 'sent' ? 'sent' : 'trips');
  const trips = useMyTrips();
  const pullTrips = usePullRefresh(() => trips.refetch());
  const sent = useMyBookings();
  const pullSent = usePullRefresh(() => sent.refetch());
  return (
    <Screen>
      <Header title="Mes voyages et envois" />
      <View style={{ padding: 16, paddingBottom: 4 }}>
        <Segmented value={tab} onChange={setTab} items={[{ key: 'trips', label: 'Mes voyages' }, { key: 'sent', label: 'Mes envois' }]} />
      </View>
      {tab === 'trips' ? (
        <QueryView query={trips}>
          {(items) => (
            <FlatList data={items} keyExtractor={(t) => t.detail.trip.id} renderItem={({ item }) => <TripBlock m={item} />}
              ListEmptyComponent={<EmptyState icon="airplane-takeoff" title="Aucun voyage publié" message="Vous partez bientôt ? Proposez vos kilos libres."
                action="Proposer mes kilos" onAction={() => router.push('/voyages/publish')} />}
              refreshControl={<RefreshControl refreshing={pullTrips.refreshing} onRefresh={pullTrips.onRefresh} tintColor={c.primary} />}
              contentContainerStyle={{ paddingTop: 8, paddingBottom: 32 }} />
          )}
        </QueryView>
      ) : (
        <QueryView query={sent}>
          {(items) => (
            <FlatList data={items} keyExtractor={(b) => b.id} renderItem={({ item }) => <BookingCard b={item} />}
              ListEmptyComponent={<EmptyState icon="package-variant" title="Aucun envoi" message="Trouvez un membre qui part et réservez quelques kilos."
                action="Chercher un trajet" onAction={() => router.replace('/voyages')} />}
              refreshControl={<RefreshControl refreshing={pullSent.refreshing} onRefresh={pullSent.onRefresh} tintColor={c.primary} />}
              contentContainerStyle={{ paddingTop: 8, paddingBottom: 32 }} />
          )}
        </QueryView>
      )}
    </Screen>
  );
}

function TripBlock({ m }: { m: MyTrip }) {
  const t = m.detail.trip;
  const cancel = useCancelTrip();
  const active = t.status === 'Open' || t.status === 'Full';
  const editable = active && !m.bookings.some((b) => ['Accepted', 'Delivered', 'Disputed'].includes(b.status));
  return (
    <Card style={styles.card}>
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <TripRoute t={t} />
          <Muted>{`${tripDay(t.departureDate, true)} · ${kg(t.capacityKg - t.remainingKg)} réservés sur ${kg(t.capacityKg)}`}</Muted>
        </View>
        <Tag label={m.detail.hiddenReason !== 'None' ? 'Masqué' : TRIP_STATUS_LABELS[t.status]} tone={active ? 'primary' : 'neutral'} />
      </View>
      {m.pendingCount > 0 && <T v="label" style={{ marginTop: 8 }}>{fmt.plural(m.pendingCount, 'demande à traiter', 'demandes à traiter')}</T>}
      <View style={styles.actions}>
        <Button title="Voir" kind="secondary" small icon="eye-outline" onPress={() => router.push(`/voyages/trip/${t.id}`)} />
        {editable && <Button title="Modifier" kind="secondary" small icon="pencil-outline" onPress={() => router.push({ pathname: '/voyages/publish', params: { id: t.id } })} />}
        {active && <Button title="Annuler le trajet" kind="secondary" small onPress={async () => {
          if (await confirm('Annuler ce trajet ?', 'Les réservations en cours seront annulées et les expéditeurs prévenus.', 'Annuler le trajet', true)) cancel.mutate(t.id);
        }} />}
      </View>
      {m.bookings.map((b) => <View key={b.id} style={styles.nested}><BookingCard b={b} nested /></View>)}
    </Card>
  );
}

function showCode(code: string, shareText: string) {
  const share = () => void Share.share({ message: shareText });
  if (Platform.OS === 'web') {
    if (typeof window !== 'undefined') window.alert(`Code de remise : ${code}`);
    return;
  }
  Alert.alert(`Code de remise : ${code}`,
    'Transmettez-le à votre destinataire. Il le donnera au voyageur à la réception du colis, pas avant. Ce code ne sera plus affiché.',
    [{ text: 'Partager', onPress: share }, { text: 'OK' }]);
}

function BookingCard({ b, nested }: { b: Booking; nested?: boolean }) {
  const respond = useRespondBooking();
  const issue = useIssueDeliveryCode();
  const dispute = useDisputeBooking();
  const other = b.iAmTraveler ? b.sender : b.traveler;
  const open = b.status === 'Pending' || b.status === 'Accepted';

  const content = (
    <>
      <View style={styles.row}>
        <PersonAvatar p={other} size={nested ? 32 : 40} />
        <View style={{ flex: 1 }}>
          <T v="label" lines={1}>{b.iAmTraveler ? `${other.name} · ${kg(b.weightKg)}` : `${b.fromCity} → ${b.toCity}`}</T>
          <Muted lines={1}>{b.iAmTraveler ? `${PARCEL_LABELS[b.category]} · ${fmt.relative(b.createdAt)}` : `${tripDay(b.departureDate)} · ${other.name} · ${kg(b.weightKg)}`}</Muted>
        </View>
        <BookingStatusTag status={b.status} />
      </View>
      <T style={{ marginTop: 6 }} lines={3}>{b.description}</T>
      {!!b.declineReason && <Muted style={{ marginTop: 4 }}>{`Motif : ${b.declineReason}`}</Muted>}
      {!!b.disputeReason && <Muted style={{ marginTop: 4 }}>{`Litige : ${b.disputeReason}`}</Muted>}
      {!!b.resolutionNote && <Muted style={{ marginTop: 4 }}>{`Décision : ${b.resolutionNote}`}</Muted>}
      {!b.iAmTraveler && b.status === 'Accepted' && (
        <Muted style={{ marginTop: 4 }}>
          {b.codeLocked ? 'Code bloqué après trop d’essais : générez-en un nouveau.' : b.hasCode ? 'Code de remise transmis.' : 'Générez le code et envoyez-le à votre destinataire.'}
        </Muted>
      )}
      <View style={styles.actions}>
        {b.conversationId && <Button title="Conversation" kind="secondary" small icon="message-outline" onPress={() => router.push(`/conversation/${b.conversationId}`)} />}
        {b.iAmTraveler && b.status === 'Pending' && <>
          <Button title="Accepter" small icon="check" loading={respond.isPending} onPress={() => respond.mutate({ id: b.id, action: 'accept' })} />
          <Button title="Refuser" kind="secondary" small onPress={async () => {
            const reason = await askReason('Refuser cette demande', 'L’expéditeur sera prévenu.',
              ['Plus assez de place', 'Ce type de colis ne me convient pas'], 'Refuser sans motif');
            if (reason !== false) respond.mutate({ id: b.id, action: 'decline', reason: reason ?? undefined });
          }} />
        </>}
        {b.iAmTraveler && b.status === 'Accepted' && (
          <Button title="Confirmer la livraison" small icon="package-variant-closed-check"
            onPress={() => router.push({ pathname: '/voyages/deliver', params: { id: b.id, name: b.sender.name } })} />
        )}
        {!b.iAmTraveler && b.status === 'Accepted' && (
          <Button title={b.hasCode || b.codeLocked ? 'Nouveau code' : 'Obtenir le code'} small icon="key-outline" loading={issue.isPending}
            onPress={async () => {
              if (b.hasCode && !(await confirm('Générer un nouveau code ?', 'L’ancien code ne fonctionnera plus.', 'Générer'))) return;
              issue.mutate(b.id, { onSuccess: (r) => showCode(r.code, r.shareText) });
            }} />
        )}
        {open && <Button title="Annuler" kind="secondary" small onPress={async () => {
          if (await confirm('Annuler la réservation ?', 'L’autre membre sera prévenu.', 'Annuler la réservation', true)) respond.mutate({ id: b.id, action: 'cancel' });
        }} />}
        {(b.status === 'Accepted' || b.status === 'Delivered') && <Button title="Signaler un problème" kind="secondary" small icon="alert-outline" onPress={async () => {
          const reason = await askReason('Ouvrir un litige', 'L’équipe BéninConnect examinera la situation. Les avis sont suspendus en attendant.', DISPUTE_REASONS);
          if (reason) dispute.mutate({ id: b.id, reason });
        }} />}
        {b.canReview && <Button title="Laisser un avis" small icon="star" onPress={() =>
          router.push({ pathname: '/voyages/review', params: { id: b.id, name: other.name } })} />}
      </View>
    </>
  );
  return nested ? content : <Card style={styles.card}>{content}</Card>;
}

const styles = StyleSheet.create({
  card: { marginHorizontal: 16, marginBottom: 12, padding: 14 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 10 },
  nested: { marginTop: 12, paddingTop: 12, borderTopWidth: StyleSheet.hairlineWidth, borderColor: 'rgba(127,127,127,0.25)' },
});
