import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { FlatList, RefreshControl, View } from 'react-native';
import { EventCard } from '@/components/cards';
import { ChipRow, EmptyState, Header, IconButton, Muted, QueryView, Screen, SearchBar, Select } from '@/components/ui';
import { useColors } from '@/core/theme';
import type { EventPeriod } from '@/data/types';
import { useEventCategories, useEventCities, useEvents } from '@/features/queries';

const PERIODS: { value: EventPeriod; label: string }[] = [
  { value: 'All', label: 'Toutes les dates' },
  { value: 'ThisWeek', label: 'Cette semaine' },
  { value: 'ThisMonth', label: 'Ce mois-ci' },
  { value: 'Later', label: 'Plus tard' },
];

function useDebounced<T>(value: T, ms = 300) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

export default function Events() {
  const c = useColors();
  const categories = useEventCategories().data ?? [];
  const cities = useEventCities().data ?? [];
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('*');
  const [city, setCity] = useState<string | null>(null);
  const [period, setPeriod] = useState<EventPeriod>('All');
  const q = useDebounced(search.trim());
  const query = useEvents({ search: q || undefined, category: category === '*' ? undefined : category, city: city ?? undefined, period });
  const count = query.data?.length ?? 0;

  const clear = () => {
    setSearch('');
    setCategory('*');
    setCity(null);
    setPeriod('All');
  };

  return (
    <Screen>
      <Header title="Événements"
        right={<IconButton name="calendar-plus" label="Créer un événement" color={c.primary} onPress={() => router.push('/events/new')} />} />
      <View style={{ padding: 16, paddingBottom: 8 }}>
        <SearchBar value={search} onChangeText={setSearch} placeholder="Rechercher un événement" />
      </View>
      <ChipRow items={[{ key: '*', label: 'Tous' }, ...categories.map((x) => ({ key: x, label: x }))]} selected={category} onSelect={setCategory} />
      <View style={{ flexDirection: 'row', gap: 12, paddingHorizontal: 16, paddingTop: 12 }}>
        <View style={{ flex: 1 }}>
          <Select label="Ville" value={city} onChange={setCity}
            options={[{ value: null, label: 'Toutes les villes' }, ...cities.map((x) => ({ value: x as string | null, label: x }))]} />
        </View>
        <View style={{ flex: 1 }}>
          <Select label="Période" value={period} onChange={setPeriod} options={PERIODS} />
        </View>
      </View>
      {query.data && (
        <Muted style={{ paddingHorizontal: 16, marginBottom: 8 }}>
          {count === 0 ? 'Aucun événement' : count === 1 ? '1 événement à venir' : `${count} événements à venir`}
        </Muted>
      )}
      <QueryView query={query} loadingLabel="Chargement des événements…">
        {(events) => (
          <FlatList data={events} keyExtractor={(e) => e.id} renderItem={({ item }) => <EventCard event={item} />}
            ListEmptyComponent={<EmptyState icon="calendar-search" title="Aucun événement trouvé" message="Modifiez vos filtres pour élargir la recherche."
              action="Effacer les filtres" onAction={clear} />}
            refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
            contentContainerStyle={{ paddingBottom: 24 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag" />
        )}
      </QueryView>
    </Screen>
  );
}
