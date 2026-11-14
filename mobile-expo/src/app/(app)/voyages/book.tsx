import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native';
import { Notice } from '@/components/services';
import { Button, Checkbox, Header, Loading, Muted, Screen, SectionTitle, T, TextField } from '@/components/ui';
import { CategoryChips, ProhibitedNotice, tripDay, TripRoute } from '@/components/voyages';
import { kg, type ParcelCategory } from '@/data/types';
import { useRequestBooking, useTrip, useVoyagesInfo } from '@/features/queries';

/** Demande de réservation : poids, catégorie, contenu et attestation obligatoire. */
export default function BookTrip() {
  const { tripId } = useLocalSearchParams<{ tripId: string }>();
  const trip = useTrip(tripId).data;
  const info = useVoyagesInfo().data;
  const book = useRequestBooking();
  const [weight, setWeight] = useState('');
  const [categories, setCategories] = useState<ParcelCategory[]>([]);
  const [description, setDescription] = useState('');
  const [certified, setCertified] = useState(false);
  if (!trip) return <Screen><Header title="Réserver des kilos" /><Loading /></Screen>;

  const t = trip.trip;
  const value = Number(weight.replace(',', '.'));
  const max = Math.min(t.remainingKg, info?.maxBookingKg ?? 50);
  const weightError = weight && (!(value >= (info?.minBookingKg ?? 0.5)) || value > max) ? `Entre ${info?.minBookingKg ?? 0.5} et ${max} kg.` : null;
  const category = categories[categories.length - 1] ?? null;
  const invalid = !(value > 0) || !!weightError || !category || description.trim().length < 10 || !certified;

  const send = () => book.mutate({ tripId, input: { weightKg: value, category: category!, description: description.trim(), certified } }, {
    onSuccess: () => router.replace({ pathname: '/voyages/mine', params: { tab: 'sent' } }),
  });

  return (
    <Screen>
      <Header title="Réserver des kilos" />
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
          <TripRoute t={t} big />
          <Muted style={{ marginBottom: 12 }}>{`${tripDay(t.departureDate, true)} · ${t.traveler.name} · ${kg(t.remainingKg)} disponibles`}</Muted>
          <TextField label="Poids (kg)" value={weight} onChangeText={setWeight} keyboardType="decimal-pad" placeholder="Ex. 3" error={weightError} />
          <SectionTitle title="Type de colis" />
          <CategoryChips allowed={t.acceptedCategories} value={category ? [category] : []} onChange={(v) => setCategories(v.slice(-1))} />
          <View style={{ height: 12 }} />
          <TextField label="Contenu du colis" value={description} onChangeText={setDescription} multiline maxLength={500} counter={`${description.length}/500`}
            placeholder="Ex. vêtements pour enfants, deux paires de chaussures" hint="Décrivez précisément : le voyageur vérifiera le contenu." />
          {info && <ProhibitedNotice items={info.prohibitedItems} />}
          <Notice icon="package-variant">
            Le colis est remis ouvert au voyageur, qui vérifie son contenu. Une fois la réservation acceptée, vous recevrez un code à transmettre à votre destinataire.
          </Notice>
          <View style={{ height: 12 }} />
          <Checkbox checked={certified} onChange={setCertified} label="Je certifie que le contenu ne contient aucun objet interdit." />
          <View style={{ height: 16 }} />
          <Button title="Envoyer la demande" icon="send" loading={book.isPending} disabled={invalid} onPress={send} />
          <T v="caption" style={{ marginTop: 12, textAlign: 'center' }}>Aucun paiement dans l’application : le prix se règle entre vous.</T>
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}
