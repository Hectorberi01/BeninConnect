import { router } from 'expo-router';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Stars } from '@/components/services';
import { Card, Muted, SectionTitle, T } from '@/components/ui';
import { TripTile, TrustedTag } from '@/components/voyages';
import * as fmt from '@/core/format';
import { useVoyageStats } from '@/features/queries';

/** Bloc « Voyages » du profil : livraisons réussies, note, badge et trajets à venir. Masqué s'il n'y a rien à montrer. */
export function VoyageBlock({ userId, isMe }: { userId: string; isMe: boolean }) {
  const stats = useVoyageStats(userId).data;
  if (!stats) return null;
  const empty = stats.deliveriesAsTraveler === 0 && stats.ratingCount === 0 && stats.upcomingTrips.length === 0 && stats.shipmentsDelivered === 0;
  if (empty) return null;
  return (
    <>
      <SectionTitle title="Voyages" action={isMe ? 'Gérer' : undefined} onAction={() => router.push('/voyages/mine')} />
      <Card style={styles.card}>
        <View style={styles.row}>
          <View style={{ flex: 1 }}>
            <T v="label">{fmt.plural(stats.deliveriesAsTraveler, 'livraison réussie', 'livraisons réussies')}</T>
            {stats.ratingCount > 0
              ? <View style={styles.row}><Stars value={stats.ratingAverage} size={13} /><Muted>{`${stats.ratingAverage} · ${fmt.plural(stats.ratingCount, 'avis', 'avis')}`}</Muted></View>
              : <Muted>Pas encore d’avis comme voyageur</Muted>}
            {stats.shipmentsDelivered > 0 && <Muted>{fmt.plural(stats.shipmentsDelivered, 'envoi reçu', 'envois reçus')}</Muted>}
          </View>
        </View>
        {stats.trustedTraveler && <View style={{ marginTop: 8 }}><TrustedTag /></View>}
      </Card>
      {stats.upcomingTrips.length > 0 && (
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ paddingHorizontal: 16, paddingTop: 8 }}>
          {stats.upcomingTrips.map((t) => <TripTile key={t.id} t={t} />)}
        </ScrollView>
      )}
    </>
  );
}

const styles = StyleSheet.create({
  card: { marginHorizontal: 16, padding: 14 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 6 },
});
