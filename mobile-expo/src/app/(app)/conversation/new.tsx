import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, View } from 'react-native';
import { UserRow } from '@/components/cards';
import { EmptyState, Header, QueryView, Screen, SearchBar } from '@/components/ui';
import { useMe } from '@/core/session';
import { toast } from '@/core/toast';
import { matches } from '@/core/validation';
import { data } from '@/data';
import type { User } from '@/data/types';
import { displayName, errorMessage } from '@/data/types';
import { useSearchUsers } from '@/features/queries';

export default function NewConversation() {
  const me = useMe();
  const query = useSearchUsers('');
  const [search, setSearch] = useState('');
  const subtitle = (u: User) => [u.profession, u.city].filter(Boolean).join(' · ');
  const people = useMemo(() => {
    const q = search.trim();
    return (query.data ?? []).filter((u) => u.id !== me.id && (!q || matches(displayName(u), q) || matches(subtitle(u), q)));
  }, [query.data, search, me.id]);

  const start = async (user: User) => {
    try {
      const conversation = await data().startConversation(user.id);
      router.replace(`/conversation/${conversation.id}`);
    } catch (e) {
      toast(errorMessage(e), 'error');
    }
  };

  return (
    <Screen>
      <Header title="Nouvelle conversation" />
      <View style={{ padding: 16, paddingBottom: 8 }}>
        <SearchBar value={search} onChangeText={setSearch} placeholder="Rechercher un membre" autoFocus />
      </View>
      <QueryView query={query}>
        {() => (
          <FlatList data={people} keyExtractor={(u) => u.id} keyboardShouldPersistTaps="handled"
            renderItem={({ item }) => <UserRow user={item} subtitle={subtitle(item)} onPress={() => start(item)} />}
            ListEmptyComponent={<EmptyState icon="account-search-outline" title="Aucun membre trouvé" />} />
        )}
      </QueryView>
    </Screen>
  );
}
