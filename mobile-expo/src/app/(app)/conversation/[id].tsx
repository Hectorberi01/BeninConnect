import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import * as DocumentPicker from 'expo-document-picker';
import { Alert, FlatList, KeyboardAvoidingView, Platform, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { MessageBubble } from '@/components/cards';
import { type PickerAction, useReactionPicker } from '@/components/reactions';
import { Avatar, EmptyState, Icon, IconButton, Muted, QueryView, Screen, T } from '@/components/ui';
import * as fmt from '@/core/format';
import { useMe } from '@/core/session';
import { font, useColors } from '@/core/theme';
import type { ConversationSummary, Message, OutgoingFile } from '@/data/types';
import { displayName, initials, location } from '@/data/types';
import { confirm } from '@/core/dialog';
import { pickPhoto } from '@/core/photos';
import { toast } from '@/core/toast';
import {
  useConversation, useDeleteMessage, useEditMessage, useMarkConversationRead, useMessages, useReactToMessage, useSendAttachment, useSendMessage,
} from '@/features/queries';
import { useVoiceRecorder } from '@/features/voice';

export default function Conversation() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useConversation(id);
  return <Screen><QueryView query={query}>{(summary) => <Chat summary={summary} />}</QueryView></Screen>;
}

/** Même délai que l'API (Message.EditWindow). */
const EDIT_WINDOW_MS = 15 * 60_000;
const MAX_ATTACHMENT_BYTES = 20 * 1024 * 1024;

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
  const edit = useEditMessage(id);
  const remove = useDeleteMessage(id);
  const [editing, setEditing] = useState<Message | null>(null);
  const attach = useSendAttachment(id);
  const sendFile = (file: OutgoingFile) => attach.mutate({ file });
  const voice = useVoiceRecorder(sendFile);
  const picker = useReactionPicker();
  const [draft, setDraft] = useState('');
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
    if (!content || send.isPending || edit.isPending) return;
    if (editing) {
      if (content === editing.content) return cancelEdit();
      edit.mutate({ messageId: editing.id, content }, { onSuccess: cancelEdit });
      return;
    }
    send.mutate({ content }, { onSuccess: () => setDraft('') });
  };

  // Pièces jointes : photo (galerie, appareil photo) ou document PDF, 20 Mo au plus
  const pickDocument = async () => {
    const result = await DocumentPicker.getDocumentAsync({ type: 'application/pdf', copyToCacheDirectory: true });
    const asset = result.canceled ? null : result.assets[0];
    if (!asset) return;
    if ((asset.size ?? 0) > MAX_ATTACHMENT_BYTES) return toast('Le fichier ne doit pas dépasser 20 Mo.', 'error');
    sendFile({ uri: asset.uri, name: asset.name, mimeType: asset.mimeType ?? 'application/pdf' });
  };
  const pickImage = async (source: 'library' | 'camera') => {
    const photo = await pickPhoto(source);
    if (photo) sendFile({ uri: photo.uri, name: photo.fileName ?? 'photo.jpg', mimeType: photo.mimeType ?? 'image/jpeg' });
  };
  const openAttachMenu = () => Alert.alert('Envoyer', undefined, [
    { text: 'Photo de la galerie', onPress: () => pickImage('library') },
    { text: 'Prendre une photo', onPress: () => pickImage('camera') },
    { text: 'Document PDF', onPress: pickDocument },
    { text: 'Annuler', style: 'cancel' },
  ]);

  const startVoice = () => voice.start().catch(() => toast("Impossible de démarrer l'enregistrement.", 'error'));
  const finishVoice = async (send: boolean) => {
    const file = await voice.stop().catch(() => null);
    if (send && file) sendFile(file);
    else if (send) toast('Message vocal trop court.', 'info');
  };

  const startEdit = (message: Message) => {
    setEditing(message);
    setDraft(message.content);
  };
  const cancelEdit = () => {
    setEditing(null);
    setDraft('');
  };
  const askDelete = async (message: Message) => {
    if (await confirm('Supprimer le message ?', `Il sera remplacé par « Message supprimé » pour vous et ${other.firstName}.`, 'Supprimer', true)) {
      remove.mutate(message.id);
      if (editing?.id === message.id) cancelEdit();
    }
  };

  // Appui long : réactions, et pour ses messages Modifier (15 min) / Supprimer.
  const actionsFor = (message: Message): PickerAction[] => {
    if (message.senderId !== me.id) return [];
    const editable = !message.attachment && Date.now() - Date.parse(message.sentAt) < EDIT_WINDOW_MS;
    return [
      ...(editable ? [{ label: 'Modifier', icon: 'pencil-outline' as const, onPress: () => startEdit(message) }] : []),
      { label: 'Supprimer', icon: 'delete-outline' as const, destructive: true, onPress: () => askDelete(message) },
    ];
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
                  (emoji) => reactTo(item.message.id, emoji), actionsFor(item.message))} />
            )}
          onContentSizeChange={() => list.current?.scrollToEnd({ animated: false })}
          ListEmptyComponent={<EmptyState icon="hand-wave-outline" title="Dites bonjour !"
            message={`Envoyez votre premier message à ${other.firstName}.`} />}
          contentContainerStyle={{ paddingTop: 12, paddingBottom: 22, flexGrow: 1 }}
          keyboardShouldPersistTaps="handled"
        />
      )}

      <View style={[styles.composer, { backgroundColor: c.surface, borderColor: c.border, paddingBottom: insets.bottom + 8 }]}>
        {editing ? (
          <View style={[styles.editing, { borderColor: c.primary }]}>
            <Icon name="pencil-outline" size={18} color={c.primary} />
            <View style={{ flex: 1 }}>
              <T v="label" color={c.primary} style={{ fontSize: 13 }}>Modifier le message</T>
              <Muted lines={1}>{editing.content}</Muted>
            </View>
            <IconButton name="close" label="Annuler la modification" onPress={cancelEdit} />
          </View>
        ) : (
          <Muted style={{ textAlign: 'right', fontSize: 12, marginBottom: 6 }} color={attach.isPending ? c.primary : undefined}>
            {attach.isPending ? 'Envoi du fichier…' : 'Appui long sur un message pour réagir'}
          </Muted>
        )}
        {voice.recording ? (
          <View style={styles.row}>
            <Pressable accessibilityRole="button" accessibilityLabel="Annuler l'enregistrement" onPress={() => finishVoice(false)}
              style={[styles.send, { backgroundColor: c.surfaceVariant }]}>
              <Icon name="delete-outline" color={c.error} size={22} />
            </Pressable>
            <View style={[styles.input, styles.recording, { backgroundColor: c.surfaceVariant }]}>
              <View style={[styles.dot, { backgroundColor: c.error }]} />
              <T style={{ flex: 1 }}>{`Enregistrement… ${fmt.duration(voice.durationMs)}`}</T>
            </View>
            <Pressable accessibilityRole="button" accessibilityLabel="Envoyer le message vocal" onPress={() => finishVoice(true)}
              style={[styles.send, { backgroundColor: c.primary }]}>
              <Icon name="send" color="#FFFFFF" size={20} />
            </Pressable>
          </View>
        ) : (
          <View style={styles.row}>
            {!editing && (
              <Pressable accessibilityRole="button" accessibilityLabel="Joindre une photo ou un document" onPress={openAttachMenu}
                disabled={attach.isPending} hitSlop={6} style={styles.tool}>
                <Icon name="paperclip" color={c.textSecondary} size={24} />
              </Pressable>
            )}
            <TextInput value={draft} onChangeText={setDraft} placeholder="Votre message…" placeholderTextColor={c.textSecondary}
              multiline maxLength={2000} accessibilityLabel="Votre message"
              style={[styles.input, { backgroundColor: c.surfaceVariant, color: c.text }]} />
            {draft.trim() || editing ? (
              <Pressable accessibilityRole="button" accessibilityLabel={editing ? 'Enregistrer la modification' : 'Envoyer'} onPress={submit}
                style={[styles.send, { backgroundColor: draft.trim() ? c.primary : c.border }]}>
                <Icon name={editing ? 'check' : 'send'} color="#FFFFFF" size={20} />
              </Pressable>
            ) : (
              <Pressable accessibilityRole="button" accessibilityLabel="Enregistrer un message vocal" onPress={startVoice}
                disabled={attach.isPending} style={[styles.send, { backgroundColor: c.primary }]}>
                <Icon name="microphone" color="#FFFFFF" size={22} />
              </Pressable>
            )}
          </View>
        )}
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
  row: { flexDirection: 'row', alignItems: 'flex-end', gap: 8 },
  tool: { width: 34, height: 42, alignItems: 'center', justifyContent: 'center' },
  recording: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  dot: { width: 10, height: 10, borderRadius: 5 },
  editing: { flexDirection: 'row', alignItems: 'center', gap: 10, borderLeftWidth: 3, paddingLeft: 10, marginBottom: 8 },
});
