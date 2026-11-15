import { useState } from 'react';
import { FlatList, StyleSheet, View } from 'react-native';
import { Button, Card, EmptyState, Header, IconButton, Muted, QueryView, Screen, T, TextField } from '@/components/ui';
import { VoyagesGate } from '@/components/VoyagesGate';
import { formatDay, parseDay, PlaceFields, placeFilter, placeLabel } from '@/components/voyages';
import { useRouteAlerts, useRouteAlertActions, useVoyagesInfo } from '@/features/queries';

/** Alertes de trajet : une notification dès qu'un membre publie un trajet correspondant. */
export default function RouteAlertsScreen() {
  return <VoyagesGate><RouteAlerts /></VoyagesGate>;
}

function RouteAlerts() {
  const query = useRouteAlerts();
  const { create, remove } = useRouteAlertActions();
  const max = useVoyagesInfo().data?.maxAlerts ?? 5;
  const [fromCity, setFromCity] = useState('');
  const [fromCountry, setFromCountry] = useState('');
  const [toCity, setToCity] = useState('');
  const [toCountry, setToCountry] = useState('');
  const from = placeFilter(fromCity, fromCountry);
  const to = placeFilter(toCity, toCountry);
  const ready = !!from?.country && !!to?.country;
  const [before, setBefore] = useState('');
  const beforeDay = parseDay(before);
  const count = query.data?.length ?? 0;

  const add = () => create.mutate(
    { fromCountry: from!.country, fromCity: from!.city, toCountry: to!.country, toCity: to!.city, after: null, before: beforeDay ?? null },
    { onSuccess: () => { setFromCity(''); setFromCountry(''); setToCity(''); setToCountry(''); setBefore(''); } });

  return (
    <Screen>
      <Header title="Alertes de trajet" subtitle={`${count} sur ${max}`} />
      <QueryView query={query}>
        {(alerts) => (
          <FlatList data={alerts} keyExtractor={(a) => a.id} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag"
            renderItem={({ item: a }) => (
              <Card style={styles.card}>
                <View style={styles.row}>
                  <View style={{ flex: 1 }}>
                    <T v="label">{`${placeLabel({ country: a.fromCountry, city: a.fromCity })} → ${placeLabel({ country: a.toCountry, city: a.toCity })}`}</T>
                    <Muted>{a.before ? `Départs jusqu’au ${formatDay(a.before)}` : 'Toutes dates'}</Muted>
                  </View>
                  <IconButton name="delete-outline" label="Supprimer l’alerte" onPress={() => remove.mutate(a.id)} />
                </View>
              </Card>
            )}
            ListEmptyComponent={<EmptyState icon="bell-outline" title="Aucune alerte" message="Créez une alerte pour être prévenu dès qu’un membre part sur votre itinéraire." />}
            ListFooterComponent={count < max ? (
              <View style={{ padding: 16 }}>
                <T v="h3" style={{ marginBottom: 8 }}>Nouvelle alerte</T>
                <PlaceFields label="Départ" city={fromCity} country={fromCountry} onCity={setFromCity} onCountry={setFromCountry} />
                <PlaceFields label="Arrivée" city={toCity} country={toCountry} onCity={setToCity} onCountry={setToCountry} />
                <TextField label="Départ avant le (facultatif)" value={before} onChangeText={setBefore} placeholder="JJ/MM/AAAA"
                  keyboardType="numbers-and-punctuation" error={beforeDay === undefined ? 'Date invalide : JJ/MM/AAAA.' : null} />
                <Button title="Créer l’alerte" icon="bell-plus-outline" loading={create.isPending} disabled={!ready || beforeDay === undefined} onPress={add} />
              </View>
            ) : <Muted style={{ padding: 16 }}>{`${max} alertes au plus : supprimez-en une pour en créer une autre.`}</Muted>}
            contentContainerStyle={{ paddingTop: 8, paddingBottom: 32 }} />
        )}
      </QueryView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  card: { marginHorizontal: 16, marginBottom: 12, padding: 14 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
});
