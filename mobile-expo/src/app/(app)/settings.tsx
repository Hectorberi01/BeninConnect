import { router } from 'expo-router';
import { type ReactNode, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { InfoBox } from '@/components/Form';
import { Avatar, Button, Card, Header, ListRow, Muted, Screen, Segmented, SwitchRow, T, TextField } from '@/components/ui';
import { confirm, info } from '@/core/dialog';
import { type DataSourceKind, DEFAULT_API_URL, type ThemePreference, usePreferences } from '@/core/preferences';
import { useMe } from '@/core/session';
import { useColors } from '@/core/theme';
import { toast } from '@/core/toast';
import { data, setDataSource } from '@/data';
import { displayName, initials } from '@/data/types';
import { signOut } from '@/features/ProfileView';

export default function Settings() {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const prefs = usePreferences();
  const [source, setSource] = useState<DataSourceKind>(prefs.dataSource);
  const [apiUrl, setApiUrl] = useState(prefs.apiUrl);
  const [urlError, setUrlError] = useState<string | null>(null);
  const sourceChanged = source !== data().kind || (source === 'api' && apiUrl.trim() !== prefs.apiUrl);

  const applySource = async () => {
    if (source === 'api' && !/^https?:\/\/\S+$/.test(apiUrl.trim())) {
      setUrlError("L'adresse doit commencer par http:// ou https://");
      return;
    }
    setUrlError(null);
    const ok = await confirm('Changer de source de données ?',
      'Vous serez déconnecté puis redirigé vers la connexion avec la nouvelle source.', 'Appliquer');
    if (!ok) return;
    prefs.set({ dataSource: source, apiUrl: apiUrl.trim().replace(/\/+$/, '') });
    setDataSource(null);
    await signOut();
    toast(source === 'api' ? `Source : API ${apiUrl.trim()}` : 'Source : données de démonstration hors ligne.');
  };

  const logout = async () => {
    if (await confirm('Se déconnecter ?', 'Vous reviendrez à l\'écran de connexion.', 'Se déconnecter', true)) await signOut();
  };

  return (
    <Screen>
      <Header title="Paramètres" />
      <ScrollView contentContainerStyle={{ padding: 16, paddingBottom: insets.bottom + 32 }}>
        <Card style={{ padding: 14 }} onPress={() => router.push('/profile/edit')}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
            <Avatar initials={initials(me)} color={me.avatarColor} image={me.avatarImage} size={52} />
            <View style={{ flex: 1 }}>
              <T v="h3">{displayName(me)}</T>
              <Muted>{me.email}</Muted>
            </View>
            <T v="label" color={c.primary}>Modifier</T>
          </View>
        </Card>

        <Group title="Apparence">
          <View style={{ marginHorizontal: -16, paddingVertical: 8 }}>
            <Segmented<ThemePreference> value={prefs.theme} onChange={(theme) => prefs.set({ theme })}
              items={[{ key: 'light', label: 'Clair' }, { key: 'dark', label: 'Sombre' }, { key: 'system', label: 'Système' }]} />
          </View>
        </Group>

        <Group title="Confidentialité">
          <SwitchRow icon="eye-off-outline" label="Profil privé" description="Seuls vos contacts voient votre profil complet"
            value={prefs.privateProfile} onChange={(privateProfile) => prefs.set({ privateProfile })} />
          <SwitchRow icon="map-marker-outline" label="Afficher ma ville" value={prefs.showCity} onChange={(showCity) => prefs.set({ showCity })} />
          <SwitchRow icon="message-outline" label="Autoriser les messages" description="Les membres peuvent vous écrire"
            value={prefs.allowMessages} onChange={(allowMessages) => prefs.set({ allowMessages })} />
          <ListRow icon="shield-account-outline" label="Vos données" onPress={() => info('Vos données',
            data().kind === 'api'
              ? 'Vos réglages sont enregistrés sur cet appareil. Les contenus publiés sont envoyés au serveur BéninConnect configuré.'
              : 'Dans cette démonstration, vos réglages sont enregistrés uniquement sur cet appareil. Les contenus que vous publiez restent en mémoire et disparaissent à la fermeture de l\'application.')} />
        </Group>

        <Group title="Notifications">
          <SwitchRow icon="heart-outline" label="Activité sociale" value={prefs.notifySocial} onChange={(notifySocial) => prefs.set({ notifySocial })} />
          <SwitchRow icon="account-group-outline" label="Communautés" value={prefs.notifyCommunities} onChange={(notifyCommunities) => prefs.set({ notifyCommunities })} />
          <SwitchRow icon="message-outline" label="Messages" value={prefs.notifyMessages} onChange={(notifyMessages) => prefs.set({ notifyMessages })} />
          <SwitchRow icon="calendar-outline" label="Événements" value={prefs.notifyEvents} onChange={(notifyEvents) => prefs.set({ notifyEvents })} />
        </Group>

        <Group title="Langue">
          <ListRow icon="translate" label="Langue de l'application" value="Français"
            onPress={() => toast("Le français est la langue de cette version. L'anglais arrivera dans une prochaine version.")} />
        </Group>

        <Group title="Source de données">
          <View style={{ marginHorizontal: -16, paddingVertical: 8 }}>
            <Segmented<DataSourceKind> value={source} onChange={setSource}
              items={[{ key: 'demo', label: 'Démo hors ligne' }, { key: 'api', label: 'API BéninConnect' }]} />
          </View>
          {source === 'api' && (
            <TextField label="Adresse de l'API" value={apiUrl} onChangeText={setApiUrl} error={urlError} autoCapitalize="none" autoCorrect={false}
              keyboardType="url" placeholder={DEFAULT_API_URL}
              hint="Simulateur iOS : http://localhost:5080 · Émulateur Android : http://10.0.2.2:5080 · Téléphone : IP du Mac" />
          )}
          <Muted style={{ marginBottom: 10 }}>{`Source active : ${data().kind === 'api' ? `API (${prefs.apiUrl})` : 'démonstration hors ligne'}`}</Muted>
          {sourceChanged && <Button title="Appliquer" icon="swap-horizontal" onPress={applySource} style={{ marginBottom: 8 }} />}
        </Group>

        {data().kind === 'demo' && (
          <Group title="Développeur">
            <SwitchRow icon="bug-outline" label="Simuler des erreurs réseau" description="Environ un appel sur deux échoue"
              value={prefs.simulateErrors} onChange={(simulateErrors) => {
                prefs.set({ simulateErrors });
                toast(simulateErrors ? 'Erreurs simulées activées : environ un appel sur deux échouera.' : 'Erreurs simulées désactivées.');
              }} />
          </Group>
        )}

        <Group title="À propos">
          <ListRow icon="information-outline" label="À propos de BéninConnect" value="1.0.0" onPress={() => info('À propos de BéninConnect',
            'BéninConnect Community — prototype de démonstration (version 1.0, React Native / Expo).\n\n'
            + 'Toutes les personnes, communautés, publications et événements sont fictifs.\n\n'
            + 'Icônes : Material Design Icons (Apache 2.0). Police : Inter (SIL OFL). Images : illustrations générées pour le projet.')} />
        </Group>

        <View style={{ marginTop: 20 }}>
          <InfoBox icon="flask-outline"><T v="caption" color={c.primaryDark}>Version 1.0.0 (démonstration)</T></InfoBox>
          <Button title="Se déconnecter" kind="danger" icon="logout" onPress={logout} />
        </View>
      </ScrollView>
    </Screen>
  );
}

function Group({ title, children }: { title: string; children: ReactNode }) {
  const c = useColors();
  return (
    <View style={{ marginTop: 20 }}>
      <T v="label" color={c.textSecondary} style={styles.groupTitle}>{title.toUpperCase()}</T>
      <Card style={{ paddingHorizontal: 16 }}>{children}</Card>
    </View>
  );
}

const styles = StyleSheet.create({
  groupTitle: { marginBottom: 8, marginLeft: 4, letterSpacing: 0.5 },
});
