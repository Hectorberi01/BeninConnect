import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import { FlatList, KeyboardAvoidingView, Platform, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { MessageBubble } from '@/components/cards';
import { useReactionPicker } from '@/components/reactions';
import { Avatar, Button, EmptyState, Icon, IconButton, Muted, QueryView, Screen, T } from '@/components/ui';
import * as fmt from '@/core/format';
import { useMe } from '@/core/session';
import { font, useColors } from '@/core/theme';
import type { ConversationSummary, Message } from '@/data/types';
import { displayName, initials, location } from '@/data/types';
import { useConversation, useMarkConversationRead, useMessages, useReactToMessage, useSendMessage } from '@/features/queries';
import { DEMO_REPLIES } from '@/features/rules';

export default function Conversation() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useConversation(id);
  return <Screen><QueryView query={query}>{(summary) => <Chat summary={summary} />}</QueryView></Screen>;
}

type Row = { kind: 'day'; key: string; label: string } | { kind: 'message'; key: string; message: Message };

function Chat({ summary }: { summary: ConversationSummary }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const id = summary.conversation.id;
  const other = summary.otherParticipant;
  const messages = useMessages(id);
  const send = useSendMessage(id);
  const markRead = useMarkConversationRead();
  const react = useReactToMessage(id);
  const picker = useReactionPicker();
  const [draft, setDraft] = useState('');
  const replyIndex = useRef(0);
  const list = useRef<FlatList<Row>>(null);

  // Lu à l'ouverture et à chaque nouveau message reçu.
  const count = messages.data?.length ?? 0;
  useEffect(() => {
    if (count > 0) markRead.mutate(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, count]);

  const rows = useMemo<Row[]>(() => {
    const out: Row[] = [];
    let lastDay = '';
    for (const m of messages.data ?? []) {
      const day = new Date(m.sentAt).toDateString();
      if (day !== lastDay) out.push({ kind: 'day', key: `d${m.id}`, label: fmt.dayLabel(m.sentAt) });
      lastDay = day;
      out.push({ kind: 'message', key: m.id, message: m });
    }
    return out;
  }, [messages.data]);

  // Réagir au dernier message agrandit la liste : on garde le bas visible.
  const reactTo = (messageId: string, emoji: string) => {
    react.mutate({ messageId, emoji });
    if (messageId === messages.data?.at(-1)?.id) setTimeout(() => list.current?.scrollToEnd({ animated: true }), 80);
  };

  const submit = () => {
    const content = draft.trim();
    if (!content || send.isPending) return;
    send.mutate({ content }, { onSuccess: () => setDraft('') });
  };

  const simulate = () => {
    const content = DEMO_REPLIES[replyIndex.current++ % DEMO_REPLIES.length];
    send.mutate({ content, simulated: true });
  };

  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <View style={[styles.header, { paddingTop: insets.top + 6, backgroundColor: c.surface, borderColor: c.border }]}>
        <IconButton name="arrow-left" label="Retour" onPress={() => router.back()} />
        <Pressable accessibilityRole="button" accessibilityLabel={`Profil de ${displayName(other)}`} style={styles.who}
          onPress={() => router.push(`/user/${other.id}`)}>
          <Avatar initials={initials(other)} color={other.avatarColor} image={other.avatarImage} size={38} />
          <View style={{ flex: 1 }}>
            <T v="label" style={{ fontSize: 15 }} lines={1}>{displayName(other)}</T>
            <Muted lines={1}>{location(other)}</Muted>
          </View>
        </Pressable>
      </View>

      {messages.isPending ? <View style={{ flex: 1 }} /> : (
        <FlatList
          ref={list}
          data={rows}
          keyExtractor={(r) => r.key}
          renderItem={({ item }) => item.kind === 'day'
            ? <Muted style={styles.day}>{item.label}</Muted>
            : (
              <MessageBubble message={item.message} mine={item.message.senderId === me.id} me={me.id}
                onToggleReaction={(emoji) => reactTo(item.message.id, emoji)}
                onLongPress={(e) => picker.open(e, item.message.reactions.find((r) => r.userId === me.id)?.emoji ?? null,
                  (emoji) => reactTo(item.message.id, emoji))} />
            )}
          onContentSizeChange={() => list.current?.scrollToEnd({ animated: false })}
          ListEmptyComponent={<EmptyState icon="hand-wave-outline" title="Dites bonjour !"
            message={`Envoyez votre premier message à ${other.firstName}.`} />}
          contentContainerStyle={{ paddingTop: 12, paddingBottom: 22, flexGrow: 1 }}
          keyboardShouldPersistTaps="handled"
        />
      )}

      <View style={[styles.composer, { backgroundColor: c.surface, borderColor: c.border, paddingBottom: insets.bottom + 8 }]}>
        <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 4 }}>
          <Button small kind="ghost" icon="robot-outline" title="Simuler une réponse" onPress={simulate} disabled={send.isPending} />
          <Muted style={{ marginLeft: 'auto', fontSize: 12 }}>Appui long pour réagir</Muted>
        </View>
        <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: 8 }}>
          <TextInput value={draft} onChangeText={setDraft} placeholder="Votre message…" placeholderTextColor={c.textSecondary}
            multiline maxLength={2000} accessibilityLabel="Votre message"
            style={[styles.input, { backgroundColor: c.surfaceVariant, color: c.text }]} />
          <Pressable accessibilityRole="button" accessibilityLabel="Envoyer" onPress={submit}
            style={[styles.send, { backgroundColor: draft.trim() ? c.primary : c.border }]}>
            <Icon name="send" color="#FFFFFF" size={20} />
          </Pressable>
        </View>
      </View>
      {picker.element}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 8, paddingBottom: 8, borderBottomWidth: StyleSheet.hairlineWidth },
  who: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 10, marginLeft: 4 },
  day: { textAlign: 'center', marginVertical: 10, fontSize: 12 },
  composer: { paddingHorizontal: 12, paddingTop: 4, borderTopWidth: StyleSheet.hairlineWidth },
  input: { flex: 1, minHeight: 42, maxHeight: 120, borderRadius: 21, paddingHorizontal: 16, paddingVertical: 10, fontFamily: font.regular, fontSize: 15 },
  send: { width: 42, height: 42, borderRadius: 21, alignItems: 'center', justifyContent: 'center' },
});
