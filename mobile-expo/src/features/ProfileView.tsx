import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CommunityTile, PostCard, usePostContext } from '@/components/cards';
import { Avatar, Button, Chip, Cover, EmptyState, Icon, IconButton, Muted, QueryView, SectionTitle, T } from '@/components/ui';
import { confirm } from '@/core/dialog';
import * as fmt from '@/core/format';
import { askPhotoSource, pickPhoto } from '@/core/photos';
import { blockUser, unblockUser, useIsBlocked } from './block';
import { reportContent } from './report';
import { queryClient } from '@/core/queryClient';
import { useMe, useSession } from '@/core/session';
import { depth, radius, useColors } from '@/core/theme';
import { useTabBarSpace } from '@/components/glass';
import { toast } from '@/core/toast';
import { data } from '@/data';
import type { User } from '@/data/types';
import { displayName, errorMessage, initials, location } from '@/data/types';
import { useMemberCommunities, usePosts, useUpdateProfile, useUser } from './queries';

export const RECENT_POSTS = 10;

export async function signOut() {
  const email = useSession.getState().user?.email;
  await useSession.getState().signOut();
  queryClient.clear();
  router.replace({ pathname: '/login', params: email ? { email } : {} });
}

/** Profil (onglet « Profil » pour soi, /user/[id] pour les autres membres). */
export function ProfileView({ userId, showBack }: { userId: string; showBack?: boolean }) {
  const query = useUser(userId);
  return <QueryView query={query} loadingLabel="Chargement du profil…">{(user) => <Profile user={user} showBack={showBack} />}</QueryView>;
}

function Profile({ user, showBack }: { user: User; showBack?: boolean }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const isMe = user.id === me.id;
  const blocked = useIsBlocked(isMe ? undefined : user.id);
  const shownUser = isMe ? me : user;
  const posts = usePosts({ authorId: user.id });
  const communities = useMemberCommunities(user.id);
  const [starting, setStarting] = useState(false);
  const update = useUpdateProfile();
  const [changing, setChanging] = useState<'avatar' | 'cover' | null>(null);

  // Changement direct de la photo de profil ou de couverture (sans passer par « Modifier le profil »).
  const changeImage = async (kind: 'avatar' | 'cover') => {
    const current = kind === 'avatar' ? me.avatarImage : me.coverImage;
    const choice = await askPhotoSource(kind === 'avatar' ? 'Photo de profil' : 'Photo de couverture', { remove: !!current });
    if (!choice || choice === 'demo') return;
    let path = '';
    if (choice !== 'remove') {
      const photo = await pickPhoto(choice, { aspect: kind === 'avatar' ? [1, 1] : [16, 9] });
      if (!photo) return;
      setChanging(kind);
      try {
        path = await data().uploadImage(photo);
      } catch (e) {
        setChanging(null);
        toast(`Envoi de la photo impossible : ${errorMessage(e)}`, 'error');
        return;
      }
    } else {
      setChanging(kind);
    }
    const { firstName, lastName, profession, country, city, bio, interests } = me;
    update.mutate({ firstName, lastName, profession, country, city, bio, interests, [kind === 'avatar' ? 'avatarImage' : 'coverImage']: path }, {
      onSuccess: () => toast(kind === 'avatar' ? 'Photo de profil mise à jour.' : 'Couverture mise à jour.', 'success'),
      onSettled: () => setChanging(null),
    });
  };
  const tabSpace = useTabBarSpace();

  const visiblePosts = (posts.data ?? []).filter((p) => isMe || p.visibility === 'Public');
  const recent = visiblePosts.slice(0, RECENT_POSTS);
  const ctx = usePostContext(recent);
  const shownCommunities = (communities.data ?? []).filter((x) => isMe || x.visibility === 'Public');
  const likes = visiblePosts.reduce((n, p) => n + p.likedBy.length, 0);

  const message = async () => {
    setStarting(true);
    try {
      const conversation = await data().startConversation(user.id);
      router.push(`/conversation/${conversation.id}`);
    } catch (e) {
      toast(errorMessage(e), 'error');
    } finally {
      setStarting(false);
    }
  };

  const logout = async () => {
    if (await confirm('Se déconnecter ?', 'Vous pourrez vous reconnecter avec le compte de démonstration.', 'Se déconnecter', true)) await signOut();
  };

  return (
    <ScrollView
      contentContainerStyle={{ paddingBottom: showBack ? insets.bottom + 32 : tabSpace }}
      refreshControl={<RefreshControl refreshing={posts.isRefetching} onRefresh={() => { posts.refetch(); communities.refetch(); }} tintColor={c.primary} />}
    >
      <Cover path={shownUser.coverImage} height={150 + insets.top}>
        <View style={[styles.coverBar, { top: insets.top + 4 }]}>
          {showBack ? <RoundButton icon="arrow-left" label="Retour" onPress={() => router.back()} /> : <View />}
          {isMe
            ? <RoundButton icon="cog-outline" label="Paramètres" onPress={() => router.push('/settings')} />
            : <RoundButton icon="dots-horizontal" label="Options du membre" onPress={() => Alert.alert(displayName(user), undefined, [
              { text: 'Signaler ce membre', style: 'destructive', onPress: () => reportContent('User', user.id) },
              blocked
                ? { text: `Débloquer ${displayName(user)}`, onPress: () => unblockUser(user) }
                : { text: `Bloquer ${displayName(user)}`, style: 'destructive', onPress: () => blockUser(user) },
              { text: 'Annuler', style: 'cancel' },
            ])} />}
        </View>
        {isMe && (
          <Pressable accessibilityRole="button" accessibilityLabel="Changer la photo de couverture" onPress={() => changeImage('cover')}
            disabled={!!changing} style={({ pressed }) => [styles.coverButton, pressed && { opacity: 0.7 }]}>
            {changing === 'cover' ? <ActivityIndicator color="#FFFFFF" size="small" /> : <Icon name="camera-outline" size={18} color="#FFFFFF" />}
            <T v="label" color="#FFFFFF">Couverture</T>
          </Pressable>
        )}
      </Cover>

      <View style={{ paddingHorizontal: 16 }}>
        <View style={[styles.avatarRing, { backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 2)]}>
          <Avatar initials={initials(shownUser)} color={shownUser.avatarColor} image={shownUser.avatarImage} size={92}
            onPress={isMe && !changing ? () => changeImage('avatar') : undefined} />
          {isMe && (
            <View pointerEvents="none" style={[styles.avatarCamera, { backgroundColor: c.primary, borderColor: c.surface }]}>
              {changing === 'avatar' ? <ActivityIndicator color="#FFFFFF" size="small" /> : <Icon name="camera" size={15} color="#FFFFFF" />}
            </View>
          )}
        </View>
        <T v="h2">{displayName(shownUser)}</T>
        {!!shownUser.profession && <T style={{ marginTop: 2 }}>{shownUser.profession}</T>}
        <View style={styles.metaLine}>
          <Icon name="map-marker-outline" size={16} color={c.textSecondary} />
          <Muted>{location(shownUser)}</Muted>
        </View>
        {!!shownUser.originCity && (
          <View style={styles.metaLine}>
            <Icon name="home-heart" size={16} color={c.textSecondary} />
            <Muted>{`Originaire de ${shownUser.originCity}`}</Muted>
          </View>
        )}
        <View style={styles.metaLine}>
          <Icon name="calendar-outline" size={16} color={c.textSecondary} />
          <Muted>{`Membre depuis ${fmt.monthYear(shownUser.joinedAt)}`}</Muted>
        </View>

        <View style={[styles.stats, { backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 2)]}>
          <Stat value={visiblePosts.length} label="Publications" />
          <Stat value={shownCommunities.length} label="Communautés" />
          <Stat value={likes} label="J'aime reçus" />
        </View>

        <View style={{ flexDirection: 'row', gap: 10 }}>
          {isMe ? (
            <Button title="Modifier le profil" icon="pencil-outline" onPress={() => router.push('/profile/edit')} style={{ flex: 1 }} />
          ) : (
            <Button title="Envoyer un message" icon="message-outline" loading={starting} onPress={message} style={{ flex: 1 }} />
          )}
        </View>
      </View>

      <SectionTitle title="À propos" />
      <View style={{ paddingHorizontal: 16 }}>
        {shownUser.bio.trim()
          ? <T>{shownUser.bio}</T>
          : <Muted style={{ fontSize: 15 }}>{isMe ? 'Ajoutez une biographie pour vous présenter à la communauté.' : 'Pas encore de biographie.'}</Muted>}
        {shownUser.interests.length > 0 && (
          <View style={styles.interests}>{shownUser.interests.map((i) => <Chip key={i} label={i} />)}</View>
        )}
      </View>

      <SectionTitle title="Communautés" />
      {shownCommunities.length > 0 ? (
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ paddingHorizontal: 16 }}>
          {shownCommunities.map((x) => <CommunityTile key={x.id} community={x} showAction={false} />)}
        </ScrollView>
      ) : <Muted style={{ paddingHorizontal: 16, fontSize: 15 }}>Aucune communauté pour le moment.</Muted>}

      <SectionTitle title="Publications récentes" />
      {posts.isPending ? null : recent.length > 0
        ? recent.map((p) => <PostCard key={p.id} post={p} author={ctx.users.get(p.authorId)} community={p.communityId ? ctx.communities.get(p.communityId) : undefined} />)
        : <EmptyState icon="text-box-outline" title="Aucune publication" message={isMe ? 'Partagez votre première publication avec la communauté.' : undefined}
          action={isMe ? 'Publier' : undefined} onAction={() => router.push('/post/new')} />}

      {isMe && <Button title="Se déconnecter" kind="danger" icon="logout" onPress={logout} style={{ marginHorizontal: 16, marginTop: 16 }} />}
    </ScrollView>
  );
}

function Stat({ value, label }: { value: number; label: string }) {
  return (
    <View style={{ flex: 1, alignItems: 'center' }}>
      <T v="h3">{String(value)}</T>
      <Muted>{label}</Muted>
    </View>
  );
}

function RoundButton({ icon, label, onPress }: { icon: 'arrow-left' | 'cog-outline' | 'dots-horizontal'; label: string; onPress: () => void }) {
  return (
    <View style={styles.round}><IconButton name={icon} label={label} color="#FFFFFF" onPress={onPress} /></View>
  );
}

const styles = StyleSheet.create({
  coverBar: { position: 'absolute', left: 8, right: 8, flexDirection: 'row', justifyContent: 'space-between' },
  coverButton: {
    position: 'absolute', right: 12, bottom: 12, flexDirection: 'row', alignItems: 'center', gap: 6,
    paddingHorizontal: 12, height: 34, borderRadius: 17, backgroundColor: 'rgba(0,0,0,0.45)',
  },
  avatarCamera: {
    position: 'absolute', right: 2, bottom: 4, width: 30, height: 30, borderRadius: 15, borderWidth: 2,
    alignItems: 'center', justifyContent: 'center',
  },
  round: { borderRadius: 20, backgroundColor: 'rgba(0,0,0,0.35)' },
  avatarRing: { marginTop: -50, marginBottom: 10, alignSelf: 'flex-start', padding: 4, borderRadius: 52, borderWidth: 1 },
  metaLine: { flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: 6 },
  stats: { flexDirection: 'row', paddingVertical: 14, borderRadius: radius.card, borderWidth: 1, marginVertical: 16 },
  interests: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 12 },
});
