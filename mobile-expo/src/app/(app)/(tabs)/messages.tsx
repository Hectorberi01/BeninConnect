import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, View } from 'react-native';
import { Avatar, Badge, EmptyState, Header, IconButton, Muted, QueryView, Screen, SearchBar, T } from '@/components/ui';
import { useTabBarSpace } from '@/components/glass';
import * as fmt from '@/core/format';
import { useMe } from '@/core/session';
import { font, useColors } from '@/core/theme';
import { matches } from '@/core/validation';
import type { ConversationSummary } from '@/data/types';
import { displayName, initials } from '@/data/types';
import { useConversations } from '@/features/queries';

export default function Messages() {
  const c = useColors();
  const me = useMe();
  const query = useConversations();
  const [search, setSearch] = useState('');
  const bottom = useTabBarSpace();
  const preview = (s: ConversationSummary) =>
    !s.lastMessage ? 'Nouvelle conversation' : s.lastMessage.senderId === me.id ? `Vous : ${s.lastMessage.content}` : s.lastMessage.content;

  const items = useMemo(() => {
    const q = search.trim();
    return (query.data ?? []).filter((s) => !q || matches(displayName(s.otherParticipant), q) || matches(preview(s), q));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.data, search]);

  return (
    <Screen>
      <Header title="Messages" back={false}
        right={<IconButton name="square-edit-outline" label="Nouvelle conversation" color={c.primary} onPress={() => router.push('/conversation/new')} />} />
      <View style={{ padding: 16, paddingBottom: 8 }}>
        <SearchBar value={search} onChangeText={setSearch} placeholder="Rechercher une conversation" />
      </View>
      <QueryView query={query} loadingLabel="Chargement des conversations…">
        {() => (
          <FlatList
            data={items}
            keyExtractor={(s) => s.conversation.id}
            renderItem={({ item: s }) => (
              <Pressable accessibilityRole="button" onPress={() => router.push(`/conversation/${s.conversation.id}`)}
                style={({ pressed }) => [styles.row, { borderColor: c.border }, pressed && { opacity: 0.7 }]}>
                <Avatar initials={initials(s.otherParticipant)} color={s.otherParticipant.avatarColor} image={s.otherParticipant.avatarImage} size={50} />
                <View style={{ flex: 1, gap: 2 }}>
                  <View style={styles.line}>
                    <T v="label" style={{ fontSize: 15, flex: 1 }} lines={1}>{displayName(s.otherParticipant)}</T>
                    <Muted color={s.unreadCount ? c.primary : undefined}>{fmt.chat(s.lastMessage?.sentAt ?? s.conversation.createdAt)}</Muted>
                  </View>
                  <View style={styles.line}>
                    <T v="caption" lines={1} style={{ flex: 1, fontFamily: s.unreadCount ? font.semibold : font.regular }}
                      color={s.unreadCount ? c.text : c.textSecondary}>{preview(s)}</T>
                    {s.unreadCount > 0 && <Badge count={s.unreadCount} style={{ backgroundColor: c.primary }} />}
                  </View>
                </View>
              </Pressable>
            )}
            ListEmptyComponent={search
              ? <EmptyState icon="magnify" title="Aucune conversation trouvée" />
              : <EmptyState icon="message-outline" title="Aucune conversation" message="Écrivez à un membre de la communauté pour commencer."
                action="Nouvelle conversation" onAction={() => router.push('/conversation/new')} />}
            contentContainerStyle={{ paddingBottom: bottom }}
            refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
          />
        )}
      </QueryView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: StyleSheet.hairlineWidth },
  line: { flexDirection: 'row', alignItems: 'center', gap: 8 },
});
