import { useLocalSearchParams } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, ScrollView, View } from 'react-native';
import { CityPicker, ProviderCardView } from '@/components/services';
import { Chip, EmptyState, ErrorState, Header, Loading, Muted, Screen, SearchBar, Segmented, Select } from '@/components/ui';
import { useColors } from '@/core/theme';
import { findCity, type City } from '@/data/cities';
import { errorMessage, type ServiceSort } from '@/data/types';
import { useServiceCategories, useServiceSearch } from '@/features/queries';

const RADII = [5, 10, 25, 50];
const PRICES = [null, 30, 50, 100, 200] as const;

function useDebounced<T>(value: T, ms = 350) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

/** Recherche de prestataires : catégorie, ville et rayon, à domicile, note, prix, vérifiés, tri. */
export default function ServiceSearch() {
  const c = useColors();
  const params = useLocalSearchParams<{ categoryId?: string; q?: string; city?: string; country?: string }>();
  const tree = useServiceCategories().data ?? [];
  const [search, setSearch] = useState(params.q ?? '');
  const [categoryId, setCategoryId] = useState<string | null>(params.categoryId ?? null);
  const [city, setCity] = useState<City | null>(() => findCity(params.city, params.country) ?? null);
  const [radius, setRadius] = useState(25);
  const [atHome, setAtHome] = useState(false);
  const [verified, setVerified] = useState(false);
  const [minRating, setMinRating] = useState(false);
  const [priceMax, setPriceMax] = useState<string | null>(null);
  const [sort, setSort] = useState<ServiceSort>('Relevance');
  const q = useDebounced(search.trim());

  const query = useServiceSearch({
    categoryId: categoryId ?? undefined, q: q || undefined, lat: city?.lat, lng: city?.lng, radiusKm: city ? radius : undefined,
    atHome: atHome || undefined, verifiedOnly: verified || undefined, minRating: minRating ? 4 : undefined,
    priceMax: priceMax ? Number(priceMax) : undefined, sort: sort === 'Distance' && !city ? 'Relevance' : sort,
  });
  const providers = useMemo(() => (query.data?.pages ?? []).flatMap((p) => p.items), [query.data]);
  const total = query.data?.pages[0]?.total ?? 0;

  const categoryOptions = [{ value: null as string | null, label: 'Toutes les catégories' },
    ...tree.flatMap((p) => [{ value: p.id as string | null, label: p.name }, ...p.children.map((x) => ({ value: x.id as string | null, label: `   ${x.name}` }))])];

  return (
    <Screen>
      <Header title="Prestataires" />
      <FlatList
        data={providers}
        keyExtractor={(p) => p.id}
        renderItem={({ item }) => <ProviderCardView p={item} />}
        ListHeaderComponent={
          <View style={{ paddingTop: 12 }}>
            <View style={{ paddingHorizontal: 16 }}>
              <SearchBar value={search} onChangeText={setSearch} placeholder="Service, prestataire…" />
              <View style={{ flexDirection: 'row', gap: 12, marginTop: 12 }}>
                <View style={{ flex: 1 }}><Select label="Catégorie" value={categoryId} onChange={setCategoryId} options={categoryOptions} /></View>
                <View style={{ flex: 1 }}><CityPicker value={city} onChange={setCity} label="Près de" allowAll="Partout" /></View>
              </View>
            </View>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8, paddingHorizontal: 16, paddingBottom: 8 }}>
              {city && RADII.map((r) => <Chip key={r} label={`${r} km`} selected={radius === r} onPress={() => setRadius(r)} />)}
              <Chip label="À domicile" icon="home-outline" selected={atHome} onPress={() => setAtHome((v) => !v)} />
              <Chip label="Pro vérifiés" icon="check-decagram-outline" selected={verified} onPress={() => setVerified((v) => !v)} />
              <Chip label="4★ et plus" selected={minRating} onPress={() => setMinRating((v) => !v)} />
            </ScrollView>
            <View style={{ paddingHorizontal: 16 }}>
              <Select label="Prix maximum" value={priceMax} onChange={setPriceMax}
                options={PRICES.map((p) => ({ value: p === null ? null : String(p), label: p === null ? 'Tous les prix' : `${p} € ou moins` }))} />
              <Segmented value={sort} onChange={setSort} items={[
                { key: 'Relevance', label: 'Pertinence' }, ...(city ? [{ key: 'Distance' as const, label: 'Distance' }] : []),
                { key: 'Rating', label: 'Note' }, { key: 'Recent', label: 'Récents' },
              ]} />
              {query.data && <Muted style={{ marginVertical: 8 }}>{total === 0 ? 'Aucun prestataire' : total === 1 ? '1 prestataire' : `${total} prestataires`}</Muted>}
            </View>
          </View>
        }
        ListEmptyComponent={query.isPending ? <Loading label="Recherche…" /> : query.error ? <ErrorState message={errorMessage(query.error)} onRetry={() => query.refetch()} />
          : <EmptyState icon="account-search-outline" title="Aucun prestataire trouvé" message="Élargissez la zone ou retirez des filtres." />}
        ListFooterComponent={query.isFetchingNextPage ? <ActivityIndicator color={c.primary} style={{ padding: 16 }} /> : undefined}
        onEndReached={() => query.hasNextPage && !query.isFetchingNextPage && query.fetchNextPage()}
        onEndReachedThreshold={0.5}
        refreshControl={<RefreshControl refreshing={query.isRefetching && !query.isFetchingNextPage} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
        contentContainerStyle={{ paddingBottom: 24 }}
        keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag" />
    </Screen>
  );
}
