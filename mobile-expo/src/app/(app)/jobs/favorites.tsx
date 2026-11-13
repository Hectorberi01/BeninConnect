import { router } from 'expo-router';
import { FlatList, RefreshControl } from 'react-native';
import { JobCard } from '@/components/jobs';
import { EmptyState, Header, QueryView, Screen } from '@/components/ui';
import { useColors } from '@/core/theme';
import { useFavoriteJobs } from '@/features/queries';

export default function FavoriteJobs() {
  const c = useColors();
  const query = useFavoriteJobs();
  return (
    <Screen>
      <Header title="Offres favorites" />
      <QueryView query={query} loadingLabel="Chargement…">
        {(jobs) => (
          <FlatList data={jobs} keyExtractor={(j) => j.id} renderItem={({ item }) => <JobCard job={item} />}
            ListEmptyComponent={<EmptyState icon="bookmark-outline" title="Aucune offre favorite"
              message="Touchez le marque-page d’une offre pour la retrouver ici." action="Voir les offres" onAction={() => router.replace('/jobs')} />}
            refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => { query.refetch(); }} tintColor={c.primary} />}
            contentContainerStyle={{ paddingTop: 12, paddingBottom: 24 }} />
        )}
      </QueryView>
    </Screen>
  );
}
