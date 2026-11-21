import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';
import { FormScroll, InfoBox } from '@/components/Form';
import { Button, Header, Icon, Muted, PasswordField, Screen, T, TextField } from '@/components/ui';
import { usePreferences } from '@/core/preferences';
import { useSession } from '@/core/session';
import { useColors } from '@/core/theme';
import { toast } from '@/core/toast';
import * as v from '@/core/validation';
import { authRequest, data } from '@/data';
import { errorMessage } from '@/data/types';

/** Mot de passe oublié : email, puis code reçu et nouveau mot de passe ; connexion directe ensuite. */
export default function ForgotPassword() {
  const c = useColors();
  const params = useLocalSearchParams<{ email?: string }>();
  const [email, setEmail] = useState(params.email ?? '');
  const [step, setStep] = useState<'email' | 'code'>('email');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState<Record<string, string | null>>({});
  const [busy, setBusy] = useState(false);
  const demoCode = data().demoVerificationCode;
  const address = email.trim();

  const send = async () => {
    const e = v.email(email);
    setErrors({ email: e });
    if (e) return;
    setBusy(true);
    try {
      await authRequest(address, (d) => d.forgotPassword(address));
      setStep('code');
    } catch (err) {
      setErrors({ email: errorMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const resend = async () => {
    try {
      await authRequest(address, (d) => d.forgotPassword(address));
      toast(`Un nouveau code a été envoyé à ${address}.`);
    } catch (err) {
      toast(errorMessage(err), 'error');
    }
  };

  const reset = async () => {
    const e = {
      code: /^\d{6}$/.test(code.trim()) ? null : 'Le code contient 6 chiffres.',
      password: v.password(password),
      confirm: confirm === password ? null : 'Les mots de passe ne correspondent pas.',
    };
    setErrors(e);
    if (e.code || e.password || e.confirm) return;
    setBusy(true);
    try {
      await authRequest(address, (d) => d.resetPassword(address, code.trim(), password));
      // Connexion directe avec le nouveau mot de passe
      const result = await authRequest(address, (d) => d.login(address, password));
      await useSession.getState().signIn(result, usePreferences.getState().rememberMe);
      toast('Mot de passe modifié. Bienvenue !', 'success');
      router.replace('/home');
    } catch (err) {
      setErrors({ code: errorMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen>
      <FormScroll header={<Header title="Mot de passe oublié" onBack={step === 'code' ? () => setStep('email') : undefined} />}>
        <View style={{ alignItems: 'center', marginVertical: 20 }}>
          <View style={{ width: 72, height: 72, borderRadius: 36, backgroundColor: c.primaryLight, alignItems: 'center', justifyContent: 'center' }}>
            <Icon name={step === 'code' ? 'email-fast-outline' : 'lock-reset'} size={34} color={c.primary} />
          </View>
          <T v="h2" style={{ marginTop: 16, textAlign: 'center' }}>{step === 'code' ? 'Choisissez un nouveau mot de passe' : 'Réinitialiser le mot de passe'}</T>
          <Muted style={{ textAlign: 'center', marginTop: 6, fontSize: 15 }}>
            {step === 'code'
              ? `Si un compte existe pour ${address}, un code à 6 chiffres vient d'y être envoyé. Saisissez-le ci-dessous.`
              : 'Saisissez votre adresse email : nous vous enverrons un code pour choisir un nouveau mot de passe.'}
          </Muted>
        </View>

        {step === 'email' ? (
          <>
            <TextField label="Adresse email" value={email} onChangeText={setEmail} error={errors.email}
              keyboardType="email-address" autoCapitalize="none" autoComplete="email" />
            <Button title="Recevoir le code" onPress={send} loading={busy} />
            <Button title="Retour à la connexion" kind="ghost" onPress={() => router.back()} style={{ marginTop: 8 }} />
          </>
        ) : (
          <>
            <TextField label="Code reçu par email" value={code} onChangeText={(t) => setCode(t.replace(/\D/g, '').slice(0, 6))} error={errors.code}
              keyboardType="number-pad" textContentType="oneTimeCode" autoComplete="one-time-code" placeholder="000000" maxLength={6} />
            <PasswordField label="Nouveau mot de passe" value={password} onChangeText={setPassword} error={errors.password}
              textContentType="newPassword" autoComplete="new-password" hint="8 caractères au moins, avec des lettres et un chiffre." />
            <PasswordField label="Confirmer le mot de passe" value={confirm} onChangeText={setConfirm} error={errors.confirm}
              textContentType="newPassword" autoComplete="new-password" />
            {demoCode ? (
              <InfoBox icon="flask-outline">
                <T v="caption" color={c.primaryDark}>{`Démonstration : aucun email n'est envoyé. Le code est ${demoCode}.`}</T>
                <T v="label" color={c.primary} style={{ marginTop: 6 }} onPress={() => setCode(demoCode)}>Utiliser le code</T>
              </InfoBox>
            ) : (
              <Muted style={{ marginBottom: 12, textAlign: 'center' }}>Pensez à vérifier vos courriers indésirables. Le code est valable peu de temps.</Muted>
            )}
            <Button title="Changer le mot de passe" onPress={reset} loading={busy} />
            <Button title="Renvoyer le code" kind="ghost" onPress={resend} style={{ marginTop: 8 }} />
          </>
        )}
      </FormScroll>
    </Screen>
  );
}
