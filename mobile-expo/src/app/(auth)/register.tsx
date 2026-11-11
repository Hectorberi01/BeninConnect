import { router } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';
import { useState } from 'react';
import { View } from 'react-native';
import { FormError, FormScroll } from '@/components/Form';
import { Button, Checkbox, Header, Muted, PasswordField, Screen, T, TextField } from '@/components/ui';
import { SITE_URL } from '@/core/config';
import { useColors } from '@/core/theme';
import * as v from '@/core/validation';
import { authRequest, data } from '@/data';
import { errorMessage } from '@/data/types';

type Field = 'firstName' | 'lastName' | 'email' | 'country' | 'city' | 'password' | 'confirm' | 'terms';

export default function Register() {
  const c = useColors();
  const demo = data().kind !== 'api';
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', country: '', city: '', password: '', confirm: '' });
  const [terms, setTerms] = useState(false);
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof form) => (value: string) => setForm((f) => ({ ...f, [k]: value }));

  const submit = async () => {
    setFormError(null);
    const e = v.collect({
      firstName: v.length(form.firstName, 'Le prénom', 2, 40),
      lastName: v.length(form.lastName, 'Le nom', 2, 40),
      email: v.email(form.email),
      country: v.required(form.country, 'Le pays'),
      city: v.required(form.city, 'La ville'),
      password: v.password(form.password),
      confirm: form.password === form.confirm ? null : 'Les mots de passe ne correspondent pas.',
      terms: terms ? null : 'Vous devez accepter les conditions de la démonstration.',
    });
    setErrors(e);
    if (Object.keys(e).length) return;
    setBusy(true);
    try {
      const { confirm: _confirm, ...request } = form;
      await authRequest(form.email, (d) => d.register({ ...request, email: form.email.trim() }));
      router.replace({ pathname: '/verify-email', params: { email: form.email.trim() } });
    } catch (err) {
      setFormError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen>
      <FormScroll header={<Header title="Créer un compte" subtitle="Rejoignez la communauté BéninConnect" />}>
        <FormError message={formError} />
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <TextField style={{ flex: 1 }} label="Prénom" value={form.firstName} onChangeText={set('firstName')} error={errors.firstName} autoComplete="given-name" />
          <TextField style={{ flex: 1 }} label="Nom" value={form.lastName} onChangeText={set('lastName')} error={errors.lastName} autoComplete="family-name" />
        </View>
        <TextField label="Adresse email" value={form.email} onChangeText={set('email')} error={errors.email}
          keyboardType="email-address" autoCapitalize="none" autoComplete="email" />
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <TextField style={{ flex: 1 }} label="Pays de résidence" value={form.country} onChangeText={set('country')} error={errors.country} placeholder="France" />
          <TextField style={{ flex: 1 }} label="Ville" value={form.city} onChangeText={set('city')} error={errors.city} placeholder="Lyon" />
        </View>
        <PasswordField label="Mot de passe" value={form.password} onChangeText={set('password')} error={errors.password}
          hint="8 caractères minimum, lettres et chiffres" textContentType="newPassword" />
        <PasswordField label="Confirmer le mot de passe" value={form.confirm} onChangeText={set('confirm')} error={errors.confirm} />
        <Checkbox checked={terms} onChange={setTerms} error={errors.terms}
          label={demo
            ? "J'accepte les conditions d'utilisation de cette démonstration (aucune donnée réelle n'est collectée)."
            : "J'accepte les conditions d'utilisation (aucune tolérance pour les contenus abusifs) et la politique de confidentialité."} />
        {!demo && (
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 16, marginBottom: 14 }}>
            <T v="label" color={c.primary} onPress={() => WebBrowser.openBrowserAsync(`${SITE_URL}/conditions/`)}>Conditions d'utilisation</T>
            <T v="label" color={c.primary} onPress={() => WebBrowser.openBrowserAsync(`${SITE_URL}/confidentialite/`)}>Confidentialité</T>
          </View>
        )}
        <Button title="Créer mon compte" onPress={submit} loading={busy} />
        <Muted style={{ textAlign: 'center', marginTop: 16 }}>Un code de vérification vous sera demandé à l'étape suivante.</Muted>
      </FormScroll>
    </Screen>
  );
}
