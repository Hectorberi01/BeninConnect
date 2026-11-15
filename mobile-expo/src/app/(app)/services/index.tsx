import { router } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { CityField } from '@/components/places';
import { CategoryGrid, Notice, ProviderCardView } from '@/components/services';
import { Button, EmptyState, Header, Icon, IconButton, Loading, Muted, Screen, SearchBar, SectionTitle, T } from '@/components/ui';
import { useMe } from '@/core/session';
import { useColors } from '@/core/theme';
import { matchCity } from '@/data/cities';
import { useDebounced } from '@/core/useDebounced';
import { useMyProvider, useServiceCategories, useServiceSearch, useServicesInfo } from '@/features/queries';

/** Accueil Services : proposer un service, catégories, services disponibles près de la ville choisie. */
export default function ServicesHome() {
  const c = useColors();
  const me = useMe();
  const [cityText, setCityText] = useState(() => matchCity(me.city)?.name ?? '');
  const city = matchCity(useDebounced(cityText));
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
            <IconButton name="plus-circle-outline" label="Proposer un service" color={c.primary} onPress={() => router.push('/services/me')} />
          </View>
        } />
      <ScrollView contentContainerStyle={{ paddingBottom: 32 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
        <View style={{ paddingHorizontal: 16, paddingTop: 12 }}>
          <SearchBar value={q} onChangeText={setQ} placeholder="Coiffure, ménage, traiteur…" />
          {q.trim().length > 1 && <Button title={`Rechercher « ${q.trim()} »`} small kind="secondary" onPress={() => search({ q: q.trim() })} style={{ marginTop: 8 }} />}
          <CityField style={{ marginTop: 12 }} label="Près de" value={cityText} onChangeText={setCityText} placeholder="Toutes les villes" hint={cityText.trim() && !city ? 'Ville non reconnue : choisissez une suggestion.' : undefined} />
        </View>

        {/* Créer un service : en tête de page, avant la liste des services existants */}
        <Pressable accessibilityRole="button" onPress={() => router.push('/services/me')}
          style={({ pressed }) => [styles.offer, { backgroundColor: c.primary }, pressed && { opacity: 0.9 }]}>
          <View style={styles.offerIcon}><Icon name={mine ? 'briefcase-account-outline' : 'briefcase-plus-outline'} color="#FFFFFF" /></View>
          <View style={{ flex: 1 }}>
            <T v="label" color="#FFFFFF" style={{ fontSize: 16 }}>{mine ? 'Mon espace prestataire' : 'Proposer un service'}</T>
            <Muted color="rgba(255,255,255,0.85)">{mine ? 'Ajouter une offre, voir les demandes reçues.' : 'Coiffure, cours, traiteur… Faites-vous connaître de la communauté.'}</Muted>
          </View>
          <Icon name={mine ? 'chevron-right' : 'plus'} color="#FFFFFF" />
        </Pressable>

        <SectionTitle title="Catégories" action="Tout voir" onAction={() => search({})} />
        <CategoryGrid categories={categories} onSelect={(cat) => search({ categoryId: cat.id })} />

        <SectionTitle title={city ? `Services près de ${city.name}` : 'Services disponibles'} action="Voir tout" onAction={() => search({})} />
        {nearby.isPending ? <Loading /> : providers.length === 0 ? (
          <EmptyState icon="account-search-outline" title="Pas encore de prestataire ici"
            message="Élargissez la zone ou proposez vos propres services." action="Chercher partout" onAction={() => setCityText('')} />
        ) : providers.map((p) => <ProviderCardView key={p.id} p={p} />)}


        {info && <View style={{ paddingHorizontal: 16 }}><Notice icon="handshake-outline">{info.intermediation}</Notice></View>}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  offer: { flexDirection: 'row', alignItems: 'center', gap: 12, marginHorizontal: 16, marginTop: 14, padding: 16, borderRadius: 18 },
  offerIcon: { width: 44, height: 44, borderRadius: 22, backgroundColor: 'rgba(255,255,255,0.18)', alignItems: 'center', justifyContent: 'center' },
});
