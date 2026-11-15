import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { Notice } from '@/components/services';
import { Button, Chip, EmptyState, ErrorState, Header, IconButton, Loading, Muted, Screen, TextField } from '@/components/ui';
import { TripSheet } from '@/components/TripForm';
import { VoyagesGate } from '@/components/VoyagesGate';
import { formatDay, parseDay, PARCEL_CATEGORIES, PlaceFields, placeFilter, TripCardView } from '@/components/voyages';
import { useDebounced } from '@/core/useDebounced';
import { useColors } from '@/core/theme';
import { PARCEL_LABELS, type ParcelCategory, type TripSearch } from '@/data/types';
import { errorMessage } from '@/data/types';
import { useRouteAlertActions, useTripSearch, useVoyagesInfo } from '@/features/queries';
import { usePullRefresh } from '@/core/refresh';

/** « Rechercher un départ » : trajets à venir, filtrés par itinéraire, date et catégorie. */
export default function VoyagesScreen() {
  return <VoyagesGate><Voyages /></VoyagesGate>;
}

function Voyages() {
  const c = useColors();
  const info = useVoyagesInfo().data;
  const [fromCity, setFromCity] = useState('');
  const [fromCountry, setFromCountry] = useState('');
  const [toCity, setToCity] = useState('');
  const [toCountry, setToCountry] = useState('');
  const from = placeFilter(useDebounced(fromCity), useDebounced(fromCountry));
  const to = placeFilter(useDebounced(toCity), useDebounced(toCountry));
  const [before, setBefore] = useState('');
  const [category, setCategory] = useState<ParcelCategory | null>(null);
  const [publishing, setPublishing] = useState(false);
  const beforeDay = parseDay(before);
  const filter: TripSearch = useMemo(() => ({
    from: from?.country || null, fromCity: from?.city ?? null, to: to?.country || null, toCity: to?.city ?? null,
    before: beforeDay ?? null, category,
  }), [from, to, beforeDay, category]);
  const query = useTripSearch(filter);
  const pull = usePullRefresh(() => query.refetch());
  const alerts = useRouteAlertActions();
  const trips = query.data?.pages.flatMap((p) => p.items) ?? [];

  const createAlert = () => {
    if (!from?.country || !to?.country) return;
    alerts.create.mutate({ fromCountry: from.country, fromCity: from.city, toCountry: to.country, toCity: to.city, after: null, before: beforeDay ?? null });
  };

  const header = (
    <View style={{ padding: 16, paddingBottom: 4 }}>
      <View style={styles.actions}>
        <Button title="Proposer mes kilos" icon="airplane-takeoff" small onPress={() => setPublishing(true)} style={{ flex: 1 }} />
        <Button title="Mes voyages et envois" icon="bag-suitcase-outline" kind="secondary" small onPress={() => router.push('/voyages/mine')} style={{ flex: 1 }} />
      </View>
      <PlaceFields label="Départ" city={fromCity} country={fromCountry} onCity={setFromCity} onCountry={setFromCountry} />
      <PlaceFields label="Arrivée" city={toCity} country={toCountry} onCity={setToCity} onCountry={setToCountry} />
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
      <Header title="Rechercher un départ" subtitle="Des kilos entre membres"
        right={<IconButton name="bell-plus-outline" label="Mes alertes de trajet" color={c.primary} onPress={() => router.push('/voyages/alerts')} />} />
      {query.isPending ? <Loading /> : query.error ? <ErrorState message={errorMessage(query.error)} onRetry={() => query.refetch()} /> : (
        <FlatList data={trips} keyExtractor={(t) => t.id} renderItem={({ item }) => <TripCardView t={item} />}
          ListHeaderComponent={header}
          ListEmptyComponent={
            <View>
              <EmptyState icon="airplane-search" title="Aucun trajet pour l’instant"
                message={from?.country && to?.country ? 'Créez une alerte : vous serez prévenu dès qu’un membre publie ce trajet.' : 'Indiquez le pays de départ et d’arrivée pour créer une alerte.'}
                action={from?.country && to?.country ? 'Créer une alerte' : undefined} onAction={createAlert} />
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
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.onRefresh} tintColor={c.primary} />}
          keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag" />
      )}
      <TripSheet visible={publishing} onClose={() => setPublishing(false)} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  actions: { flexDirection: 'row', gap: 8, marginBottom: 12 },
});
