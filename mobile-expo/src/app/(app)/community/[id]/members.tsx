import { useLocalSearchParams } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, View } from 'react-native';
import { UserRow } from '@/components/cards';
import { EmptyState, Header, Muted, QueryView, Screen, SearchBar } from '@/components/ui';
import { matches } from '@/core/validation';
import { displayName, location } from '@/data/types';
import { useCommunity, useMembers } from '@/features/queries';
import { roleLabel } from '@/features/rules';

export default function CommunityMembers() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const community = useCommunity(id).data;
  const query = useMembers(id);
  const [search, setSearch] = useState('');
  const subtitle = (u: { profession: string; city: string; country: string }) =>
    u.profession ? `${u.profession} · ${location(u)}` : location(u);
  const items = useMemo(() => {
    const q = search.trim();
    return (query.data ?? []).filter((m) => !q || matches(displayName(m.user), q) || matches(subtitle(m.user), q));
  }, [query.data, search]);

  return (
    <Screen>
      <Header title={community ? `Membres · ${community.name}` : 'Membres'} />
      <View style={{ padding: 16, paddingBottom: 8, gap: 8 }}>
        <SearchBar value={search} onChangeText={setSearch} placeholder="Rechercher un membre" />
        {query.data && <Muted>{`${query.data.length} membre${query.data.length > 1 ? 's' : ''}`}</Muted>}
      </View>
      <QueryView query={query}>
        {() => (
          <FlatList data={items} keyExtractor={(m) => m.user.id} keyboardShouldPersistTaps="handled"
            renderItem={({ item }) => <UserRow user={item.user} subtitle={subtitle(item.user)} badge={roleLabel(item.role)} />}
            ListEmptyComponent={<EmptyState icon="account-search-outline" title="Aucun membre trouvé" />} />
        )}
      </QueryView>
    </Screen>
  );
}
