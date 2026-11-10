import { useEffect, useState } from 'react';
import { ScrollView, View } from 'react-native';
import { CommunityCard, EventCard, PostCard, usePostContext, UserRow } from '@/components/cards';
import { Chip, ChipRow, EmptyState, ErrorState, Header, Loading, Muted, Screen, SearchBar, SectionTitle } from '@/components/ui';
import { useQuery } from '@tanstack/react-query';
import { data } from '@/data';
import { errorMessage } from '@/data/types';
import { SEARCH_SUGGESTIONS } from '@/features/rules';

type Scope = 'All' | 'People' | 'Posts' | 'Communities' | 'Events';
const SCOPES: { key: Scope; label: string }[] = [
  { key: 'All', label: 'Tout' }, { key: 'People', label: 'Membres' }, { key: 'Posts', label: 'Publications' },
  { key: 'Communities', label: 'Communautés' }, { key: 'Events', label: 'Événements' },
];

async function searchAll(q: string, scope: Scope) {
  const want = (s: Scope) => scope === 'All' || scope === s;
  const [people, posts, communities, events] = await Promise.all([
    want('People') ? data().searchUsers(q) : [],
    // « #cotonou » : publications portant ce hashtag
    want('Posts') ? data().getPosts(q.startsWith('#') ? { tag: q.slice(1) } : { search: q }) : [],
    want('Communities') ? data().getCommunities(q) : [],
    want('Events') ? data().getEvents({ search: q }) : [],
  ]);
  return { people, posts, communities, events, total: people.length + posts.length + communities.length + events.length };
}

export default function Search() {
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [scope, setScope] = useState<Scope>('All');

  useEffect(() => {
    const t = setTimeout(() => setQ(text.trim()), 300);
    return () => clearTimeout(t);
  }, [text]);

  const prompt = q.length < 2;
  const query = useQuery({ queryKey: ['search', q, scope], queryFn: () => searchAll(q, scope), enabled: !prompt });
  const ctx = usePostContext(query.data?.posts ?? []);
  const r = query.data;

  return (
    <Screen>
      <Header title="Recherche" />
      <View style={{ padding: 16, paddingBottom: 8 }}>
        <SearchBar value={text} onChangeText={setText} placeholder="Membres, publications, communautés…" autoFocus />
      </View>
      <ChipRow items={SCOPES} selected={scope} onSelect={(k) => setScope(k as Scope)} />
      {prompt ? (
        <View style={{ padding: 16 }}>
          <Muted style={{ marginBottom: 10, fontSize: 15 }}>Tapez au moins 2 caractères. Suggestions :</Muted>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {SEARCH_SUGGESTIONS.map((s) => <Chip key={s} label={s} icon="magnify" onPress={() => setText(s)} />)}
          </View>
        </View>
      ) : query.isPending ? <Loading label="Recherche…" />
        : query.error || !r ? <ErrorState message={errorMessage(query.error)} onRetry={() => query.refetch()} />
          : r.total === 0 ? <EmptyState icon="magnify-close" title="Aucun résultat" message={`Rien ne correspond à « ${q} ».`} />
            : (
              <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={{ paddingBottom: 32 }}>
                <Muted style={{ paddingHorizontal: 16, marginTop: 8 }}>{r.total === 1 ? '1 résultat' : `${r.total} résultats`}</Muted>
                {r.people.length > 0 && <SectionTitle title="Membres" />}
                {r.people.map((u) => <UserRow key={u.id} user={u} />)}
                {r.communities.length > 0 && <SectionTitle title="Communautés" />}
                {r.communities.map((x) => <CommunityCard key={x.id} community={x} />)}
                {r.events.length > 0 && <SectionTitle title="Événements" />}
                {r.events.map((e) => <EventCard key={e.id} event={e} />)}
                {r.posts.length > 0 && <SectionTitle title="Publications" />}
                {r.posts.map((p) => <PostCard key={p.id} post={p} author={ctx.users.get(p.authorId)} community={p.communityId ? ctx.communities.get(p.communityId) : undefined} />)}
              </ScrollView>
            )}
    </Screen>
  );
}
