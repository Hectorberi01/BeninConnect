import { router, useLocalSearchParams } from 'expo-router';
import { type ComponentRef, useRef, useState } from 'react';
import { Alert, FlatList, KeyboardAvoidingView, Platform, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { PostCard, usePostContext } from '@/components/cards';
import { ReactionBar, useReactionPicker } from '@/components/reactions';
import { MentionSuggestions, RichText } from '@/components/social';
import { Avatar, Header, Icon, IconButton, Muted, QueryView, Screen, T } from '@/components/ui';
import * as fmt from '@/core/format';
import { useMe } from '@/core/session';
import { depth, font, useColors } from '@/core/theme';
import { insertMention, keptMentions } from '@/data/social';
import type { Comment, Post, User } from '@/data/types';
import { displayName, errorMessage, initials } from '@/data/types';
import { blockUser } from '@/features/block';
import { reportContent } from '@/features/report';
import { useAddComment, useComments, usePinComment, usePost, useReactToComment, useUsersMap } from '@/features/queries';

export default function PostDetails() {
  const { id, focus } = useLocalSearchParams<{ id: string; focus?: string }>();
  const query = usePost(id);
  return (
    <Screen>
      <Header title="Publication" />
      <QueryView query={query}>{(post) => <Details post={post} focus={focus === '1'} />}</QueryView>
    </Screen>
  );
}

type Row = { comment: Comment; reply: boolean } | { more: string; count: number; open: boolean };

function Details({ post, focus }: { post: Post; focus: boolean }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const ctx = usePostContext([post]);
  const comments = useComments(post.id);
  const authors = useUsersMap((comments.data ?? []).map((x) => x.authorId));
  const add = useAddComment(post.id);
  const react = useReactToComment(post.id);
  const pinComment = usePinComment(post.id);
  const picker = useReactionPicker();
  const input = useRef<ComponentRef<typeof TextInput>>(null);
  const [draft, setDraft] = useState('');
  const [cursor, setCursor] = useState(0);
  const [chosen, setChosen] = useState<User[]>([]);
  const [replyTo, setReplyTo] = useState<Comment | null>(null);
  const [opened, setOpened] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const isAuthor = post.authorId === me.id;

  // Commentaires d'origine (épinglé d'abord), suivis de leurs réponses dépliées
  const all = comments.data ?? [];
  const roots = all.filter((x) => !x.parentId)
    .sort((a, b) => Number(b.id === post.pinnedCommentId) - Number(a.id === post.pinnedCommentId) || a.createdAt.localeCompare(b.createdAt));
  const rows: Row[] = roots.flatMap((root) => {
    const replies = all.filter((x) => x.parentId === root.id);
    const open = opened.has(root.id);
    return [
      { comment: root, reply: false } as Row,
      ...(open ? replies.map((r) => ({ comment: r, reply: true }) as Row) : []),
      ...(replies.length ? [{ more: root.id, count: replies.length, open } as Row] : []),
    ];
  });

  const toggleThread = (id: string) => setOpened((s) => {
    const next = new Set(s);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });

  const startReply = (comment: Comment, author: User) => {
    setReplyTo(comment);
    setChosen((list) => (author.id === me.id || list.some((u) => u.id === author.id) ? list : [...list, author]));
    const prefix = author.id === me.id ? '' : `@${displayName(author)} `;
    setDraft(prefix);
    setCursor(prefix.length);
    input.current?.focus();
  };

  const pickMention = (u: User) => {
    const next = insertMention(draft, cursor, u);
    setDraft(next.text);
    setCursor(next.cursor);
    setChosen((list) => (list.some((x) => x.id === u.id) ? list : [...list, u]));
  };

  const send = () => {
    if (!draft.trim() || add.isPending) return;
    setError(null);
    const parent = replyTo;
    add.mutate({ content: draft, parentId: parent?.id ?? null, mentions: keptMentions(draft, chosen) }, {
      onSuccess: () => {
        setDraft('');
        setChosen([]);
        setReplyTo(null);
        if (parent) setOpened((s) => new Set(s).add(parent.parentId ?? parent.id));
      },
      onError: (e) => setError(errorMessage(e)),
    });
  };

  const renderRow = ({ item }: { item: Row }) => {
    if ('more' in item) {
      return (
        <Pressable accessibilityRole="button" onPress={() => toggleThread(item.more)} style={styles.more}>
          <View style={[styles.moreLine, { backgroundColor: c.border }]} />
          <T v="label" color={c.textSecondary} style={{ fontSize: 12 }}>
            {item.open ? 'Masquer les réponses' : item.count === 1 ? 'Voir la réponse' : `Voir les ${item.count} réponses`}
          </T>
        </Pressable>
      );
    }
    const { comment, reply } = item;
    const author = authors.get(comment.authorId);
    if (!author) return null;
    const mine = comment.reactions.find((r) => r.userId === me.id)?.emoji ?? null;
    const hearts = comment.reactions.filter((r) => r.emoji === '❤️').length;
    const others = comment.reactions.filter((r) => r.emoji !== '❤️');
    const toggle = (emoji: string) => react.mutate({ commentId: comment.id, emoji });
    const openPicker = (e: Parameters<typeof picker.open>[0]) => picker.open(e, mine, toggle);
    const pinned = comment.id === post.pinnedCommentId;
    const menu = () => Alert.alert('Commentaire', undefined, [
      ...(isAuthor && !reply ? [{ text: pinned ? 'Désépingler le commentaire' : 'Épingler le commentaire', onPress: () => pinComment.mutate(pinned ? null : comment.id) }] : []),
      ...(comment.authorId !== me.id ? [
        { text: 'Signaler le commentaire', style: 'destructive' as const, onPress: () => reportContent('Comment', comment.id) },
        { text: `Bloquer ${displayName(author)}`, style: 'destructive' as const, onPress: () => blockUser(author) },
      ] : []),
      { text: 'Annuler', style: 'cancel' as const },
    ]);
    const size = reply ? 28 : 34;
    return (
      <View style={[styles.comment, reply && { paddingLeft: 60 }]}>
        <Avatar initials={initials(author)} color={author.avatarColor} image={author.avatarImage} size={size} onPress={() => router.push(`/user/${author.id}`)} />
        <View style={{ flex: 1 }}>
          <Pressable onLongPress={openPicker} delayLongPress={280} accessibilityHint="Appui long pour réagir"
            style={({ pressed }) => [styles.commentBubble, { backgroundColor: c.glassStrong, borderColor: pinned ? c.primary : c.glassBorder }, depth(c, 1), pressed && { opacity: 0.85 }]}>
            {pinned && (
              <View style={styles.pinned}>
                <Icon name="pin" size={13} color={c.primary} />
                <T v="caption" color={c.primary} style={{ fontSize: 12 }}>Épinglé par l'auteur</T>
              </View>
            )}
            <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
              <T v="label" style={{ flex: 1 }} lines={1} onPress={() => router.push(`/user/${author.id}`)}>{displayName(author)}</T>
              <Muted style={{ fontSize: 12 }}>{fmt.relative(comment.createdAt)}</Muted>
            </View>
            <RichText text={comment.content} mentionIds={comment.mentionIds} style={{ marginTop: 2 }} />
          </Pressable>
          <View style={styles.commentActions}>
            <T v="label" color={c.textSecondary} style={{ fontSize: 12 }} onPress={() => startReply(comment, author)}>Répondre</T>
            <T v="label" color={mine && mine !== '❤️' ? c.primary : c.textSecondary} style={{ fontSize: 12 }} onPress={() => openPicker(null)}>
              {mine && mine !== '❤️' ? `${mine} Réagi` : 'Réagir'}
            </T>
            <View style={{ flex: 1 }}><ReactionBar reactions={others} me={me.id} onToggle={toggle} /></View>
            {(isAuthor && !reply) || comment.authorId !== me.id ? (
              <Pressable accessibilityRole="button" accessibilityLabel="Options du commentaire" hitSlop={10} onPress={menu}>
                <Icon name="dots-horizontal" size={18} color={c.textSecondary} />
              </Pressable>
            ) : null}
          </View>
        </View>
        <Pressable accessibilityRole="button" accessibilityLabel={mine === '❤️' ? "Je n'aime plus ce commentaire" : "J'aime ce commentaire"}
          hitSlop={10} onPress={() => toggle('❤️')} style={styles.heart}>
          <Icon name={mine === '❤️' ? 'heart' : 'heart-outline'} size={16} color={mine === '❤️' ? c.error : c.textSecondary} />
          {hearts > 0 && <Muted style={{ fontSize: 11 }}>{hearts}</Muted>}
        </Pressable>
      </View>
    );
  };

  const replyAuthor = replyTo ? authors.get(replyTo.authorId) : undefined;
  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <FlatList
        data={rows}
        keyExtractor={(x) => ('more' in x ? `more:${x.more}` : x.comment.id)}
        renderItem={renderRow}
        ListHeaderComponent={
          <View style={{ paddingTop: 16 }}>
            <PostCard post={post} author={ctx.users.get(post.authorId)} community={post.communityId ? ctx.communities.get(post.communityId) : undefined} detailed />
            <T v="h3" style={{ paddingHorizontal: 16, marginVertical: 8 }}>Commentaires</T>
          </View>
        }
        ListEmptyComponent={comments.isPending ? undefined : <Muted style={{ paddingHorizontal: 16, fontSize: 15 }}>Soyez le premier à commenter.</Muted>}
        contentContainerStyle={{ paddingBottom: 16 }}
        keyboardShouldPersistTaps="handled"
      />
      <MentionSuggestions text={draft} cursor={cursor} onPick={pickMention} />
      <View style={[styles.composer, { backgroundColor: c.surface, borderColor: c.border, paddingBottom: insets.bottom + 8 }]}>
        {!!error && <T v="caption" color={c.error} style={{ marginBottom: 6 }}>{error}</T>}
        {replyTo && (
          <View style={styles.replying}>
            <Muted style={{ flex: 1 }} lines={1}>{`Réponse à ${replyAuthor ? displayName(replyAuthor) : 'un commentaire'}`}</Muted>
            <Pressable accessibilityRole="button" accessibilityLabel="Annuler la réponse" hitSlop={10}
              onPress={() => { setReplyTo(null); setDraft(''); }}>
              <Icon name="close" size={18} color={c.textSecondary} />
            </Pressable>
          </View>
        )}
        <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: 10 }}>
          <Avatar initials={initials(me)} color={me.avatarColor} image={me.avatarImage} size={34} />
          <TextInput ref={input} value={draft} onChangeText={setDraft} placeholder={replyTo ? 'Écrire une réponse…' : 'Écrire un commentaire…'}
            placeholderTextColor={c.textSecondary} autoFocus={focus} multiline maxLength={1000} accessibilityLabel="Écrire un commentaire"
            onSelectionChange={(e) => setCursor(e.nativeEvent.selection.end)}
            style={[styles.input, { backgroundColor: c.surfaceVariant, color: c.text }]} />
          <IconButton name="send" label="Envoyer le commentaire" color={draft.trim() ? c.primary : c.textSecondary} onPress={send} />
        </View>
      </View>
      {picker.element}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  comment: { flexDirection: 'row', gap: 10, paddingHorizontal: 16, marginBottom: 10 },
  commentBubble: { borderRadius: 16, padding: 10, borderWidth: 1 },
  commentActions: { flexDirection: 'row', alignItems: 'center', gap: 14, paddingLeft: 10, marginTop: 4, minHeight: 26 },
  heart: { alignItems: 'center', paddingTop: 14, width: 24, gap: 2 },
  pinned: { flexDirection: 'row', alignItems: 'center', gap: 4, marginBottom: 4 },
  more: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingLeft: 60, paddingRight: 16, marginBottom: 10, minHeight: 28 },
  moreLine: { width: 24, height: StyleSheet.hairlineWidth * 2 },
  replying: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 6, paddingHorizontal: 4 },
  composer: { paddingHorizontal: 12, paddingTop: 8, borderTopWidth: StyleSheet.hairlineWidth },
  input: { flex: 1, minHeight: 40, maxHeight: 120, borderRadius: 20, paddingHorizontal: 14, paddingTop: 10, paddingBottom: 10, fontFamily: font.regular, fontSize: 15 },
});
