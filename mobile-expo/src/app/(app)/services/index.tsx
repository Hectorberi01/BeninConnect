import { router } from 'expo-router';
import { useState } from 'react';
import { ScrollView, View } from 'react-native';
import { CategoryGrid, CityPicker, Notice, ProviderCardView } from '@/components/services';
import { Button, Card, EmptyState, Header, Icon, IconButton, Loading, Muted, Screen, SearchBar, SectionTitle, T } from '@/components/ui';
import { useMe } from '@/core/session';
import { useColors } from '@/core/theme';
import { findCity, type City } from '@/data/cities';
import { useMyProvider, useServiceCategories, useServiceSearch, useServicesInfo } from '@/features/queries';

/** Accueil Services : catégories, prestataires recommandés près de la ville choisie. */
export default function ServicesHome() {
  const c = useColors();
  const me = useMe();
  const [city, setCity] = useState<City | null>(() => findCity(me.city) ?? null);
  const [q, setQ] = useState('');
  const categories = useServiceCategories().data ?? [];
  const info = useServicesInfo().data;
  const mine = useMyProvider().data;
  const nearby = useServiceSearch(city ? { lat: city.lat, lng: city.lng, radiusKm: 30, sort: 'Relevance' } : { sort: 'Relevance' });
  const providers = (nearby.data?.pages[0]?.items ?? []).slice(0, 6);

  const search = (params: Record<string, string>) =>
    router.push({ pathname: '/services/search', params: { ...params, ...(city ? { city: city.name, country: city.country } : {}) } });

  return (
    <Screen>
      <Header title="Services" subtitle="Trouvez un prestataire de la communauté"
        right={
          <View style={{ flexDirection: 'row' }}>
            <IconButton name="heart-outline" label="Prestataires favoris" onPress={() => router.push('/services/favorites')} />
            <IconButton name="clipboard-text-outline" label="Mes demandes" color={c.primary} onPress={() => router.push('/services/requests')} />
          </View>
        } />
      <ScrollView contentContainerStyle={{ paddingBottom: 32 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
        <View style={{ paddingHorizontal: 16, paddingTop: 12 }}>
          <SearchBar value={q} onChangeText={setQ} placeholder="Coiffure, ménage, traiteur…" />
          {q.trim().length > 1 && <Button title={`Rechercher « ${q.trim()} »`} small kind="secondary" onPress={() => search({ q: q.trim() })} style={{ marginTop: 8 }} />}
          <View style={{ marginTop: 12 }}><CityPicker value={city} onChange={setCity} label="Près de" allowAll="Partout" /></View>
        </View>

        <SectionTitle title="Catégories" action="Tout voir" onAction={() => search({})} />
        <CategoryGrid categories={categories} onSelect={(cat) => search({ categoryId: cat.id })} />

        <SectionTitle title={city ? `Recommandés près de ${city.name}` : 'Recommandés'} action="Voir tout" onAction={() => search({})} />
        {nearby.isPending ? <Loading /> : providers.length === 0 ? (
          <EmptyState icon="account-search-outline" title="Pas encore de prestataire ici"
            message="Élargissez la zone ou proposez vos propres services." action="Chercher partout" onAction={() => setCity(null)} />
        ) : providers.map((p) => <ProviderCardView key={p.id} p={p} />)}

        <Card style={{ marginHorizontal: 16, marginTop: 8, padding: 16 }} onPress={() => router.push('/services/me')}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
            <View style={{ width: 44, height: 44, borderRadius: 22, backgroundColor: c.primaryLight, alignItems: 'center', justifyContent: 'center' }}>
              <Icon name="briefcase-plus-outline" color={c.primary} />
            </View>
            <View style={{ flex: 1 }}>
              <T v="label" style={{ fontSize: 16 }}>{mine ? 'Mon espace prestataire' : 'Vous proposez un service ?'}</T>
              <Muted>{mine ? 'Profil, offres, demandes reçues et justificatifs.' : 'Coiffure, cours, traiteur… Faites-vous connaître de la communauté.'}</Muted>
            </View>
            <Icon name="chevron-right" color={c.textSecondary} />
          </View>
        </Card>

        {info && <View style={{ paddingHorizontal: 16 }}><Notice icon="handshake-outline">{info.intermediation}</Notice></View>}
      </ScrollView>
    </Screen>
  );
}
