import { useState } from 'react';
import { View } from 'react-native';
import { FormError, FormScroll } from '@/components/Form';
import { Button, Header, Icon, Muted, PasswordField, Screen, T } from '@/components/ui';
import { confirm } from '@/core/dialog';
import { useColors } from '@/core/theme';
import { toast } from '@/core/toast';
import { data } from '@/data';
import { errorMessage } from '@/data/types';
import { signOut } from '@/features/ProfileView';

const ERASED = [
  'Votre profil, vos photos et vos centres d\'intérêt',
  'Vos publications, commentaires et j\'aime',
  'Vos conversations et messages',
  'Les communautés et événements que vous avez créés',
  'Vos adhésions, inscriptions et notifications',
];

/** Suppression définitive du compte (exigée par l'App Store et Google Play). */
export default function DeleteAccount() {
  const c = useColors();
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!password) {
      setError('Saisissez votre mot de passe pour confirmer.');
      return;
    }
    if (!(await confirm('Supprimer définitivement votre compte ?', 'Cette action est irréversible.', 'Supprimer', true))) return;
    setBusy(true);
    setError(null);
    try {
      await data().deleteAccount(password);
      await signOut();
      toast('Votre compte a été supprimé.', 'success');
    } catch (e) {
      setError(errorMessage(e));
      setBusy(false);
    }
  };

  return (
    <Screen>
      <FormScroll header={<Header title="Supprimer mon compte" />}>
        <View style={{ flexDirection: 'row', gap: 10, alignItems: 'center', marginBottom: 12 }}>
          <Icon name="alert-circle-outline" size={26} color={c.error} />
          <T v="h3" style={{ flex: 1 }}>Cette action est définitive</T>
        </View>
        <Muted style={{ marginBottom: 12 }}>Les données suivantes seront effacées et ne pourront pas être récupérées :</Muted>
        {ERASED.map((line) => (
          <View key={line} style={{ flexDirection: 'row', gap: 8, marginBottom: 6 }}>
            <Icon name="close-circle-outline" size={18} color={c.textSecondary} />
            <T style={{ flex: 1 }}>{line}</T>
          </View>
        ))}
        <View style={{ height: 16 }} />
        <PasswordField label="Mot de passe" value={password} onChangeText={setPassword} textContentType="password"
          placeholder="Confirmez avec votre mot de passe" onSubmitEditing={submit} />
        <FormError message={error} />
        <Button title="Supprimer définitivement mon compte" kind="danger" icon="delete-outline" onPress={submit} loading={busy} />
      </FormScroll>
    </Screen>
  );
}
