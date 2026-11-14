import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { ScrollView, View } from 'react-native';
import { Notice } from '@/components/services';
import { Button, Header, Muted, Screen, T, TextField } from '@/components/ui';
import { errorMessage } from '@/data/types';
import { useDeliverBooking } from '@/features/queries';

/** Le voyageur saisit le code donné par le destinataire : 5 essais au plus. */
export default function DeliverBooking() {
  const { id, name } = useLocalSearchParams<{ id: string; name?: string }>();
  const deliver = useDeliverBooking();
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const valid = /^\d{6}$/.test(code);

  const submit = () => {
    setError(null);
    deliver.mutate({ id, code }, {
      onSuccess: () => router.back(),
      onError: (e) => { setError(errorMessage(e)); setCode(''); },
    });
  };

  return (
    <Screen>
      <Header title="Confirmer la livraison" />
      <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
        <T v="h3">Code de remise</T>
        <Muted style={{ marginBottom: 12 }}>{`Demandez au destinataire le code à 6 chiffres reçu de ${name ?? 'l’expéditeur'}.`}</Muted>
        <TextField label="Code" value={code} onChangeText={(t) => setCode(t.replace(/\D/g, '').slice(0, 6))} keyboardType="number-pad" maxLength={6}
          placeholder="000000" autoFocus error={error} textContentType="oneTimeCode" />
        <Notice icon="shield-key-outline">5 essais au plus. Au-delà, l’expéditeur doit générer un nouveau code.</Notice>
        <View style={{ height: 16 }} />
        <Button title="Confirmer la livraison" icon="check" loading={deliver.isPending} disabled={!valid} onPress={submit} />
      </ScrollView>
    </Screen>
  );
}
