import { router } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { EventTile, PostCard, usePostContext } from '@/components/cards';
import {
  HomeCommunityCard, HomeHeader, HomeHero, MyCommunities, PostSkeleton, ExploreChips, SkeletonRow, VoyagesPromo,
} from '@/components/home';
import { EmptyState, ErrorState, Icon, Muted, Screen, SectionTitle } from '@/components/ui';
import { useTabBarSpace } from '@/components/glass';
import { usePreferences } from '@/core/preferences';
import { useMe } from '@/core/session';
import { useColors } from '@/core/theme';
import { errorMessage } from '@/data/types';
import {
  useCommunities, useEvents, useFeed, useHomeBanners, useUnreadNotifications, useUsersMap, useVoyagesEnabled,
} from '@/features/queries';

export default function Home() {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const feed = useFeed();
  const communities = useCommunities();
  const events = useEvents({});
  const voyages = useVoyagesEnabled().enabled;
  const banners = useHomeBanners();
  const unread = useUnreadNotifications().data ?? 0;
  const [refreshing, setRefreshing] = useState(false);
  const bottom = useTabBarSpace();

  // Bannière haute à la toute première visite seulement
  const [compactHero] = useState(() => usePreferences.getState().homeIntroSeen);
  useEffect(() => {
    if (!usePreferences.getState().homeIntroSeen) usePreferences.getState().set({ homeIntroSeen: true });
  }, []);

  const posts = useMemo(() => {
    const seen = new Set<string>();
    return (feed.data?.pages ?? []).flatMap((p) => p.items).filter((p) => (seen.has(p.id) ? false : (seen.add(p.id), true)));
  }, [feed.data]);
  const ctx = usePostContext(posts);
  const all = communities.data ?? [];
  const mine = all.filter((x) => x.memberIds.includes(me.id)).slice(0, 10);
  const suggestions = all.filter((x) => !x.memberIds.includes(me.id)).slice(0, 8);
  const members = useUsersMap(suggestions.flatMap((x) => x.memberIds.slice(0, 3)));
  const upcoming = (events.data ?? []).slice(0, 5);

  const refresh = async () => {
    setRefreshing(true);
    await Promise.all([feed.refetch(), banners.refetch(), communities.refetch(), events.refetch()]);
    setRefreshing(false);
  };

  const header = (
    <View>
      <HomeHeader unread={unread} top={insets.top} />
      <HomeHero compact={compactHero} voyages={voyages} banners={banners.data} />
      <ExploreChips voyages={voyages} />
      {voyages && <VoyagesPromo />}

      {/*mine.length > 0 && (
        <>
          <SectionTitle title="Mes communautés" action="Voir tout" onAction={() => router.navigate('/communities')} />
          <MyCommunities communities={mine} />
        </>
      )*/}

      {/*communities.isPending ? (
        <>
          <SectionTitle title="Communautés suggérées" />
          <SkeletonRow width={250} height={196} />
        </>
      ) : suggestions.length > 0 && (
        <>
          <SectionTitle title="Communautés suggérées" action="Voir tout" onAction={() => router.navigate('/communities')} />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.hList}>
            {suggestions.map((x) => (
              <HomeCommunityCard key={x.id} community={x} members={x.memberIds.slice(0, 3).flatMap((id) => members.get(id) ?? [])} />
            ))}
          </ScrollView>
        </>
      )*/}

      {upcoming.length > 0 && (
        <>
          <SectionTitle title="Événements à venir" action="Voir tout" onAction={() => router.push('/events')} />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.hList}>
            {upcoming.map((e) => <EventTile key={e.id} event={e} />)}
          </ScrollView>
        </>
      )}

      <SectionTitle title="Fil d'actualité" />
    </View>
  );

  return (
    <Screen>
      <FlatList
        data={feed.isPending || feed.error ? [] : posts}
        keyExtractor={(p) => p.id}
        renderItem={({ item }) => (
          <PostCard post={item} author={ctx.users.get(item.authorId)} community={item.communityId ? ctx.communities.get(item.communityId) : undefined} />
        )}
        ListHeaderComponent={header}
        ListEmptyComponent={feed.isPending ? <View><PostSkeleton /><PostSkeleton /></View>
          : feed.error ? <ErrorState message={errorMessage(feed.error)} onRetry={() => feed.refetch()} />
            : <EmptyState icon="newspaper-variant-outline" title="Votre fil est vide"
              message="Rejoignez des communautés ou publiez votre premier message." action="Découvrir les communautés"
              onAction={() => router.navigate('/communities')} />}
        ListFooterComponent={posts.length === 0 ? undefined : feed.hasNextPage
          ? <View style={styles.footer}>{feed.isFetchingNextPage ? <ActivityIndicator color={c.primary} /> : null}</View>
          : <View style={styles.footer}><Icon name="check-circle-outline" color={c.primary} /><Muted>Vous êtes à jour</Muted></View>}
        onEndReached={() => feed.hasNextPage && !feed.isFetchingNextPage && feed.fetchNextPage()}
        onEndReachedThreshold={0.5}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refresh} tintColor={c.primary} colors={[c.primary]} />}
        contentContainerStyle={{ paddingBottom: bottom }}
        keyboardDismissMode="on-drag"
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  hList: { 
    paddingHorizontal: 16, 
    gap: 12 
  },
  serviceSkeleton: { 
    flexDirection: 'row', 
    gap: 8, 
    paddingHorizontal: 16 },
  footer: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', gap: 8, paddingVertical: 20 },
});
