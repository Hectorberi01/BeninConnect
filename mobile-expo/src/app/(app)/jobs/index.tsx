import { router } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, View } from 'react-native';
import { JobCard } from '@/components/jobs';
import { Chip, EmptyState, ErrorState, Header, IconButton, Loading, Muted, Screen, SearchBar, Select } from '@/components/ui';
import { useColors } from '@/core/theme';
import { CONTRACT_LABELS, countryName, errorMessage, type JobContract, type JobFilter } from '@/data/types';
import { useJobFilters, useJobs } from '@/features/queries';

function useDebounced<T>(value: T, ms = 350) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

export default function Jobs() {
  const c = useColors();
  const filters = useJobFilters().data;
  const [search, setSearch] = useState('');
  const [country, setCountry] = useState<string | null>(null);
  const [contract, setContract] = useState<JobContract | null>(null);
  const [remote, setRemote] = useState(false);
  const q = useDebounced(search.trim());
  const filter: JobFilter = { q: q || undefined, country: country ?? undefined, contract: contract ?? undefined, remote: remote || undefined };
  const query = useJobs(filter);
  const jobs = useMemo(() => {
    const seen = new Set<string>();
    return (query.data?.pages ?? []).flatMap((p) => p.items).filter((j) => (seen.has(j.id) ? false : (seen.add(j.id), true)));
  }, [query.data]);

  const clear = () => {
    setSearch('');
    setCountry(null);
    setContract(null);
    setRemote(false);
  };

  const countries = [{ value: null as string | null, label: 'Tous les pays' },
    ...(filters?.countries ?? []).map((x) => ({ value: x.label as string | null, label: `${countryName(x.label)} (${x.count})` }))];
  const contracts = [{ value: null as JobContract | null, label: 'Tous les contrats' },
    ...(filters?.contracts ?? []).map((x) => ({ value: x.label as JobContract | null, label: `${CONTRACT_LABELS[x.label as JobContract] ?? x.label} (${x.count})` }))];

  return (
    <Screen>
      <Header title="Emploi" subtitle="Offres de France Travail, ReliefWeb…"
        right={<IconButton name="bookmark-multiple-outline" label="Mes offres favorites" color={c.primary} onPress={() => router.push('/jobs/favorites')} />} />
      <View style={{ padding: 16, paddingBottom: 8 }}>
        <SearchBar value={search} onChangeText={setSearch} placeholder="Métier, entreprise…" />
      </View>
      <View style={{ flexDirection: 'row', gap: 12, paddingHorizontal: 16 }}>
        <View style={{ flex: 1 }}><Select label="Pays" value={country} onChange={setCountry} options={countries} /></View>
        <View style={{ flex: 1 }}><Select label="Contrat" value={contract} onChange={setContract} options={contracts} /></View>
      </View>
      <View style={{ flexDirection: 'row', paddingHorizontal: 16, marginBottom: 8 }}>
        <Chip label="Télétravail" icon="home-city-outline" selected={remote} onPress={() => setRemote((r) => !r)} />
      </View>

      {query.isPending ? <Loading label="Chargement des offres…" />
        : query.error ? <ErrorState message={errorMessage(query.error)} onRetry={() => query.refetch()} />
          : (
            <FlatList
              data={jobs}
              keyExtractor={(j) => j.id}
              renderItem={({ item }) => <JobCard job={item} />}
              ListEmptyComponent={<EmptyState icon="briefcase-search-outline" title="Aucune offre trouvée"
                message="Modifiez votre recherche ou vos filtres." action="Effacer les filtres" onAction={clear} />}
              ListFooterComponent={jobs.length === 0 ? undefined : (
                <View style={{ padding: 16, alignItems: 'center', gap: 6 }}>
                  {query.isFetchingNextPage ? <ActivityIndicator color={c.primary} />
                    : !query.hasNextPage && <Muted style={{ textAlign: 'center' }}>La candidature se fait sur le site de la source de chaque offre.</Muted>}
                </View>
              )}
              onEndReached={() => query.hasNextPage && !query.isFetchingNextPage && query.fetchNextPage()}
              onEndReachedThreshold={0.5}
              refreshControl={<RefreshControl refreshing={query.isRefetching && !query.isFetchingNextPage} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
              contentContainerStyle={{ paddingBottom: 24 }}
              keyboardShouldPersistTaps="handled" />
          )}
    </Screen>
  );
}
