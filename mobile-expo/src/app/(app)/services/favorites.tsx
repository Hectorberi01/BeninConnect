import { router } from 'expo-router';
import { FlatList, RefreshControl } from 'react-native';
import { ProviderCardView } from '@/components/services';
import { EmptyState, Header, QueryView, Screen } from '@/components/ui';
import { useColors } from '@/core/theme';
import { useFavoriteProviders } from '@/features/queries';

export default function FavoriteProviders() {
  const c = useColors();
  const query = useFavoriteProviders();
  return (
    <Screen>
      <Header title="Prestataires favoris" />
      <QueryView query={query}>
        {(items) => (
          <FlatList data={items} keyExtractor={(p) => p.id} renderItem={({ item }) => <ProviderCardView p={item} />}
            ListEmptyComponent={<EmptyState icon="heart-outline" title="Aucun favori" message="Touchez le cœur d’un prestataire pour le retrouver ici."
              action="Trouver un prestataire" onAction={() => router.replace('/services')} />}
            refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
            contentContainerStyle={{ paddingTop: 12, paddingBottom: 24 }} />
        )}
      </QueryView>
    </Screen>
  );
}
