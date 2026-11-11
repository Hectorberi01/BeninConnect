import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';
import { FormScroll } from '@/components/Form';
import { Button, Header, Icon, Muted, Screen, T, TextField } from '@/components/ui';
import { useColors } from '@/core/theme';
import * as v from '@/core/validation';
import { authRequest } from '@/data';
import { errorMessage } from '@/data/types';

export default function ForgotPassword() {
  const c = useColors();
  const params = useLocalSearchParams<{ email?: string }>();
  const [email, setEmail] = useState(params.email ?? '');
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  const send = async () => {
    const e = v.email(email);
    setError(e);
    if (e) return;
    setBusy(true);
    try {
      await authRequest(email, (d) => d.forgotPassword(email.trim()));
      setSent(true);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen>
      <FormScroll header={<Header title="Mot de passe oublié" />}>
        <View style={{ alignItems: 'center', marginVertical: 20 }}>
          <View style={{ width: 72, height: 72, borderRadius: 36, backgroundColor: c.primaryLight, alignItems: 'center', justifyContent: 'center' }}>
            <Icon name={sent ? 'email-fast-outline' : 'lock-reset'} size={34} color={c.primary} />
          </View>
          <T v="h2" style={{ marginTop: 16, textAlign: 'center' }}>{sent ? 'Vérifiez votre boîte mail' : 'Réinitialiser le mot de passe'}</T>
          <Muted style={{ textAlign: 'center', marginTop: 6, fontSize: 15 }}>
            {sent
              ? `Si un compte existe pour ${email.trim()}, un lien de réinitialisation vient d'être envoyé (simulation : aucun email réel).`
              : 'Saisissez votre adresse email : nous vous enverrons un lien pour choisir un nouveau mot de passe.'}
          </Muted>
        </View>
        {!sent && (
          <>
            <TextField label="Adresse email" value={email} onChangeText={setEmail} error={error}
              keyboardType="email-address" autoCapitalize="none" autoComplete="email" />
            <Button title="Envoyer le lien" onPress={send} loading={busy} />
          </>
        )}
        <Button title="Retour à la connexion" kind={sent ? 'primary' : 'ghost'} onPress={() => router.back()} style={{ marginTop: 8 }} />
      </FormScroll>
    </Screen>
  );
}
