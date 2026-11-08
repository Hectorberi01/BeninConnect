import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { router } from 'expo-router';
import { memo, useState } from 'react';
import { Alert, type GestureResponderEvent, Pressable, StyleSheet, Text, View } from 'react-native';
import { confirm } from '@/core/dialog';
import * as fmt from '@/core/format';
import { imageSource } from '@/core/images';
import { useMe } from '@/core/session';
import { depth, font, radius, type, useColors } from '@/core/theme';
import type { AppNotification, Community, CommunityEvent, Message, Post, User } from '@/data/types';
import { displayName, initials, isEventFull, location, membershipStatus } from '@/data/types';
import {
  useCommunitiesMap, useDeletePost, useJoinRequests, useMembership, useSharePost, useToggleLike, useToggleParticipation, useUsersMap,
} from '@/features/queries';
import { reportContent } from '@/features/report';
import { AttachmentView } from './attachments';
import { ReactionBar } from './reactions';
import { PostVideo } from './video';
import { Avatar, Button, Card, Cover, Icon, type IconName, Muted, T, Tag } from './ui';

// --- Publications -------------------------------------------------------------------------

/** Auteurs et communautés d'une liste de publications (résolus en lot). */
export function usePostContext(posts: Post[]) {
  const users = useUsersMap(posts.map((p) => p.authorId));
  const communities = useCommunitiesMap(posts.map((p) => p.communityId));
  return { users, communities };
}

export const PostCard = memo(function PostCard({ post, author, community, detailed }: {
  post: Post; author: User | undefined; community?: Community; detailed?: boolean;
}) {
  const c = useColors();
  const me = useMe();
  const like = useToggleLike();
  const share = useSharePost();
  const remove = useDeletePost();
  const mine = post.authorId === me.id;
  const liked = post.likedBy.includes(me.id);
  const likes = post.likedBy.length;
  const open = () => router.push(`/post/${post.id}`);
  const source = imageSource(post.imagePath);

  const askDelete = async () => {
    if (!(await confirm('Supprimer la publication ?', 'Elle sera définitivement supprimée, avec ses commentaires et ses j\'aime.', 'Supprimer', true))) return;
    remove.mutate(post.id, { onSuccess: () => { if (detailed && router.canGoBack()) router.back(); } });
  };
  const openMenu = () => Alert.alert('Publication', undefined, [
    ...(mine
      ? [
        { text: 'Modifier la publication', onPress: () => router.push({ pathname: '/post/new', params: { edit: post.id } }) },
        { text: 'Supprimer la publication', style: 'destructive' as const, onPress: askDelete },
      ]
      : [{ text: 'Signaler la publication', style: 'destructive' as const, onPress: () => reportContent('Post', post.id) }]),
    { text: 'Annuler', style: 'cancel' as const },
  ]);

  return (
    <Card style={styles.post} onPress={detailed ? undefined : open}>
      <View style={styles.postHeader}>
        {author
          ? <Avatar initials={initials(author)} color={author.avatarColor} image={author.avatarImage} onPress={() => router.push(`/user/${author.id}`)} />
          : <Avatar initials="…" color={c.border} />}
        <View style={{ flex: 1 }}>
          <T v="label" style={{ fontSize: 15 }} lines={1} onPress={author ? () => router.push(`/user/${author.id}`) : undefined}>
            {author ? displayName(author) : 'Membre'}
          </T>
          <View style={styles.meta}>
            <Muted>{fmt.relative(post.createdAt)}</Muted>
            {post.editedAt && <Muted>· modifié</Muted>}
            {community && (
              <>
                <Muted>·</Muted>
                <Text style={[type.caption, { color: c.primary, flexShrink: 1 }]} numberOfLines={1}
                  onPress={() => router.push(`/community/${community.id}`)}>{community.name}</Text>
              </>
            )}
            {post.visibility === 'CommunityMembers' && <Icon name="account-group" size={14} color={c.textSecondary} />}
            {post.visibility === 'OnlyMe' && <Icon name="lock-outline" size={14} color={c.textSecondary} />}
          </View>
        </View>
        <Pressable accessibilityRole="button" accessibilityLabel="Options de la publication" hitSlop={12} onPress={openMenu}
          disabled={remove.isPending} style={({ pressed }) => [styles.menu, pressed && { opacity: 0.6 }]}>
          <Icon name="dots-horizontal" size={22} color={c.textSecondary} />
        </Pressable>
      </View>

      <T style={styles.postContent} lines={detailed ? undefined : 6}>{post.content}</T>
      {post.videoPath
        ? <PostVideo videoPath={post.videoPath} posterPath={post.imagePath} />
        : source && <Image source={source} style={styles.postImage} contentFit="cover" transition={150} accessibilityLabel="Image de la publication" />}

      {detailed && (
        <Muted style={{ paddingHorizontal: 16, paddingTop: 10 }}>
          {`${likes} j'aime · ${fmt.plural(post.commentCount, 'commentaire')} · ${fmt.plural(post.shareCount, 'partage')}`}
        </Muted>
      )}

      <View style={[styles.actions, { borderColor: c.border }]}>
        <Action icon={liked ? 'heart' : 'heart-outline'} color={liked ? c.error : undefined}
          label={likes === 0 ? "J'aime" : String(likes)} a11y={liked ? "Je n'aime plus" : "J'aime"} onPress={() => like.mutate(post)} />
        <Action icon="comment-outline" label={post.commentCount === 0 ? 'Commenter' : String(post.commentCount)} a11y="Commenter"
          onPress={() => router.push({ pathname: '/post/[id]', params: { id: post.id, focus: '1' } })} />
        <Action icon="share-variant-outline" label={post.shareCount === 0 ? 'Partager' : String(post.shareCount)} a11y="Partager"
          onPress={() => share.mutate({ post, authorName: author ? displayName(author) : 'Un membre' })} />
      </View>
    </Card>
  );
});

function Action({ icon, label, onPress, color, a11y }: { icon: IconName; label: string; onPress: () => void; color?: string; a11y: string }) {
  const c = useColors();
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={a11y} onPress={onPress} style={({ pressed }) => [styles.action, pressed && { opacity: 0.6 }]}>
      <Icon name={icon} size={20} color={color ?? c.textSecondary} />
      <Text style={[type.label, { color: c.textSecondary }]}>{label}</Text>
    </Pressable>
  );
}

// --- Communautés --------------------------------------------------------------------------

const membersText = (n: number) => (n === 0 ? 'Aucun membre' : fmt.plural(n, 'membre'));

export function useCommunityStatus(c: Community) {
  const me = useMe();
  const pending = useJoinRequests().data ?? [];
  return membershipStatus(c, me.id, pending);
}

export function MembershipButton({ community, small }: { community: Community; small?: boolean }) {
  const status = useCommunityStatus(community);
  const actions = useMembership();
  const [busy, setBusy] = useState(false);
  const run = (fn: () => Promise<void>) => {
    if (busy) return;
    setBusy(true);
    fn().finally(() => setBusy(false));
  };

  switch (status) {
    case 'Owner':
      return <Button small={small} kind="secondary" icon="crown-outline" title="Créateur" disabled />;
    case 'Member':
      return (
        <Button small={small} kind="secondary" icon="check" title="Membre" loading={busy}
          onPress={() => run(async () => {
            const ok = await confirm('Quitter la communauté ?',
              `Vous ne verrez plus les publications réservées aux membres de « ${community.name} ».`, 'Quitter', true);
            if (ok) await actions.leave(community);
          })} />
      );
    case 'Pending':
      return <Button small={small} kind="outline" icon="clock-outline" title="Demande envoyée" loading={busy}
        onPress={() => run(() => actions.cancel(community))} />;
    default:
      return <Button small={small} icon={community.visibility === 'Private' ? 'lock-outline' : 'plus'}
        title={community.visibility === 'Private' ? 'Demander' : 'Rejoindre'} loading={busy}
        onPress={() => run(() => actions.join(community))} />;
  }
}

/** Carte pleine largeur (liste des communautés). */
export function CommunityCard({ community }: { community: Community }) {
  const c = useColors();
  return (
    <Card style={styles.communityCard} onPress={() => router.push(`/community/${community.id}`)}>
      <Cover path={community.coverImage} height={110} />
      <View style={{ padding: 14, gap: 6 }}>
        <View style={styles.rowCenter}>
          <T v="h3" style={{ flex: 1 }} lines={1}>{community.name}</T>
          {community.visibility === 'Private' && <Icon name="lock-outline" size={18} color={c.textSecondary} />}
        </View>
        <Muted lines={2}>{community.description}</Muted>
        <View style={[styles.rowCenter, { gap: 8, marginTop: 4 }]}>
          <Tag label={community.category} />
          <Muted style={{ flex: 1 }} lines={1}>{`${location(community) || 'International'} · ${membersText(community.memberIds.length)}`}</Muted>
        </View>
        <View style={{ marginTop: 6, alignSelf: 'flex-start' }}><MembershipButton community={community} small /></View>
      </View>
    </Card>
  );
}

/** Tuile compacte (suggestions, profil). */
export function CommunityTile({ community, showAction = true }: { community: Community; showAction?: boolean }) {
  return (
    <Card style={styles.tile} onPress={() => router.push(`/community/${community.id}`)}>
      <Cover path={community.coverImage} height={84} />
      <View style={{ padding: 10, gap: 4, flex: 1 }}>
        <T v="label" lines={2} style={{ fontSize: 14 }}>{community.name}</T>
        <Muted lines={1}>{membersText(community.memberIds.length)}</Muted>
        {showAction && <View style={{ marginTop: 'auto', paddingTop: 6 }}><MembershipButton community={community} small /></View>}
      </View>
    </Card>
  );
}

// --- Événements ---------------------------------------------------------------------------

export function participantsText(e: CommunityEvent) {
  const n = e.participantIds.length;
  return e.capacity != null ? `${n} / ${e.capacity} participants` : fmt.plural(n, 'participant');
}

export function ParticipationButton({ event, small, full: fullWidth }: { event: CommunityEvent; small?: boolean; full?: boolean }) {
  const me = useMe();
  const toggle = useToggleParticipation();
  const participating = event.participantIds.includes(me.id);
  const isFull = isEventFull(event) && !participating;
  const label = participating
    ? (small ? 'Inscrit' : 'Inscrit · Annuler ma participation')
    : isFull ? (small ? 'Complet' : 'Événement complet') : (small ? 'Participer' : "Participer à l'événement");
  return (
    <Button small={small} title={label} kind={participating ? 'secondary' : 'primary'} icon={participating ? 'check' : isFull ? 'cancel' : 'calendar-plus'}
      disabled={isFull} loading={toggle.isPending} onPress={() => toggle.mutate(event.id)} style={fullWidth ? { alignSelf: 'stretch' } : undefined} />
  );
}

function DateBadge({ iso }: { iso: string }) {
  const c = useColors();
  return (
    <View style={[styles.dateBadge, { backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 2)]}>
      <Text style={{ fontFamily: font.bold, fontSize: 18, color: c.primary }}>{new Date(iso).getDate()}</Text>
      <Text style={{ fontFamily: font.semibold, fontSize: 11, color: c.textSecondary }}>{fmt.monthShort(iso)}</Text>
    </View>
  );
}

export function EventCard({ event }: { event: CommunityEvent }) {
  return (
    <Card style={styles.eventCard} onPress={() => router.push(`/events/${event.id}`)}>
      <Cover path={event.imagePath} height={130}><DateBadge iso={event.startsAt} /></Cover>
      <View style={{ padding: 14, gap: 6 }}>
        <Tag label={event.category} />
        <T v="h3" lines={2}>{event.title}</T>
        <Muted>{`${fmt.longDate(event.startsAt)} · ${fmt.clock(event.startsAt)}`}</Muted>
        <View style={[styles.rowCenter, { gap: 10, marginTop: 4 }]}>
          <Muted style={{ flex: 1 }} lines={1}>{`${event.city} · ${participantsText(event)}`}</Muted>
          <ParticipationButton event={event} small />
        </View>
      </View>
    </Card>
  );
}

export function EventTile({ event }: { event: CommunityEvent }) {
  return (
    <Card style={styles.eventTile} onPress={() => router.push(`/events/${event.id}`)}>
      <Cover path={event.imagePath} height={96}><DateBadge iso={event.startsAt} /></Cover>
      <View style={{ padding: 10, gap: 4 }}>
        <T v="label" lines={2} style={{ fontSize: 14 }}>{event.title}</T>
        <Muted lines={1}>{`${fmt.shortDate(event.startsAt)} · ${fmt.clock(event.startsAt)} · ${event.city}`}</Muted>
      </View>
    </Card>
  );
}

// --- Membres ------------------------------------------------------------------------------

export function UserRow({ user, subtitle, badge, onPress }: { user: User; subtitle?: string; badge?: string | null; onPress?: () => void }) {
  const c = useColors();
  return (
    <Pressable accessibilityRole="button" onPress={onPress ?? (() => router.push(`/user/${user.id}`))}
      style={({ pressed }) => [styles.userRow, { borderColor: c.border }, pressed && { opacity: 0.7 }]}>
      <Avatar initials={initials(user)} color={user.avatarColor} image={user.avatarImage} />
      <View style={{ flex: 1 }}>
        <T v="label" style={{ fontSize: 15 }} lines={1}>{displayName(user)}</T>
        <Muted lines={1}>{subtitle ?? [user.profession, user.city].filter(Boolean).join(' · ')}</Muted>
      </View>
      {!!badge && <Tag label={badge} />}
      <Icon name="chevron-right" color={c.textSecondary} />
    </Pressable>
  );
}

// --- Notifications ------------------------------------------------------------------------

const NOTIFICATION_ICONS: Record<AppNotification['category'], IconName> = {
  Social: 'heart-outline', Communities: 'account-group-outline', Messages: 'message-outline', Events: 'calendar-outline', System: 'information-outline',
};

export function NotificationItem({ item, onPress }: { item: AppNotification; onPress: () => void }) {
  const c = useColors();
  return (
    <Pressable accessibilityRole="button" onPress={onPress}
      style={({ pressed }) => [styles.notification, { backgroundColor: item.isRead ? c.glassStrong : c.primaryLight, borderColor: c.glassBorder }, depth(c, 1), pressed && { opacity: 0.8 }]}>
      <View style={[styles.notificationIcon, { backgroundColor: c.surface }, depth(c, 1)]}>
        <Icon name={NOTIFICATION_ICONS[item.category]} size={20} color={c.primary} />
      </View>
      <View style={{ flex: 1, gap: 2 }}>
        <T v="label" style={{ fontSize: 14 }}>{item.title}</T>
        <T v="caption" lines={2}>{item.body}</T>
        <Muted>{fmt.relative(item.createdAt)}</Muted>
      </View>
      {!item.isRead && <View style={[styles.dot, { backgroundColor: c.primary }]} />}
    </Pressable>
  );
}

// --- Messages -----------------------------------------------------------------------------

export function MessageBubble({ message, mine, me, onLongPress, onToggleReaction }: {
  message: Message; mine: boolean; me: string;
  onLongPress?: (e: GestureResponderEvent) => void; onToggleReaction?: (emoji: string) => void;
}) {
  const c = useColors();
  const deleted = !!message.isDeleted;
  const meta = [type.caption, { fontSize: 11, color: mine && !deleted ? '#D1FAE5' : c.textSecondary }];
  const body = (
    <>
      {deleted
        ? <T color={c.textSecondary} style={{ fontStyle: 'italic' }}>Message supprimé</T>
        : (
          <>
            {message.attachment && <AttachmentView message={message} mine={mine} />}
            {!!message.content && <T color={mine ? '#FFFFFF' : c.text}>{message.content}</T>}
          </>
        )}
      <View style={[styles.rowCenter, { gap: 6, alignSelf: 'flex-end', marginTop: 2 }]}>
        {message.isSimulated && <Text style={meta}>Simulé</Text>}
        {!deleted && message.editedAt && <Text style={meta}>modifié</Text>}
        <Text style={meta}>{fmt.clock(message.sentAt)}</Text>
      </View>
    </>
  );
  return (
    <View style={[styles.bubbleRow, { alignItems: mine ? 'flex-end' : 'flex-start' }]}>
      <Pressable onLongPress={deleted ? undefined : onLongPress} delayLongPress={280}
        accessibilityHint={deleted ? undefined : mine ? 'Appui long pour réagir, modifier ou supprimer' : 'Appui long pour réagir'}
        style={({ pressed }) => [{ maxWidth: '80%' }, pressed && !deleted && { transform: [{ scale: 0.98 }] }]}>
        {mine && !deleted ? (
          <LinearGradient colors={[c.primaryTop, c.bubbleMine]} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }}
            style={[styles.bubble, { borderBottomRightRadius: 6 }, depth(c, 1)]}>{body}</LinearGradient>
        ) : (
          <View style={[styles.bubble, mine ? { borderBottomRightRadius: 6 } : { borderBottomLeftRadius: 6 },
            { backgroundColor: deleted ? 'transparent' : c.glassStrong, borderColor: c.glassBorder, borderWidth: 1 }, !deleted && depth(c, 1)]}>{body}</View>
        )}
      </Pressable>
      {onToggleReaction && !deleted && message.reactions.length > 0 && (
        <View style={{ marginTop: -6, paddingHorizontal: 8 }}>
          <ReactionBar reactions={message.reactions} me={me} onToggle={onToggleReaction} align={mine ? 'flex-end' : 'flex-start'} />
        </View>
      )}
    </View>
  );
}

// --- Sélecteur d'images embarquées --------------------------------------------------------

export function ImageGrid({ paths, selected, onSelect, columns = 4 }: { paths: string[]; selected: string | null; onSelect: (p: string) => void; columns?: number }) {
  const c = useColors();
  return (
    <View style={styles.grid}>
      {paths.map((p) => (
        <Pressable key={p} accessibilityRole="button" accessibilityState={{ selected: p === selected }} accessibilityLabel={`Image ${p}`}
          onPress={() => onSelect(p)} style={{ width: `${100 / columns}%`, padding: 3 }}>
          <View style={[styles.gridItem, { borderColor: p === selected ? c.primary : 'transparent' }]}>
            <Image source={imageSource(p)} style={StyleSheet.absoluteFill} contentFit="cover" />
            {p === selected && <View style={[styles.gridCheck, { backgroundColor: c.primary }]}><Icon name="check" size={14} color="#FFFFFF" /></View>}
          </View>
        </Pressable>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  post: { marginHorizontal: 16, marginBottom: 12 },
  postHeader: { flexDirection: 'row', alignItems: 'center', gap: 12, padding: 16, paddingBottom: 8 },
  menu: { padding: 4, marginRight: -4 },
  meta: { flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 2 },
  postContent: { paddingHorizontal: 16, paddingBottom: 10 },
  postImage: { width: '100%', aspectRatio: 4 / 3 },
  actions: { flexDirection: 'row', borderTopWidth: StyleSheet.hairlineWidth, marginTop: 8 },
  action: { flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, minHeight: 46 },
  rowCenter: { flexDirection: 'row', alignItems: 'center' },
  communityCard: { marginHorizontal: 16, marginBottom: 12 },
  tile: { width: 168, marginRight: 12 },
  eventCard: { marginHorizontal: 16, marginBottom: 12 },
  eventTile: { width: 200, marginRight: 12 },
  dateBadge: { position: 'absolute', top: 10, left: 10, borderRadius: 14, borderWidth: 1, paddingHorizontal: 10, paddingVertical: 4, alignItems: 'center' },
  userRow: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: StyleSheet.hairlineWidth },
  notification: { flexDirection: 'row', gap: 12, padding: 14, marginHorizontal: 16, marginBottom: 10, borderRadius: radius.card, borderWidth: 1 },
  notificationIcon: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center' },
  dot: { width: 10, height: 10, borderRadius: 5, marginTop: 4 },
  bubbleRow: { paddingHorizontal: 12, marginVertical: 3 },
  bubble: { borderRadius: 18, paddingHorizontal: 14, paddingVertical: 8 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', marginHorizontal: -3 },
  gridItem: { aspectRatio: 1, borderRadius: 10, overflow: 'hidden', borderWidth: 3 },
  gridCheck: { position: 'absolute', top: 4, right: 4, width: 20, height: 20, borderRadius: 10, alignItems: 'center', justifyContent: 'center' },
});
