import { useEffect, useState } from 'react';
import { KeyboardAvoidingView, Modal, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Notice } from '@/components/services';
import { CityField, CountryField, countryError } from '@/components/places';
import { Button, Checkbox, Header, IconButton, Muted, Segmented, SectionTitle, T, TextField } from '@/components/ui';
import { CategoryChips, formatDay, isoDay, parseDay, ProhibitedNotice } from '@/components/voyages';
import { useColors } from '@/core/theme';
import { countryCode, countryName, normalizePlace } from '@/data/cities';
import type { ParcelCategory } from '@/data/types';
import { useSaveTrip, useTrip, useVoyagesInfo } from '@/features/queries';

/** Fenêtre « Proposer mes kilos » : le membre reste sur la page des trajets. */
export function TripSheet({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  const c = useColors();
  return (
    <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={onClose} statusBarTranslucent>
      <View style={{ flex: 1, backgroundColor: c.background }}>
        {visible && <TripForm onSaved={onClose} onClose={onClose} />}
      </View>
    </Modal>
  );
}

/** Publier ou modifier un trajet, en deux étapes : itinéraire puis conditions. onClose : affiché dans une fenêtre. */
export function TripForm({ id, onSaved, onClose }: { id?: string; onSaved: (tripId: string) => void; onClose?: () => void }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const existing = useTrip(id ?? '').data;
  const info = useVoyagesInfo().data;
  const save = useSaveTrip();
  const [step, setStep] = useState<1 | 2>(1);
  const [fromCity, setFromCity] = useState('');
  const [fromCountry, setFromCountry] = useState('');
  const [toCity, setToCity] = useState('');
  const [toCountry, setToCountry] = useState('');
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
    setFromCity(t.fromCity);
    setFromCountry(countryName(t.fromCountry));
    setToCity(t.toCity);
    setToCountry(countryName(t.toCountry));
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
  const from = countryCode(fromCountry);
  const to = countryCode(toCountry);
  const cityOk = (t: string) => t.trim().length >= 2 && t.trim().length <= 80;
  const sameCity = !!fromCity.trim() && from === to && normalizePlace(fromCity) === normalizePlace(toCity);
  const step1Valid = cityOk(fromCity) && cityOk(toCity) && !!from && !!to && !sameCity && !!day && !dateError && kgValue > 0 && !kgError;
  const priceValue = price.trim() ? Number(price.replace(',', '.')) : null;
  const priceError = priceValue != null && !(priceValue >= 0) ? 'Prix invalide.' : null;
  const step2Valid = categories.length > 0 && !priceError && rules;

  const submit = () => save.mutate({
    id, input: {
      fromCountry: from!, fromCity: fromCity.trim(), toCountry: to!, toCity: toCity.trim(), departureDate: day!, capacityKg: kgValue,
      pricePerKg: priceValue, currency, acceptedCategories: categories, dropOffZones: zones.trim() || null, notes: notes.trim() || null,
    },
  }, { onSuccess: (d) => onSaved(d.trip.id) });

  const title = id ? 'Modifier mon trajet' : 'Proposer mes kilos';
  const subtitle = `Étape ${step} sur 2`;
  return (
    <>
      {onClose ? (
        <View style={[styles.sheetHead, { borderColor: c.border, paddingTop: Platform.OS === 'ios' ? 12 : insets.top + 12 }]}>
          {step === 2 && <IconButton name="arrow-left" label="Étape précédente" onPress={() => setStep(1)} />}
          <View style={{ flex: 1, marginLeft: step === 2 ? 4 : 8 }}>
            <T v="h3" lines={1}>{title}</T>
            <Muted lines={1}>{subtitle}</Muted>
          </View>
          <IconButton name="close" label="Fermer" onPress={onClose} />
        </View>
      ) : (
        <Header title={title} subtitle={subtitle} onBack={step === 2 ? () => setStep(1) : undefined} />
      )}
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ padding: 16, paddingBottom: insets.bottom + 24 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
          {step === 1 ? (
            <>
              <T v="h3" style={styles.title}>Votre voyage</T>
              <View style={styles.row}>
                <CityField style={{ flex: 1 }} label="Ville de départ" value={fromCity} onChangeText={setFromCity} placeholder="Paris"
                  onPick={(city) => setFromCountry(countryName(city.country))} />
                <CountryField style={{ flex: 1 }} label="Pays" value={fromCountry} onChangeText={setFromCountry} placeholder="France" error={countryError(fromCountry)} />
              </View>
              <View style={styles.row}>
                <CityField style={{ flex: 1 }} label="Ville d’arrivée" value={toCity} onChangeText={setToCity} placeholder="Cotonou"
                  onPick={(city) => setToCountry(countryName(city.country))} />
                <CountryField style={{ flex: 1 }} label="Pays" value={toCountry} onChangeText={setToCountry} placeholder="Bénin" error={countryError(toCountry)} />
              </View>
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
    </>
  );
}

const styles = StyleSheet.create({
  title: { marginBottom: 12 },
  row: { flexDirection: 'row', gap: 8 },
  sheetHead: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 8, paddingBottom: 10, borderBottomWidth: StyleSheet.hairlineWidth },
});
