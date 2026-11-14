import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { CityPicker, Notice } from '@/components/services';
import { Button, Checkbox, Header, Muted, Screen, Segmented, SectionTitle, T, TextField } from '@/components/ui';
import { VoyagesGate } from '@/components/VoyagesGate';
import { CategoryChips, formatDay, isoDay, parseDay, ProhibitedNotice } from '@/components/voyages';
import { type City, findCity } from '@/data/cities';
import type { ParcelCategory } from '@/data/types';
import { useSaveTrip, useTrip, useVoyagesInfo } from '@/features/queries';

/** Publier ou modifier un trajet, en deux étapes : itinéraire puis conditions. */
export default function PublishTripScreen() {
  return <VoyagesGate><PublishTrip /></VoyagesGate>;
}

function PublishTrip() {
  const { id } = useLocalSearchParams<{ id?: string }>();
  const existing = useTrip(id ?? '').data;
  const info = useVoyagesInfo().data;
  const save = useSaveTrip();
  const [step, setStep] = useState<1 | 2>(1);
  const [from, setFrom] = useState<City | null>(null);
  const [to, setTo] = useState<City | null>(null);
  const [date, setDate] = useState('');
  const [capacity, setCapacity] = useState('');
  const [categories, setCategories] = useState<ParcelCategory[]>(['Documents', 'Clothes']);
  const [price, setPrice] = useState('');
  const [currency, setCurrency] = useState<'EUR' | 'XOF'>('EUR');
  const [zones, setZones] = useState('');
  const [notes, setNotes] = useState('');
  const [rules, setRules] = useState(!!id);

  useEffect(() => {
    if (!id || !existing) return;
    const t = existing.trip;
    setFrom(findCity(t.fromCity, t.fromCountry) ?? { name: t.fromCity, country: t.fromCountry, lat: 0, lng: 0 });
    setTo(findCity(t.toCity, t.toCountry) ?? { name: t.toCity, country: t.toCountry, lat: 0, lng: 0 });
    setDate(formatDay(t.departureDate));
    setCapacity(String(t.capacityKg));
    setCategories(t.acceptedCategories);
    setPrice(t.pricePerKg == null ? '' : String(t.pricePerKg));
    setCurrency(t.currency);
    setZones(existing.dropOffZones ?? '');
    setNotes(existing.notes ?? '');
  }, [id, existing]);

  const day = parseDay(date);
  const maxDays = info?.maxDaysAhead ?? 90;
  const dateError = date && (!day || day < isoDay() || day > isoDay(maxDays)) ? `Une date entre aujourd’hui et dans ${maxDays} jours (JJ/MM/AAAA).` : null;
  const kgValue = Number(capacity.replace(',', '.'));
  const kgError = capacity && !(kgValue >= 0.5 && kgValue <= (info?.maxCapacityKg ?? 100)) ? `Entre 0,5 et ${info?.maxCapacityKg ?? 100} kg.` : null;
  const sameCity = !!from && !!to && from.name === to.name && from.country === to.country;
  const step1Valid = !!from && !!to && !sameCity && !!day && !dateError && kgValue > 0 && !kgError;
  const priceValue = price.trim() ? Number(price.replace(',', '.')) : null;
  const priceError = priceValue != null && !(priceValue >= 0) ? 'Prix invalide.' : null;
  const step2Valid = categories.length > 0 && !priceError && rules;

  const submit = () => save.mutate({
    id, input: {
      fromCountry: from!.country, fromCity: from!.name, toCountry: to!.country, toCity: to!.name, departureDate: day!, capacityKg: kgValue,
      pricePerKg: priceValue, currency, acceptedCategories: categories, dropOffZones: zones.trim() || null, notes: notes.trim() || null,
    },
  }, { onSuccess: (d) => router.replace(`/voyages/trip/${d.trip.id}`) });

  return (
    <Screen>
      <Header title={id ? 'Modifier mon trajet' : 'Proposer mes kilos'} subtitle={`Étape ${step} sur 2`} onBack={step === 2 ? () => setStep(1) : undefined} />
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
          {step === 1 ? (
            <>
              <T v="h3" style={styles.title}>Votre voyage</T>
              <CityPicker label="Ville de départ" value={from} onChange={setFrom} />
              <CityPicker label="Ville d’arrivée" value={to} onChange={setTo} />
              {sameCity && <Muted style={{ marginBottom: 8 }}>Choisissez deux villes différentes.</Muted>}
              <TextField label="Date de départ" value={date} onChangeText={setDate} placeholder="JJ/MM/AAAA" keyboardType="numbers-and-punctuation"
                error={dateError} />
              <TextField label="Kilos disponibles" value={capacity} onChangeText={setCapacity} keyboardType="decimal-pad" placeholder="Ex. 8" error={kgError} />
              <Button title="Continuer" icon="arrow-right" disabled={!step1Valid} onPress={() => setStep(2)} />
            </>
          ) : (
            <>
              <T v="h3" style={styles.title}>Vos conditions</T>
              <SectionTitle title="Colis acceptés" />
              <CategoryChips value={categories} onChange={setCategories} />
              <View style={{ height: 12 }} />
              <View style={styles.row}>
                <View style={{ flex: 1 }}>
                  <TextField label="Prix indicatif au kilo (facultatif)" value={price} onChangeText={setPrice} keyboardType="decimal-pad"
                    placeholder={currency === 'EUR' ? 'Ex. 10' : 'Ex. 5000'} error={priceError} />
                </View>
              </View>
              <Segmented value={currency} onChange={setCurrency} items={[{ key: 'EUR', label: 'Euros' }, { key: 'XOF', label: 'FCFA' }]} />
              <View style={{ height: 12 }} />
              <TextField label="Zones de remise (facultatif)" value={zones} onChangeText={setZones} maxLength={300} placeholder="Ex. Paris 11e, Gare du Nord" />
              <TextField label="Informations (facultatif)" value={notes} onChangeText={setNotes} multiline maxLength={1000} counter={`${notes.length}/1000`}
                placeholder="Ex. valise en soute, remise la veille au soir" />
              {info && <ProhibitedNotice items={info.prohibitedItems} />}
              {info && (
                <Notice icon="shield-alert-outline">
                  {`${info.travelerAdvice.join(' ')} ${info.travelerResponsibility}`}
                </Notice>
              )}
              <View style={{ height: 12 }} />
              <Checkbox checked={rules} onChange={setRules} label="J’ai lu ces règles : je vérifierai chaque colis et je refuserai en cas de doute." />
              <View style={{ height: 16 }} />
              <Button title={id ? 'Enregistrer' : 'Publier le trajet'} icon="check" loading={save.isPending} disabled={!step2Valid || !step1Valid} onPress={submit} />
              <T v="caption" style={{ marginTop: 12, textAlign: 'center' }}>Le prix est indicatif : aucun paiement ne passe par BéninConnect.</T>
            </>
          )}
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  title: { marginBottom: 12 },
  row: { flexDirection: 'row', gap: 8 },
});
