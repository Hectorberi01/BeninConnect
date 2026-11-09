import { FlatList, StyleSheet, View } from 'react-native';
import { Avatar, Button, EmptyState, Header, Muted, QueryView, Screen, T } from '@/components/ui';
import { useColors } from '@/core/theme';
import { displayName, initials, location } from '@/data/types';
import { unblockUser, useBlockedUsers } from '@/features/block';

/** Paramètres › Utilisateurs bloqués. */
export default function BlockedUsers() {
  const c = useColors();
  const query = useBlockedUsers();
  return (
    <Screen>
      <Header title="Utilisateurs bloqués" />
      <QueryView query={query}>
        {(users) => (
          <FlatList
            data={users}
            keyExtractor={(u) => u.id}
            contentContainerStyle={{ padding: 16, flexGrow: 1 }}
            ListHeaderComponent={users.length > 0
              ? <Muted style={{ marginBottom: 12 }}>Vous ne voyez pas leurs publications et ne pouvez pas échanger de messages avec eux.</Muted>
              : undefined}
            ListEmptyComponent={<EmptyState icon="account-cancel-outline" title="Aucun membre bloqué"
              message="Pour bloquer un membre, utilisez le menu ⋯ d'une publication, son profil ou une discussion." />}
            renderItem={({ item }) => (
              <View style={[styles.row, { borderColor: c.border }]}>
                <Avatar initials={initials(item)} color={item.avatarColor} image={item.avatarImage} size={44} />
                <View style={{ flex: 1 }}>
                  <T v="label" lines={1}>{displayName(item)}</T>
                  <Muted lines={1}>{location(item)}</Muted>
                </View>
                <Button title="Débloquer" kind="secondary" small onPress={() => unblockUser(item)} />
              </View>
            )}
          />
        )}
      </QueryView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 10, borderBottomWidth: StyleSheet.hairlineWidth },
});
