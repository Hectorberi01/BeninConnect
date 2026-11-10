import { router } from 'expo-router';
import { useState } from 'react';
import { FlatList, RefreshControl } from 'react-native';
import { NotificationItem } from '@/components/cards';
import { Button, ChipRow, EmptyState, Header, QueryView, Screen } from '@/components/ui';
import { View } from 'react-native';
import { useColors } from '@/core/theme';
import { confirm } from '@/core/dialog';
import { toast } from '@/core/toast';
import type { AppNotification, NotificationCategory } from '@/data/types';
import { useNotificationActions, useNotifications, useUnreadNotifications } from '@/features/queries';
import { notificationHref } from '@/features/rules';

const CATEGORIES: { key: string; label: string }[] = [
  { key: '*', label: 'Toutes' }, { key: 'Social', label: 'Social' }, { key: 'Communities', label: 'Communautés' },
  { key: 'Messages', label: 'Messages' }, { key: 'Events', label: 'Événements' }, { key: 'System', label: 'Système' },
];

export default function Notifications() {
  const c = useColors();
  const [category, setCategory] = useState('*');
  const query = useNotifications(category === '*' ? undefined : (category as NotificationCategory));
  const unread = useUnreadNotifications().data ?? 0;
  const actions = useNotificationActions();

  const open = async (n: AppNotification) => {
    if (!n.isRead) await actions.markRead(n.id);
    const href = notificationHref(n);
    if (href) router.push(href);
    else toast(n.body);
  };

  const label = CATEGORIES.find((x) => x.key === category)?.label.toLowerCase();
  const clear = async () => {
    const scope = category === '*' ? 'toutes vos notifications' : `les notifications « ${label} »`;
    if (await confirm('Tout effacer ?', `Supprimer ${scope} ? Cette action est définitive.`, 'Tout effacer', true)) {
      await actions.clear(category === '*' ? undefined : (category as NotificationCategory));
    }
  };

  return (
    <Screen>
      <Header title="Notifications" subtitle={unread > 0 ? `${unread} non lue${unread > 1 ? 's' : ''}` : 'Tout est lu'}
        right={
          <View style={{ flexDirection: 'row' }}>
            <Button small kind="ghost" title="Tout lire" icon="check-all" disabled={unread === 0} onPress={actions.markAll} />
            <Button small kind="ghost" title="Effacer" icon="trash-can-outline" disabled={!query.data?.length} onPress={clear} />
          </View>
        } />
      <ChipRow items={CATEGORIES} selected={category} onSelect={setCategory} />
      <QueryView query={query} loadingLabel="Chargement des notifications…">
        {(items) => (
          <FlatList data={items} keyExtractor={(n) => n.id} renderItem={({ item }) => <NotificationItem item={item} onPress={() => open(item)} onDelete={() => actions.remove(item.id)} />}
            ListEmptyComponent={<EmptyState icon="bell-sleep-outline" title="Aucune notification" message="Vous serez prévenu ici de l'activité de vos communautés." />}
            contentContainerStyle={{ paddingVertical: 12 }}
            refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />} />
        )}
      </QueryView>
    </Screen>
  );
}
