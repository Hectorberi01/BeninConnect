import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { FlatList, KeyboardAvoidingView, Platform, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { PostCard, usePostContext } from '@/components/cards';
import { ReactionBar, useReactionPicker } from '@/components/reactions';
import { Avatar, Header, IconButton, Muted, QueryView, Screen, T } from '@/components/ui';
import * as fmt from '@/core/format';
import { useMe } from '@/core/session';
import { depth, font, useColors } from '@/core/theme';
import type { Comment, Post } from '@/data/types';
import { displayName, errorMessage, initials } from '@/data/types';
import { reportContent } from '@/features/report';
import { useAddComment, useComments, usePost, useReactToComment, useUsersMap } from '@/features/queries';

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

function Details({ post, focus }: { post: Post; focus: boolean }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const ctx = usePostContext([post]);
  const comments = useComments(post.id);
  const authors = useUsersMap((comments.data ?? []).map((x) => x.authorId));
  const add = useAddComment(post.id);
  const react = useReactToComment(post.id);
  const picker = useReactionPicker();
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);

  const send = () => {
    if (!draft.trim() || add.isPending) return;
    setError(null);
    add.mutate(draft, { onSuccess: () => setDraft(''), onError: (e) => setError(errorMessage(e)) });
  };

  const renderComment = ({ item }: { item: Comment }) => {
    const author = authors.get(item.authorId);
    if (!author) return null;
    const mine = item.reactions.find((r) => r.userId === me.id)?.emoji ?? null;
    const toggle = (emoji: string) => react.mutate({ commentId: item.id, emoji });
    const openPicker = (e: Parameters<typeof picker.open>[0]) => picker.open(e, mine, toggle);
    return (
      <View style={styles.comment}>
        <Avatar initials={initials(author)} color={author.avatarColor} image={author.avatarImage} size={34} onPress={() => router.push(`/user/${author.id}`)} />
        <View style={{ flex: 1 }}>
          <Pressable onLongPress={openPicker} delayLongPress={280} accessibilityHint="Appui long pour réagir"
            style={({ pressed }) => [styles.commentBubble, { backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 1), pressed && { opacity: 0.85 }]}>
            <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
              <T v="label" style={{ flex: 1 }} lines={1} onPress={() => router.push(`/user/${author.id}`)}>{displayName(author)}</T>
              <Muted style={{ fontSize: 12 }}>{fmt.relative(item.createdAt)}</Muted>
            </View>
            <T style={{ marginTop: 2 }}>{item.content}</T>
          </Pressable>
          <View style={styles.commentActions}>
            <T v="label" color={mine ? c.primary : c.textSecondary} style={{ fontSize: 12 }} onPress={() => openPicker(null)}>
              {mine ? `${mine} Réagi` : 'Réagir'}
            </T>
            <View style={{ flex: 1 }}><ReactionBar reactions={item.reactions} me={me.id} onToggle={toggle} /></View>
            {item.authorId !== me.id && (
              <T v="label" color={c.textSecondary} style={{ fontSize: 12 }} onPress={() => reportContent('Comment', item.id)}>Signaler</T>
            )}
          </View>
        </View>
      </View>
    );
  };

  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <FlatList
        data={comments.data ?? []}
        keyExtractor={(x) => x.id}
        renderItem={renderComment}
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
      <View style={[styles.composer, { backgroundColor: c.surface, borderColor: c.border, paddingBottom: insets.bottom + 8 }]}>
        {!!error && <T v="caption" color={c.error} style={{ marginBottom: 6 }}>{error}</T>}
        <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: 10 }}>
          <Avatar initials={initials(me)} color={me.avatarColor} image={me.avatarImage} size={34} />
          <TextInput value={draft} onChangeText={setDraft} placeholder="Écrire un commentaire…" placeholderTextColor={c.textSecondary}
            autoFocus={focus} multiline maxLength={1000} accessibilityLabel="Écrire un commentaire"
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
  commentActions: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingLeft: 10, marginTop: 4, minHeight: 26 },
  composer: { paddingHorizontal: 12, paddingTop: 8, borderTopWidth: StyleSheet.hairlineWidth },
  input: { flex: 1, minHeight: 40, maxHeight: 120, borderRadius: 20, paddingHorizontal: 14, paddingTop: 10, paddingBottom: 10, fontFamily: font.regular, fontSize: 15 },
});
