import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { FormError, FormScroll, InfoBox } from '@/components/Form';
import { Button, Checkbox, Muted, PasswordField, Screen, T, TextField } from '@/components/ui';
import { DEV_TOOLS, PRODUCTION_API_URL } from '@/core/config';
import { usePreferences } from '@/core/preferences';
import { useSession } from '@/core/session';
import { font, useColors } from '@/core/theme';
import * as v from '@/core/validation';
import { authRequest, data } from '@/data';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';
import { errorMessage } from '@/data/types';

export default function Login() {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const params = useLocalSearchParams<{ email?: string }>();
  const prefs = usePreferences();
  const [email, setEmail] = useState(params.email ?? '');
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(prefs.rememberMe);
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setFormError(null);
    const e = v.collect({ email: v.email(email), password: password ? null : 'Le mot de passe est obligatoire.' });
    setErrors(e);
    if (Object.keys(e).length) return;
    setBusy(true);
    try {
      const result = await authRequest(email, (d) => d.login(email.trim(), password));
      prefs.set({ rememberMe: remember });
      await useSession.getState().signIn(result, remember);
      router.replace('/home');
    } catch (err) {
      setFormError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const useDemo = () => {
    setEmail(DEMO_EMAIL);
    setPassword(DEMO_PASSWORD);
    setErrors({});
    setFormError(null);
  };

  return (
    <Screen style={{ paddingTop: insets.top }}>
      <FormScroll>
        <View style={styles.brand}>
          <View style={[styles.logo, { backgroundColor: c.primary }]}><T v="h1" color="#FFFFFF">B</T></View>
          <T v="h1" style={{ marginTop: 16 }}>Bon retour !</T>
          <Muted style={{ fontSize: 15, marginTop: 6 }}>Connectez-vous à BéninConnect</Muted>
        </View>

        <FormError message={formError} />
        <TextField label="Adresse email" value={email} onChangeText={setEmail} error={errors.email} icon="email-outline"
          keyboardType="email-address" autoCapitalize="none" autoComplete="email" textContentType="emailAddress" placeholder="vous@exemple.com" />
        <PasswordField label="Mot de passe" value={password} onChangeText={setPassword} error={errors.password}
          textContentType="password" placeholder="Votre mot de passe" onSubmitEditing={submit} />

        <View style={styles.row}>
          <View style={{ flex: 1 }}><Checkbox checked={remember} onChange={setRemember} label="Rester connecté" /></View>
          <T v="label" color={c.primary} style={{ marginBottom: 12 }}
            onPress={() => router.push({ pathname: '/forgot-password', params: { email } })}>Mot de passe oublié ?</T>
        </View>

        <Button title="Se connecter" onPress={submit} loading={busy} testID="login" />

        {data().hasDemoAccount && <View style={{ marginTop: 20 }}>
          <InfoBox icon="flask-outline">
            <T v="label" color={c.primaryDark}>{data().kind === 'api' ? 'Compte de démonstration (API)' : 'Compte de démonstration'}</T>
            <T v="caption" color={c.primaryDark} style={{ marginTop: 2 }}>{`${DEMO_EMAIL} · ${DEMO_PASSWORD}`}</T>
            <T v="label" color={c.primary} style={{ marginTop: 8, fontFamily: font.semibold }} onPress={useDemo}>Remplir automatiquement</T>
          </InfoBox>
        </View>}

        <View style={[styles.row, { justifyContent: 'center', marginTop: 12 }]}>
          <Muted style={{ fontSize: 15 }}>Pas encore de compte ? </Muted>
          <T v="label" color={c.primary} style={{ fontSize: 15 }} onPress={() => router.push('/register')}>Créer un compte</T>
        </View>

        {/* Développement seulement : source et adresse réellement utilisées */}
        {DEV_TOOLS && (
          <Muted style={{ textAlign: 'center', marginTop: 16, fontSize: 12 }}
            color={data().apiUrl === PRODUCTION_API_URL ? c.error : undefined}>
            {data().kind === 'api' ? `API : ${data().apiUrl}${data().apiUrl === PRODUCTION_API_URL ? ' (PRODUCTION)' : ''}` : 'Source : démo hors ligne'}
          </Muted>
        )}
      </FormScroll>
    </Screen>
  );
}

const styles = StyleSheet.create({
  brand: { alignItems: 'center', marginTop: 24, marginBottom: 28 },
  logo: { width: 64, height: 64, borderRadius: 18, alignItems: 'center', justifyContent: 'center' },
  row: { flexDirection: 'row', alignItems: 'center' },
});
