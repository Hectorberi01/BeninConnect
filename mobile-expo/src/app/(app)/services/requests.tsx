import * as Linking from 'expo-linking';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Alert, FlatList, Platform, RefreshControl, StyleSheet, View } from 'react-native';
import { initialsOf } from '@/components/services';
import { Avatar, Button, Card, EmptyState, Header, Muted, QueryView, Screen, Segmented, T, Tag } from '@/components/ui';
import { confirm } from '@/core/dialog';
import * as fmt from '@/core/format';
import { useColors } from '@/core/theme';
import { data } from '@/data';
import { REQUEST_STATUS_LABELS, type ServiceRequestItem } from '@/data/types';
import { useRespondServiceRequest, useServiceRequests } from '@/features/queries';

type Role = 'Client' | 'Provider';

const TONES: Record<ServiceRequestItem['status'], 'primary' | 'warning' | 'neutral'> = {
  Sent: 'warning', Viewed: 'warning', Accepted: 'primary', Declined: 'neutral', Completed: 'primary', Cancelled: 'neutral',
};

/** Demandes envoyées (client) et reçues (prestataire) : accepter, refuser, terminer, laisser un avis. */
export default function ServiceRequests() {
  const c = useColors();
  const params = useLocalSearchParams<{ role?: Role }>();
  const [role, setRole] = useState<Role>(params.role === 'Provider' ? 'Provider' : 'Client');
  const query = useServiceRequests(role);
  return (
    <Screen>
      <Header title="Mes demandes" />
      <View style={{ padding: 16, paddingBottom: 4 }}>
        <Segmented value={role} onChange={setRole} items={[{ key: 'Client', label: 'Envoyées' }, { key: 'Provider', label: 'Reçues' }]} />
      </View>
      <QueryView query={query}>
        {(items) => (
          <FlatList data={items} keyExtractor={(r) => r.id} renderItem={({ item }) => <RequestCard r={item} />}
            ListEmptyComponent={role === 'Client'
              ? <EmptyState icon="clipboard-text-outline" title="Aucune demande envoyée" message="Trouvez un prestataire et demandez un devis."
                action="Trouver un prestataire" onAction={() => router.replace('/services')} />
              : <EmptyState icon="inbox-outline" title="Aucune demande reçue" message="Les demandes de vos clients arriveront ici." />}
            refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
            contentContainerStyle={{ paddingTop: 8, paddingBottom: 24 }} />
        )}
      </QueryView>
    </Screen>
  );
}

function askDeclineReason(): Promise<string | null | false> {
  if (Platform.OS === 'web') return Promise.resolve(typeof window !== 'undefined' && window.confirm('Refuser cette demande ?') ? null : false);
  return new Promise((resolve) => Alert.alert('Refuser cette demande', 'Le client sera prévenu.', [
    { text: 'Pas disponible à cette date', onPress: () => resolve('Pas disponible à cette date') },
    { text: 'Hors de ma zone', onPress: () => resolve('Hors de ma zone d’intervention') },
    { text: 'Refuser sans motif', style: 'destructive', onPress: () => resolve(null) },
    { text: 'Annuler', style: 'cancel', onPress: () => resolve(false) },
  ]));
}

function RequestCard({ r }: { r: ServiceRequestItem }) {
  const c = useColors();
  const respond = useRespondServiceRequest();
  const other = r.isProvider ? r.clientName : r.providerName;
  const open = r.status === 'Sent' || r.status === 'Viewed';
  const act = (action: 'accept' | 'complete' | 'cancel') => respond.mutate({ id: r.id, action });

  const openConversation = async () => {
    if (r.isProvider && r.status === 'Sent') await data().getServiceRequest(r.id).catch(() => undefined); // passe en « vue »
    if (r.conversationId) router.push(`/conversation/${r.conversationId}`);
  };

  return (
    <Card style={styles.card}>
      <View style={styles.row}>
        <Avatar initials={initialsOf(other)} color={c.primary} image={r.isProvider ? null : r.providerPhoto} size={44} />
        <View style={{ flex: 1 }}>
          <T v="label" style={{ fontSize: 15 }} lines={1}>{r.offeringTitle}</T>
          <Muted lines={1}>{r.isProvider ? `De ${other}` : other} · {fmt.relative(r.createdAt)}</Muted>
        </View>
        <Tag label={REQUEST_STATUS_LABELS[r.status]} tone={TONES[r.status]} />
      </View>
      <T style={{ marginTop: 8 }} lines={3}>{r.message}</T>
      {(r.preferredDate || r.city) && (
        <Muted style={{ marginTop: 4 }}>{[r.preferredDate && `Le ${fmt.longDate(r.preferredDate)}`, r.city].filter(Boolean).join(' · ')}</Muted>
      )}
      {!!r.declineReason && <Muted style={{ marginTop: 4 }}>Motif : {r.declineReason}</Muted>}
      {!!r.providerPhone && !r.isProvider && (
        <Button title={`Appeler ${r.providerPhone}`} kind="secondary" small icon="phone" style={{ marginTop: 8 }}
          onPress={() => void Linking.openURL(`tel:${r.providerPhone!.replace(/[^+\d]/g, '')}`)} />
      )}
      <View style={styles.actions}>
        {r.conversationId && <Button title="Conversation" kind="secondary" small icon="message-outline" onPress={openConversation} />}
        {r.isProvider && open && <>
          <Button title="Accepter" small icon="check" loading={respond.isPending} onPress={() => act('accept')} />
          <Button title="Refuser" kind="secondary" small onPress={async () => {
            const reason = await askDeclineReason();
            if (reason !== false) respond.mutate({ id: r.id, action: 'decline', reason: reason ?? undefined });
          }} />
        </>}
        {r.status === 'Accepted' && <Button title="Prestation terminée" small icon="check-all" onPress={async () => {
          if (await confirm('Prestation terminée ?', r.isProvider ? 'Le client pourra laisser un avis.' : 'Vous pourrez ensuite laisser un avis.', 'Confirmer')) act('complete');
        }} />}
        {!r.isProvider && (open || r.status === 'Accepted') && <Button title="Annuler" kind="secondary" small onPress={async () => {
          if (await confirm('Annuler la demande ?', 'Le prestataire sera prévenu.', 'Annuler la demande', true)) act('cancel');
        }} />}
        {r.canReview && <Button title="Laisser un avis" small icon="star" onPress={() =>
          router.push({ pathname: '/services/review', params: { requestId: r.id, provider: r.providerName } })} />}
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  card: { marginHorizontal: 16, marginBottom: 12, padding: 14 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 10 },
});
