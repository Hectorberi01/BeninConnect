import { router } from 'expo-router';
import { type ComponentRef, type ReactElement, useEffect, useRef, useState } from 'react';
import { Alert, FlatList, Keyboard, KeyboardAvoidingView, Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ReactionBar, useReactionPicker } from '@/components/reactions';
import { MentionSuggestions, RichText } from '@/components/social';
import { Avatar, Icon, Muted, T } from '@/components/ui';
import * as fmt from '@/core/format';
import { useMe } from '@/core/session';
import { font, useColors } from '@/core/theme';
import { insertMention, keptMentions } from '@/data/social';
import type { Comment, Post, User } from '@/data/types';
import { displayName, errorMessage, initials } from '@/data/types';
import { blockUser } from '@/features/block';
import { reportContent } from '@/features/report';
import { useAddComment, useComments, usePinComment, useReactToComment, useUsersMap } from '@/features/queries';

const QUICK_EMOJIS = ['❤️', '🙌', '🔥', '👏', '😢', '😍', '😮', '😂'];

/** Clavier ouvert : la marge du bas (zone d'accueil iOS) devient inutile. */
export function useKeyboardOpen() {
  const [open, setOpen] = useState(false);
  useEffect(() => {
    const ios = Platform.OS === 'ios';
    const show = Keyboard.addListener(ios ? 'keyboardWillShow' : 'keyboardDidShow', () => setOpen(true));
    const hide = Keyboard.addListener(ios ? 'keyboardWillHide' : 'keyboardDidHide', () => setOpen(false));
    return () => { show.remove(); hide.remove(); };
  }, []);
  return open;
}

type Row = { comment: Comment; reply: boolean } | { more: string; count: number; open: boolean };

/**
 * Liste des commentaires et zone de saisie, façon Instagram.
 * header : contenu au-dessus (la publication sur l'écran de détail). sheet : affiché dans la fenêtre des commentaires.
 */
export function CommentsPanel({ post, header, focus, sheet = false }: {
  post: Post; header?: ReactElement; focus?: string; sheet?: boolean;
}) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const comments = useComments(post.id);
  const authors = useUsersMap((comments.data ?? []).map((x) => x.authorId));
  const add = useAddComment(post.id);
  const react = useReactToComment(post.id);
  const pinComment = usePinComment(post.id);
  const picker = useReactionPicker();
  const input = useRef<ComponentRef<typeof TextInput>>(null);
  const list = useRef<FlatList<Row>>(null);
  const headY = useRef<number | null>(null);
  const scrolledFor = useRef<string | undefined>(undefined);
  // focus (identifiant d'appui sur l'icône commentaire) : on amène la liste aux commentaires, sans ouvrir le clavier
  const scrollToComments = () => {
    if (!focus || headY.current == null || scrolledFor.current === focus) return;
    scrolledFor.current = focus;
    const y = headY.current;
    setTimeout(() => list.current?.scrollToOffset({ offset: y, animated: true }), 150);
  };
  useEffect(scrollToComments, [focus]); // eslint-disable-line react-hooks/exhaustive-deps
  const [draft, setDraft] = useState('');
  const [cursor, setCursor] = useState(0);
  const [chosen, setChosen] = useState<User[]>([]);
  const [replyTo, setReplyTo] = useState<Comment | null>(null);
  const [opened, setOpened] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const isAuthor = post.authorId === me.id;
  const keyboardOpen = useKeyboardOpen();

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

  const insertEmoji = (emoji: string) => {
    setDraft((d) => d.slice(0, cursor) + emoji + d.slice(cursor));
    setCursor((x) => x + emoji.length);
    input.current?.focus();
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
            {item.open ? 'Masquer les réponses' : item.count === 1 ? 'Voir 1 autre réponse' : `Voir les ${item.count} autres réponses`}
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
    const size = reply ? 28 : 36;
    return (
      <Pressable onLongPress={openPicker} delayLongPress={280} accessibilityHint="Appui long pour réagir avec un émoji"
        style={({ pressed }) => [styles.comment, reply && styles.reply, pressed && { backgroundColor: c.surfaceVariant }]}>
        <Avatar initials={initials(author)} color={author.avatarColor} image={author.avatarImage} size={size} onPress={() => router.push(`/user/${author.id}`)} />
        <View style={{ flex: 1 }}>
          {pinned && (
            <View style={styles.pinned}>
              <Icon name="pin" size={12} color={c.textSecondary} />
              <Muted style={{ fontSize: 12 }}>Épinglé par l'auteur</Muted>
            </View>
          )}
          <Text numberOfLines={1} style={[styles.meta, { color: c.textSecondary }]}>
            <Text style={{ fontFamily: font.semibold, color: c.text }} onPress={() => router.push(`/user/${author.id}`)}>{displayName(author)}</Text>
            {`  ${fmt.compact(comment.createdAt)}`}
            {comment.authorId === post.authorId ? ' · Auteur' : ''}
          </Text>
          <RichText text={comment.content} mentionIds={comment.mentionIds} style={{ marginTop: 1 }} />
          <View style={styles.commentActions}>
            <T v="label" color={c.textSecondary} onPress={() => startReply(comment, author)}>Répondre</T>
            {others.length > 0 && <View style={{ flexShrink: 1 }}><ReactionBar reactions={others} me={me.id} onToggle={toggle} /></View>}
            <View style={{ flex: 1 }} />
            {(isAuthor && !reply) || comment.authorId !== me.id ? (
              <Pressable accessibilityRole="button" accessibilityLabel="Options du commentaire" hitSlop={10} onPress={menu}>
                <Icon name="dots-horizontal" size={18} color={c.textSecondary} />
              </Pressable>
            ) : null}
          </View>
        </View>
        <Pressable accessibilityRole="button" accessibilityLabel={mine === '❤️' ? "Je n'aime plus ce commentaire" : "J'aime ce commentaire"}
          hitSlop={12} onPress={() => toggle('❤️')} style={styles.heart}>
          <Icon name={mine === '❤️' ? 'heart' : 'heart-outline'} size={18} color={mine === '❤️' ? c.error : c.textSecondary} />
          {hearts > 0 && <Muted style={{ fontSize: 12 }}>{hearts}</Muted>}
        </Pressable>
      </Pressable>
    );
  };

  const replyAuthor = replyTo ? authors.get(replyTo.authorId) : undefined;
  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <FlatList
        ref={list}
        data={rows}
        keyExtractor={(x) => ('more' in x ? `more:${x.more}` : x.comment.id)}
        renderItem={renderRow}
        ListHeaderComponent={
          <View style={{ paddingTop: sheet ? 4 : 16 }}>
            {header}
            {!sheet && <View style={[styles.sectionHead, { borderColor: c.border }]} onLayout={(e) => { headY.current = e.nativeEvent.layout.y; scrollToComments(); }}>
              <T v="label" style={{ fontSize: 15 }}>Commentaires</T>
              {all.length > 0 && <Muted style={{ fontSize: 13 }}>{all.length}</Muted>}
            </View>}
          </View>
        }
        ListEmptyComponent={comments.isPending ? undefined : (
          <View style={sheet ? styles.emptySheet : undefined}>
            {sheet && <T v="h3" style={{ textAlign: 'center' }}>Aucun commentaire pour l’instant</T>}
            <Muted style={sheet ? { textAlign: 'center', marginTop: 6 } : { paddingHorizontal: 16, fontSize: 15 }}>
              {sheet ? 'Lancez la conversation.' : 'Soyez le premier à commenter.'}
            </Muted>
          </View>
        )}
        contentContainerStyle={{ paddingBottom: 16 }}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
      />
      <MentionSuggestions text={draft} cursor={cursor} onPick={pickMention} />
      <View style={[styles.composer, { backgroundColor: c.surface, borderColor: c.border, paddingBottom: keyboardOpen ? 8 : insets.bottom + 8 }]}>
        <View style={styles.emojis}>
          {QUICK_EMOJIS.map((e) => (
            <Pressable key={e} accessibilityRole="button" accessibilityLabel={`Ajouter ${e}`} hitSlop={6} onPress={() => insertEmoji(e)}
              style={({ pressed }) => pressed && { transform: [{ scale: 1.25 }] }}>
              <Text style={styles.emoji}>{e}</Text>
            </Pressable>
          ))}
        </View>
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
          <Avatar initials={initials(me)} color={me.avatarColor} image={me.avatarImage} size={36} />
          <View style={[styles.field, { borderColor: c.border }]}>
            <TextInput ref={input} value={draft} onChangeText={setDraft} placeholder={replyTo ? 'Écrire une réponse…' : 'Ajouter un commentaire…'}
              placeholderTextColor={c.textSecondary} multiline maxLength={1000} accessibilityLabel="Écrire un commentaire"
              onSelectionChange={(e) => setCursor(e.nativeEvent.selection.end)}
              style={[styles.input, { color: c.text }]} />
            {!!draft.trim() && (
              <Pressable accessibilityRole="button" accessibilityLabel="Publier le commentaire" hitSlop={8} onPress={send} disabled={add.isPending}
                style={styles.publish}>
                <T v="label" color={c.primary} style={{ fontSize: 15, opacity: add.isPending ? 0.5 : 1 }}>Publier</T>
              </Pressable>
            )}
          </View>
        </View>
      </View>
      {picker.element}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  emptySheet: { alignItems: 'center', paddingHorizontal: 32, paddingVertical: 48 },
  sectionHead: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 16, paddingTop: 14, paddingBottom: 10, marginBottom: 4, borderTopWidth: StyleSheet.hairlineWidth },
  comment: { flexDirection: 'row', alignItems: 'flex-start', gap: 12, paddingHorizontal: 16, paddingVertical: 8 },
  reply: { paddingLeft: 64 },
  meta: { fontFamily: font.regular, fontSize: 13 },
  commentActions: { flexDirection: 'row', alignItems: 'center', gap: 16, marginTop: 6, minHeight: 22 },
  heart: { alignItems: 'center', paddingTop: 10, width: 28, gap: 2 },
  pinned: { flexDirection: 'row', alignItems: 'center', gap: 4, marginBottom: 2 },
  more: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingLeft: 64, paddingRight: 16, paddingVertical: 6, minHeight: 28 },
  moreLine: { width: 24, height: StyleSheet.hairlineWidth * 2 },
  replying: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 6, paddingHorizontal: 4 },
  composer: { paddingHorizontal: 12, paddingTop: 6, borderTopWidth: StyleSheet.hairlineWidth },
  emojis: { flexDirection: 'row', justifyContent: 'space-between', paddingHorizontal: 6, paddingVertical: 8 },
  emoji: { fontSize: 26 },
  field: { flex: 1, flexDirection: 'row', alignItems: 'flex-end', minHeight: 42, borderRadius: 21, borderWidth: 1, paddingLeft: 14, paddingRight: 6 },
  input: { flex: 1, maxHeight: 120, paddingTop: 11, paddingBottom: 11, fontFamily: font.regular, fontSize: 15 },
  publish: { paddingHorizontal: 8, paddingVertical: 11 },
});
