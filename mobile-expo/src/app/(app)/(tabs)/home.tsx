import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CommunityTile, EventTile, PostCard, usePostContext } from '@/components/cards';
import { JobTile } from '@/components/jobs';
import { Avatar, EmptyState, ErrorState, Icon, IconButton, Loading, Muted, Screen, SectionTitle, T } from '@/components/ui';
import { useTabBarSpace } from '@/components/glass';
import { useMe } from '@/core/session';
import { depth, useColors } from '@/core/theme';
import { initials } from '@/data/types';
import { useCommunities, useEvents, useFeed, useJobs, useUnreadNotifications } from '@/features/queries';
import { errorMessage } from '@/data/types';

export default function Home() {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const feed = useFeed();
  const communities = useCommunities();
  const events = useEvents({});
  const jobs = useJobs({});
  const unread = useUnreadNotifications().data ?? 0;
  const [refreshing, setRefreshing] = useState(false);
  const bottom = useTabBarSpace();

  const posts = useMemo(() => {
    const seen = new Set<string>();
    return (feed.data?.pages ?? []).flatMap((p) => p.items).filter((p) => (seen.has(p.id) ? false : (seen.add(p.id), true)));
  }, [feed.data]);
  const ctx = usePostContext(posts);
  const suggestions = (communities.data ?? []).filter((x) => !x.memberIds.includes(me.id)).slice(0, 8);
  const upcoming = (events.data ?? []).slice(0, 5);
  const latestJobs = (jobs.data?.pages[0]?.items ?? []).slice(0, 5);

  const refresh = async () => {
    setRefreshing(true);
    await Promise.all([feed.refetch(), communities.refetch(), events.refetch(), jobs.refetch()]);
    setRefreshing(false);
  };

  const header = (
    <View>
      {suggestions.length > 0 && (
        <>
          <SectionTitle title="Communautés suggérées" action="Voir tout" onAction={() => router.navigate('/communities')} />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.hList}>
            {suggestions.map((x) => <CommunityTile key={x.id} community={x} />)}
          </ScrollView>
        </>
      )}

      {upcoming.length > 0 && (
        <>
          <SectionTitle title="Événements à venir" action="Voir tout" onAction={() => router.push('/events')} />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.hList}>
            {upcoming.map((e) => <EventTile key={e.id} event={e} />)}
          </ScrollView>
        </>
      )}

      {latestJobs.length > 0 && (
        <>
          <SectionTitle title="Offres d'emploi" action="Voir tout" onAction={() => router.push('/jobs')} />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.hList}>
            {latestJobs.map((j) => <JobTile key={j.id} job={j} />)}
          </ScrollView>
        </>
      )}

      <SectionTitle title="Fil d'actualité" />
    </View>
  );

  const body = feed.isPending ? <Loading label="Chargement du fil…" />
    : feed.error ? <ErrorState message={errorMessage(feed.error)} onRetry={() => feed.refetch()} />
      : (
        <FlatList
          data={posts}
          keyExtractor={(p) => p.id}
          renderItem={({ item }) => (
            <PostCard post={item} author={ctx.users.get(item.authorId)} community={item.communityId ? ctx.communities.get(item.communityId) : undefined} />
          )}
          ListHeaderComponent={header}
          ListEmptyComponent={<EmptyState icon="newspaper-variant-outline" title="Votre fil est vide"
            message="Rejoignez des communautés ou publiez votre premier message." action="Découvrir les communautés"
            onAction={() => router.navigate('/communities')} />}
          ListFooterComponent={posts.length === 0 ? undefined : feed.hasNextPage
            ? <View style={styles.footer}>{feed.isFetchingNextPage ? <ActivityIndicator color={c.primary} /> : null}</View>
            : <View style={styles.footer}><Icon name="check-circle-outline" color={c.primary} /><Muted>Vous êtes à jour</Muted></View>}
          onEndReached={() => feed.hasNextPage && !feed.isFetchingNextPage && feed.fetchNextPage()}
          onEndReachedThreshold={0.5}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refresh} tintColor={c.primary} colors={[c.primary]} />}
          contentContainerStyle={{ paddingBottom: bottom }}
        />
      );

  return (
    <Screen>
      <View style={[styles.top, { paddingTop: insets.top + 8, backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 1)]}>
        <Pressable accessibilityRole="button" accessibilityLabel="Mon profil" onPress={() => router.navigate('/profile')}>
          <Avatar initials={initials(me)} color={me.avatarColor} image={me.avatarImage} size={38} />
        </Pressable>
        <View style={{ flex: 1, marginLeft: 12 }}>
          <T v="h3">{`Bonjour ${me.firstName}`}</T>
          <Muted>Bienvenue sur BéninConnect</Muted>
        </View>
        <IconButton name="magnify" label="Rechercher" onPress={() => router.push('/search')} />
        <IconButton name="bell-outline" label="Notifications" badge={unread} onPress={() => router.push('/notifications')} />
      </View>
      {body}
    </Screen>
  );
}

const styles = StyleSheet.create({
  top: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingBottom: 10, borderBottomWidth: 1, zIndex: 2 },
  hList: { paddingHorizontal: 16 },
  footer: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', gap: 8, paddingVertical: 20 },
});
