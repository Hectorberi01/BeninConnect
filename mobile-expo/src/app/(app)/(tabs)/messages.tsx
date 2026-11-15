import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, View } from 'react-native';
import { Avatar, Badge, EmptyState, Header, Icon, IconButton, Muted, QueryView, Screen, SearchBar, T } from '@/components/ui';
import { OfficialBadge } from '@/components/social';
import { useTabBarSpace } from '@/components/glass';
import * as fmt from '@/core/format';
import { useMe } from '@/core/session';
import { font, useColors } from '@/core/theme';
import { matches } from '@/core/validation';
import type { ConversationSummary } from '@/data/types';
import { displayName, initials, messagePreview } from '@/data/types';
import { confirm } from '@/core/dialog';
import { useConversations, useDeleteConversation, useOpenSupport } from '@/features/queries';
import { usePullRefresh } from '@/core/refresh';

export default function Messages() {
  const c = useColors();
  const me = useMe();
  const query = useConversations();
  const pull = usePullRefresh(() => query.refetch());
  const [search, setSearch] = useState('');
  const bottom = useTabBarSpace();
  const remove = useDeleteConversation();
  const askDelete = async (s: ConversationSummary) => {
    if (await confirm('Supprimer la discussion ?',
      `Elle disparaît de votre liste avec ses messages. ${s.otherParticipant.firstName} garde son historique ; un nouveau message la fera réapparaître.`,
      'Supprimer', true)) remove.mutate(s.conversation.id);
  };
  const preview = (s: ConversationSummary) =>
    !s.lastMessage ? 'Nouvelle conversation'
      : s.lastMessage.senderId === me.id && !s.lastMessage.isDeleted ? `Vous : ${messagePreview(s.lastMessage)}` : messagePreview(s.lastMessage);

  // Support (compte officiel) : toujours épinglé en tête, même avant le premier message
  const support = query.data?.find((s) => s.otherParticipant.isOfficial);
  const openSupport = useOpenSupport();
  const goSupport = () => (support
    ? router.push(`/conversation/${support.conversation.id}`)
    : openSupport.mutate(undefined, { onSuccess: (conversation) => router.push(`/conversation/${conversation.id}`) }));
  const showSupport = !search.trim() || matches('BéninConnect support aide', search.trim()) || (!!support && matches(preview(support), search.trim()));

  const items = useMemo(() => {
    const q = search.trim();
    return (query.data ?? []).filter((s) => !s.otherParticipant.isOfficial)
      .filter((s) => !q || matches(displayName(s.otherParticipant), q) || matches(preview(s), q));
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
            keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag"
            data={items}
            keyExtractor={(s) => s.conversation.id}
            renderItem={({ item: s }) => (
              <Pressable accessibilityRole="button" onPress={() => router.push(`/conversation/${s.conversation.id}`)}
                onLongPress={() => askDelete(s)} delayLongPress={350} accessibilityHint="Appui long pour supprimer la discussion"
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
            ListHeaderComponent={showSupport ? (
              <Pressable accessibilityRole="button" accessibilityLabel="Écrire à l'équipe BéninConnect" onPress={goSupport} disabled={openSupport.isPending}
                style={({ pressed }) => [styles.row, { borderColor: c.border, backgroundColor: c.primaryLight }, pressed && { opacity: 0.7 }]}>
                <Avatar initials="BC" color="#087F5B" image={support?.otherParticipant.avatarImage} size={50} />
                <View style={{ flex: 1, gap: 2 }}>
                  <View style={styles.line}>
                    <View style={[styles.line, { flex: 1, gap: 4 }]}>
                      <T v="label" style={{ fontSize: 15 }} lines={1}>BéninConnect</T>
                      <OfficialBadge size={15} />
                    </View>
                    {openSupport.isPending ? <ActivityIndicator size="small" color={c.primary} />
                      : support?.lastMessage ? <Muted color={support.unreadCount ? c.primary : undefined}>{fmt.chat(support.lastMessage.sentAt)}</Muted>
                        : <Icon name="pin" size={16} color={c.primary} />}
                  </View>
                  <View style={styles.line}>
                    <T v="caption" lines={1} style={{ flex: 1, fontFamily: support?.unreadCount ? font.semibold : font.regular }}
                      color={support?.unreadCount ? c.text : c.textSecondary}>
                      {support?.lastMessage ? preview(support) : 'Une question, une idée, un souci ? Écrivez-nous.'}
                    </T>
                    {!!support?.unreadCount && <Badge count={support.unreadCount} style={{ backgroundColor: c.primary }} />}
                  </View>
                </View>
              </Pressable>
            ) : undefined}
            ListEmptyComponent={search
              ? <EmptyState icon="magnify" title="Aucune conversation trouvée" />
              : <EmptyState icon="message-outline" title="Aucune conversation" message="Écrivez à un membre de la communauté pour commencer."
                action="Nouvelle conversation" onAction={() => router.push('/conversation/new')} />}
            contentContainerStyle={{ paddingBottom: bottom }}
            refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.onRefresh} tintColor={c.primary} />}
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
