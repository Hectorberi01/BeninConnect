import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native';
import { CityField } from '@/components/places';
import { Notice } from '@/components/services';
import { Button, Header, Muted, Screen, T, TextField } from '@/components/ui';
import { useMe } from '@/core/session';
import { useCreateServiceRequest } from '@/features/queries';

/** « 15/12/2026 » → « 2026-12-15 » ; null si vide, undefined si invalide. */
function parseDate(text: string): string | null | undefined {
  const t = text.trim();
  if (!t) return null;
  const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(t);
  if (!m) return undefined;
  const d = new Date(Number(m[3]), Number(m[2]) - 1, Number(m[1]));
  if (d.getMonth() !== Number(m[2]) - 1) return undefined;
  return `${m[3]}-${m[2].padStart(2, '0')}-${m[1].padStart(2, '0')}`;
}

/** Date du jour au format AAAA-MM-JJ (heure locale). */
const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

/** Demande de devis : le message part dans la messagerie, le prestataire accepte ou refuse. */
export default function RequestQuote() {
  const { offeringId, title, provider } = useLocalSearchParams<{ offeringId: string; title: string; provider: string }>();
  const me = useMe();
  const create = useCreateServiceRequest();
  const [message, setMessage] = useState('');
  const [date, setDate] = useState('');
  const [city, setCity] = useState(me.city ?? '');
  const parsed = parseDate(date);
  const past = !!parsed && parsed < today();
  const invalid = message.trim().length < 10 || parsed === undefined || past;

  const send = () => create.mutate({ offeringId, message: message.trim(), preferredDate: parsed ?? null, city: city.trim() || null }, {
    onSuccess: (r) => (r.conversationId ? router.replace(`/conversation/${r.conversationId}`) : router.back()),
  });

  return (
    <Screen>
      <Header title="Demander un devis" />
      <KeyboardAvoidingView behavior="padding" style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
          <T v="h3">{title}</T>
          <Muted style={{ marginBottom: 16 }}>{provider}</Muted>
          <TextField label="Votre demande" value={message} onChangeText={setMessage} multiline maxLength={2000} counter={`${message.length}/2000`}
            placeholder="Décrivez votre besoin : prestation, lieu, contraintes…" hint="10 caractères au moins." />
          <TextField label="Date souhaitée (facultatif)" value={date} onChangeText={setDate} placeholder="JJ/MM/AAAA" keyboardType="numbers-and-punctuation"
            error={parsed === undefined ? 'Date invalide : utilisez le format JJ/MM/AAAA.' : past ? 'Cette date est déjà passée.' : null} />
          <CityField label="Lieu de la prestation (facultatif)" value={city} onChangeText={setCity} placeholder="Ville" />
          <Notice icon="cash-lock">
            Aucun paiement ne passe par BéninConnect : vous fixez le prix et le règlement directement avec le prestataire. Ne payez jamais d’avance par transfert d’argent.
          </Notice>
          <View style={{ height: 16 }} />
          <Button title="Envoyer la demande" icon="send" loading={create.isPending} disabled={invalid} onPress={send} />
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}
