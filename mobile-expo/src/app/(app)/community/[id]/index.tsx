import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { EventCard, MembershipButton, PostCard, useCommunityStatus, usePostContext, UserRow } from '@/components/cards';
import { Button, Cover, EmptyState, Icon, IconButton, Muted, QueryView, Screen, Segmented, T, Tag } from '@/components/ui';
import * as fmt from '@/core/format';
import { useColors } from '@/core/theme';
import type { Community } from '@/data/types';
import { location } from '@/data/types';
import { useCommunity, useCommunityEvents, useMembers, usePosts } from '@/features/queries';
import { reportContent } from '@/features/report';
import { roleLabel } from '@/features/rules';

type Section = 'posts' | 'members' | 'events';

export default function CommunityDetails() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useCommunity(id);
  return <Screen><QueryView query={query}>{(community) => <Details community={community} />}</QueryView></Screen>;
}

function Details({ community }: { community: Community }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const status = useCommunityStatus(community);
  const isMember = status === 'Member' || status === 'Owner';
  const restricted = community.visibility === 'Private' && !isMember;
  const [section, setSection] = useState<Section>('posts');
  const posts = usePosts({ communityId: community.id }, !restricted);
  const members = useMembers(community.id, !restricted);
  const events = useCommunityEvents(community.id, !restricted);
  const ctx = usePostContext(posts.data ?? []);

  const refresh = () => { posts.refetch(); members.refetch(); events.refetch(); };

  return (
    <ScrollView contentContainerStyle={{ paddingBottom: insets.bottom + 32 }}
      refreshControl={<RefreshControl refreshing={posts.isRefetching} onRefresh={refresh} tintColor={c.primary} />}>
      <Cover path={community.coverImage} height={190 + insets.top}>
        <View style={[styles.back, { top: insets.top + 4 }]}>
          <IconButton name="arrow-left" label="Retour" color="#FFFFFF" onPress={() => router.back()} />
        </View>
        {status !== 'Owner' && (
          <View style={[styles.back, { top: insets.top + 4, left: undefined, right: 8 }]}>
            <IconButton name="flag-outline" label="Signaler la communauté" color="#FFFFFF" onPress={() => reportContent('Community', community.id)} />
          </View>
        )}
      </Cover>

      <View style={styles.body}>
        <View style={{ flexDirection: 'row', gap: 8 }}>
          <Tag label={community.category} />
          <Tag tone="neutral" label={community.visibility === 'Private' ? 'Privée' : 'Publique'} />
        </View>
        <T v="h1" style={{ marginTop: 10 }}>{community.name}</T>
        <View style={styles.meta}>
          <Icon name="map-marker-outline" size={16} color={c.textSecondary} />
          <Muted>{location(community) || 'International'}</Muted>
          <Muted>·</Muted>
          <Icon name="account-multiple-outline" size={16} color={c.textSecondary} />
          <Muted>{fmt.plural(community.memberIds.length, 'membre')}</Muted>
        </View>
        <Muted style={{ marginTop: 4 }}>{`Créée le ${fmt.longDate(community.createdAt).toLowerCase()}`}</Muted>
        <T style={{ marginTop: 12 }}>{community.description}</T>
        <View style={{ marginTop: 16, alignSelf: 'flex-start' }}><MembershipButton community={community} /></View>
      </View>

      {restricted ? (
        <EmptyState icon="lock-outline" title="Communauté privée"
          message={status === 'Pending'
            ? 'Votre demande est en attente de validation par les administrateurs.'
            : 'Rejoignez cette communauté pour voir ses publications, ses membres et ses événements.'} />
      ) : (
        <>
          <Segmented<Section> value={section} onChange={setSection} items={[
            { key: 'posts', label: 'Publications' },
            { key: 'members', label: `Membres (${community.memberIds.length})` },
            { key: 'events', label: 'Événements' },
          ]} />
          <View style={{ height: 16 }} />

          {section === 'posts' && (
            <>
              {isMember && (
                <Button title="Publier dans la communauté" icon="pencil-outline" kind="secondary" style={styles.action}
                  onPress={() => router.push({ pathname: '/post/new', params: { communityId: community.id } })} />
              )}
              {(posts.data ?? []).length === 0 && !posts.isPending
                ? <EmptyState icon="text-box-outline" title="Aucune publication" message="Lancez la discussion dans cette communauté." />
                : (posts.data ?? []).map((p) => <PostCard key={p.id} post={p} author={ctx.users.get(p.authorId)} />)}
            </>
          )}

          {section === 'members' && (
            <>
              {(members.data ?? []).slice(0, 8).map((m) => (
                <UserRow key={m.user.id} user={m.user} subtitle={location(m.user)} badge={roleLabel(m.role)} />
              ))}
              <Button title="Voir tous les membres" kind="ghost" style={styles.action} onPress={() => router.push(`/community/${community.id}/members`)} />
            </>
          )}

          {section === 'events' && (
            <>
              {isMember && (
                <Button title="Créer un événement" icon="calendar-plus" kind="secondary" style={styles.action}
                  onPress={() => router.push({ pathname: '/events/new', params: { communityId: community.id } })} />
              )}
              {(events.data ?? []).length === 0 && !events.isPending
                ? <EmptyState icon="calendar-blank-outline" title="Aucun événement prévu" />
                : (events.data ?? []).map((e) => <EventCard key={e.id} event={e} />)}
            </>
          )}
        </>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  back: { position: 'absolute', left: 8, borderRadius: 20, backgroundColor: 'rgba(0,0,0,0.35)' },
  body: { padding: 16, paddingBottom: 20 },
  meta: { flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 6, flexWrap: 'wrap' },
  action: { marginHorizontal: 16, marginBottom: 12 },
});
