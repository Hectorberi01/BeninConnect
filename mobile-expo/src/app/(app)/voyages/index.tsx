import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { Notice } from '@/components/services';
import { Button, Chip, EmptyState, ErrorState, Header, IconButton, Loading, Muted, Screen, TextField } from '@/components/ui';
import { formatDay, parseDay, PARCEL_CATEGORIES, PlacePicker, type Place, TripCardView } from '@/components/voyages';
import { useColors } from '@/core/theme';
import { PARCEL_LABELS, type ParcelCategory, type TripSearch } from '@/data/types';
import { errorMessage } from '@/data/types';
import { useRouteAlertActions, useTripSearch, useVoyagesInfo } from '@/features/queries';

/** « Qui part au pays ? » : trajets à venir, filtrés par itinéraire, date et catégorie. */
export default function Voyages() {
  const c = useColors();
  const info = useVoyagesInfo().data;
  const [from, setFrom] = useState<Place | null>(null);
  const [to, setTo] = useState<Place | null>(null);
  const [before, setBefore] = useState('');
  const [category, setCategory] = useState<ParcelCategory | null>(null);
  const beforeDay = parseDay(before);
  const filter: TripSearch = useMemo(() => ({
    from: from?.country ?? null, fromCity: from?.city ?? null, to: to?.country ?? null, toCity: to?.city ?? null,
    before: beforeDay ?? null, category,
  }), [from, to, beforeDay, category]);
  const query = useTripSearch(filter);
  const alerts = useRouteAlertActions();
  const trips = query.data?.pages.flatMap((p) => p.items) ?? [];

  const createAlert = () => {
    if (!from || !to) return;
    alerts.create.mutate({ fromCountry: from.country, fromCity: from.city, toCountry: to.country, toCity: to.city, after: null, before: beforeDay ?? null });
  };

  const header = (
    <View style={{ padding: 16, paddingBottom: 4 }}>
      <View style={styles.actions}>
        <Button title="Proposer mes kilos" icon="airplane-takeoff" small onPress={() => router.push('/voyages/publish')} style={{ flex: 1 }} />
        <Button title="Mes voyages et envois" icon="bag-suitcase-outline" kind="secondary" small onPress={() => router.push('/voyages/mine')} style={{ flex: 1 }} />
      </View>
      <PlacePicker label="Départ" value={from} onChange={setFrom} allowAll="Partout" />
      <PlacePicker label="Arrivée" value={to} onChange={setTo} allowAll="Partout" />
      <TextField label="Départ avant le (facultatif)" value={before} onChangeText={setBefore} placeholder="JJ/MM/AAAA" keyboardType="numbers-and-punctuation"
        error={beforeDay === undefined ? 'Date invalide : JJ/MM/AAAA.' : null} />
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8, paddingVertical: 4 }}>
        <Chip label="Tout type de colis" selected={!category} onPress={() => setCategory(null)} />
        {PARCEL_CATEGORIES.map((cat) => <Chip key={cat} label={PARCEL_LABELS[cat]} selected={category === cat} onPress={() => setCategory(cat)} />)}
      </ScrollView>
      {query.data && <Muted style={{ marginTop: 8 }}>{`${query.data.pages[0]?.total ?? 0} trajet(s) à venir`}</Muted>}
    </View>
  );

  return (
    <Screen>
      <Header title="Qui part au pays ?" subtitle="Des kilos entre membres"
        right={<IconButton name="bell-plus-outline" label="Mes alertes de trajet" color={c.primary} onPress={() => router.push('/voyages/alerts')} />} />
      {query.isPending ? <Loading /> : query.error ? <ErrorState message={errorMessage(query.error)} onRetry={() => query.refetch()} /> : (
        <FlatList data={trips} keyExtractor={(t) => t.id} renderItem={({ item }) => <TripCardView t={item} />}
          ListHeaderComponent={header}
          ListEmptyComponent={
            <View>
              <EmptyState icon="airplane-search" title="Aucun trajet pour l’instant"
                message={from && to ? 'Créez une alerte : vous serez prévenu dès qu’un membre publie ce trajet.' : 'Choisissez un départ et une arrivée pour créer une alerte.'}
                action={from && to ? 'Créer une alerte' : undefined} onAction={createAlert} />
            </View>
          }
          ListFooterComponent={
            <View style={{ paddingHorizontal: 16, paddingBottom: 32 }}>
              {query.isFetchingNextPage && <ActivityIndicator color={c.primary} />}
              {info && <Notice icon="handshake-outline">{info.intermediation}</Notice>}
              {!!before && beforeDay && <Muted style={{ marginTop: 8 }}>{`Départs jusqu’au ${formatDay(beforeDay)}.`}</Muted>}
            </View>
          }
          onEndReached={() => { if (query.hasNextPage && !query.isFetchingNextPage) query.fetchNextPage(); }}
          refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
          keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag" />
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  actions: { flexDirection: 'row', gap: 8, marginBottom: 12 },
});
