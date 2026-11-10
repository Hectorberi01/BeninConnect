import { useLocalSearchParams } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { SharedPostCard } from '@/components/social';
import { Avatar, Button, EmptyState, Header, Icon, Muted, QueryView, Screen, SearchBar, T } from '@/components/ui';
import { useMe } from '@/core/session';
import { font, radius, useColors } from '@/core/theme';
import { toast } from '@/core/toast';
import { matches } from '@/core/validation';
import type { User } from '@/data/types';
import { displayName, initials } from '@/data/types';
import { useBlockedUsers } from '@/features/block';
import { useConversations, useSearchUsers, useSendPostTo } from '@/features/queries';

/** Envoi d'une publication à des membres, en message privé (discussions récentes d'abord). */
export default function SendPost() {
  const c = useColors();
  const me = useMe();
  const { id } = useLocalSearchParams<{ id: string }>();
  const users = useSearchUsers('');
  const conversations = useConversations();
  const blocked = useBlockedUsers().data ?? [];
  const send = useSendPostTo();
  const [search, setSearch] = useState('');
  const [note, setNote] = useState('');
  const [sent, setSent] = useState<Set<string>>(new Set());
  const [busy, setBusy] = useState<string | null>(null);

  const people = useMemo(() => {
    const recent = (conversations.data ?? []).filter((s) => !s.isBlocked).map((s) => s.otherParticipant);
    const hidden = new Set([me.id, ...blocked.map((u) => u.id)]);
    const seen = new Set<string>();
    const q = search.trim();
    return [...recent, ...(users.data ?? [])].filter((u) => {
      if (hidden.has(u.id) || seen.has(u.id)) return false;
      seen.add(u.id);
      return !q || matches(displayName(u), q) || matches(u.city, q);
    });
  }, [conversations.data, users.data, blocked, search, me.id]);

  const sendTo = (u: User) => {
    if (busy || sent.has(u.id)) return;
    setBusy(u.id);
    send.mutate({ userId: u.id, postId: id, content: note.trim() || undefined }, {
      onSuccess: () => {
        setSent((s) => new Set(s).add(u.id));
        toast(`Envoyé à ${displayName(u)}.`, 'success');
      },
      onSettled: () => setBusy(null),
    });
  };

  return (
    <Screen>
      <Header title="Envoyer à un membre" />
      <View style={{ padding: 16, paddingBottom: 8, gap: 10 }}>
        <View style={{ alignItems: 'center' }}><SharedPostCard postId={id} mine={false} /></View>
        <TextInput value={note} onChangeText={setNote} placeholder="Ajouter un message (facultatif)" placeholderTextColor={c.textSecondary}
          maxLength={500} accessibilityLabel="Message accompagnant la publication"
          style={[styles.note, { backgroundColor: c.surfaceVariant, color: c.text }]} />
        <SearchBar value={search} onChangeText={setSearch} placeholder="Rechercher un membre" />
      </View>
      <QueryView query={users}>
        {() => (
          <FlatList data={people} keyExtractor={(u) => u.id} keyboardShouldPersistTaps="handled"
            renderItem={({ item }) => {
              const done = sent.has(item.id);
              return (
                <Pressable accessibilityRole="button" onPress={() => sendTo(item)} style={[styles.row, { borderColor: c.border }]}>
                  <Avatar initials={initials(item)} color={item.avatarColor} image={item.avatarImage} />
                  <View style={{ flex: 1 }}>
                    <T v="label" lines={1} style={{ fontSize: 15 }}>{displayName(item)}</T>
                    <Muted lines={1}>{[item.profession, item.city].filter(Boolean).join(' · ')}</Muted>
                  </View>
                  {done
                    ? <View style={styles.done}><Icon name="check" size={18} color={c.primary} /><Muted color={c.primary}>Envoyé</Muted></View>
                    : <Button small title="Envoyer" loading={busy === item.id} onPress={() => sendTo(item)} />}
                </Pressable>
              );
            }}
            ListEmptyComponent={<EmptyState icon="account-search-outline" title="Aucun membre trouvé" />} />
        )}
      </QueryView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  note: { minHeight: 42, borderRadius: radius.control, paddingHorizontal: 14, fontFamily: font.regular, fontSize: 15 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 16, paddingVertical: 10, borderBottomWidth: StyleSheet.hairlineWidth },
  done: { flexDirection: 'row', alignItems: 'center', gap: 4 },
});
