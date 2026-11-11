import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';
import { FormScroll, InfoBox } from '@/components/Form';
import { Button, Header, Icon, Muted, Screen, T, TextField } from '@/components/ui';
import { useSession } from '@/core/session';
import { useColors } from '@/core/theme';
import { toast } from '@/core/toast';
import { authRequest, data } from '@/data';
import { errorMessage } from '@/data/types';

export default function VerifyEmail() {
  const c = useColors();
  const { email = '' } = useLocalSearchParams<{ email?: string }>();
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const demoCode = data().demoVerificationCode;

  const verify = async () => {
    const trimmed = code.trim();
    if (!/^\d{6}$/.test(trimmed)) {
      setError('Le code contient 6 chiffres.');
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const result = await authRequest(email, (d) => d.verifyEmail(email, trimmed));
      await useSession.getState().signIn(result, true);
      toast('Adresse email vérifiée. Bienvenue !', 'success');
      router.replace('/home');
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const resend = async () => {
    try {
      await authRequest(email, (d) => d.resendCode(email));
      toast(demoCode ? `Nouveau code simulé : ${demoCode} (aucun email n'est envoyé).` : `Un nouveau code a été envoyé à ${email}.`);
    } catch (err) {
      toast(errorMessage(err), 'error');
    }
  };

  return (
    <Screen>
      <FormScroll header={<Header title="Vérifier votre email" onBack={() => router.replace('/login')} />}>
        <View style={{ alignItems: 'center', marginVertical: 20 }}>
          <View style={{ width: 72, height: 72, borderRadius: 36, backgroundColor: c.primaryLight, alignItems: 'center', justifyContent: 'center' }}>
            <Icon name="email-check-outline" size={34} color={c.primary} />
          </View>
          <T v="h2" style={{ marginTop: 16, textAlign: 'center' }}>Entrez le code reçu</T>
          <Muted style={{ textAlign: 'center', marginTop: 6, fontSize: 15 }}>{`Un code à 6 chiffres a été envoyé à ${email}`}</Muted>
        </View>
        <TextField label="Code de vérification" value={code} onChangeText={(t) => setCode(t.replace(/\D/g, '').slice(0, 6))} error={error}
          keyboardType="number-pad" textContentType="oneTimeCode" autoComplete="one-time-code" placeholder="000000" maxLength={6} />
        {demoCode ? (
          <InfoBox icon="flask-outline">
            <T v="caption" color={c.primaryDark}>{`Démonstration : aucun email n'est envoyé. Le code est ${demoCode}.`}</T>
            <T v="label" color={c.primary} style={{ marginTop: 6 }} onPress={() => setCode(demoCode)}>Utiliser le code</T>
          </InfoBox>
        ) : (
          <Muted style={{ marginBottom: 12, textAlign: 'center' }}>Pensez à vérifier vos courriers indésirables.</Muted>
        )}
        <Button title="Vérifier" onPress={verify} loading={busy} />
        <Button title="Renvoyer le code" kind="ghost" onPress={resend} style={{ marginTop: 8 }} />
      </FormScroll>
    </Screen>
  );
}
