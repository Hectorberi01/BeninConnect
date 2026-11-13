import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, RefreshControl, View } from 'react-native';
import { CommunityCard } from '@/components/cards';
import { ChipRow, EmptyState, Header, IconButton, QueryView, Screen, SearchBar, SectionTitle } from '@/components/ui';
import { useTabBarSpace } from '@/components/glass';
import { useMe } from '@/core/session';
import { useColors } from '@/core/theme';
import { matches } from '@/core/validation';
import type { Community } from '@/data/types';
import { location } from '@/data/types';
import { useCommunities, useCommunityCategories } from '@/features/queries';

type Row = { kind: 'title'; title: string } | { kind: 'item'; community: Community };

export default function Communities() {
  const c = useColors();
  const me = useMe();
  const query = useCommunities();
  const categories = useCommunityCategories().data ?? [];
  const [search, setSearch] = useState('');
  const bottom = useTabBarSpace();
  const [category, setCategory] = useState('*');

  const rows = useMemo<Row[]>(() => {
    const q = search.trim();
    const filtered = (query.data ?? []).filter((x) =>
      (category === '*' || x.category === category)
      && (!q || matches(x.name, q) || matches(x.description, q) || matches(location(x), q)));
    const mine = filtered.filter((x) => x.memberIds.includes(me.id));
    const others = filtered.filter((x) => !x.memberIds.includes(me.id));
    return [
      ...(mine.length ? [{ kind: 'title', title: 'Mes communautés' } as Row, ...mine.map((x) => ({ kind: 'item', community: x }) as Row)] : []),
      ...(others.length ? [{ kind: 'title', title: q || category !== '*' ? 'Résultats' : 'Communautés recommandées' } as Row,
        ...others.map((x) => ({ kind: 'item', community: x }) as Row)] : []),
    ];
  }, [query.data, search, category, me.id]);

  return (
    <Screen>
      <Header title="Communautés" back={false}
        right={<IconButton name="plus-circle-outline" label="Créer une communauté" color={c.primary} onPress={() => router.push('/community/new')} />} />
      <View style={{ padding: 16, paddingBottom: 8 }}>
        <SearchBar value={search} onChangeText={setSearch} placeholder="Rechercher une communauté, une ville…" />
      </View>
      <ChipRow items={[{ key: '*', label: 'Toutes' }, ...categories.map((x) => ({ key: x, label: x }))]} selected={category} onSelect={setCategory} />
      <QueryView query={query} loadingLabel="Chargement des communautés…">
        {() => (
          <FlatList
            data={rows}
            keyExtractor={(r, i) => (r.kind === 'item' ? r.community.id : `t${i}`)}
            renderItem={({ item }) => (item.kind === 'title' ? <SectionTitle title={item.title} /> : <CommunityCard community={item.community} />)}
            ListEmptyComponent={<EmptyState icon="account-search-outline" title="Aucune communauté trouvée"
              message="Essayez un autre mot-clé ou une autre catégorie." action="Effacer les filtres"
              onAction={() => { setSearch(''); setCategory('*'); }} />}
            refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
            contentContainerStyle={{ paddingBottom: bottom }}
            keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag"
          />
        )}
      </QueryView>
    </Screen>
  );
}
