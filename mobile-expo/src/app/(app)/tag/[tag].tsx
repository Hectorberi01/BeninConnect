import { useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { FlatList, View } from 'react-native';
import { PostCard, usePostContext } from '@/components/cards';
import { PostGrid } from '@/components/PostGrid';
import { EmptyState, Header, QueryView, Screen, Segmented } from '@/components/ui';
import * as fmt from '@/core/format';
import { normalizeTag } from '@/data/social';
import { usePosts } from '@/features/queries';

/** Publications d'un hashtag (#cotonou), en grille ou en fil. */
export default function TagPosts() {
  const { tag: raw } = useLocalSearchParams<{ tag: string }>();
  const tag = normalizeTag(raw ?? '');
  const query = usePosts({ tag }, !!tag);
  const [view, setView] = useState<'grid' | 'list'>('grid');
  const ctx = usePostContext(query.data ?? []);
  return (
    <Screen>
      <Header title={`#${tag}`} subtitle={query.data ? fmt.plural(query.data.length, 'publication') : undefined} />
      <QueryView query={query}>
        {(posts) => posts.length === 0
          ? <EmptyState icon="pound" title="Aucune publication" message={`Personne n'a encore utilisé #${tag}.`} />
          : (
            <FlatList
              data={view === 'grid' ? [] : posts}
              keyExtractor={(p) => p.id}
              ListHeaderComponent={
                <View style={{ paddingTop: 8 }}>
                  <View style={{ paddingHorizontal: 16, marginBottom: 12 }}>
                    <Segmented value={view} onChange={setView} items={[{ key: 'grid', label: 'Grille' }, { key: 'list', label: 'Fil' }]} />
                  </View>
                  {view === 'grid' && <PostGrid posts={posts} />}
                </View>
              }
              renderItem={({ item }) => (
                <PostCard post={item} author={ctx.users.get(item.authorId)} community={item.communityId ? ctx.communities.get(item.communityId) : undefined} />
              )}
              contentContainerStyle={{ paddingBottom: 32 }}
            />
          )}
      </QueryView>
    </Screen>
  );
}
