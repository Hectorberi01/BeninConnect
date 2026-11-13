import * as Linking from 'expo-linking';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { APP_VERSION, STORE_URL } from '@/core/appUpdates';
import { useColors } from '@/core/theme';
import { Button, Icon, Muted, T } from './ui';

/** Écran bloquant quand la version installée n'est plus acceptée par l'API. */
export function UpdateRequired() {
  const c = useColors();
  const insets = useSafeAreaInsets();
  return (
    <View style={{ flex: 1, backgroundColor: c.background, alignItems: 'center', justifyContent: 'center', gap: 16,
      paddingHorizontal: 32, paddingTop: insets.top, paddingBottom: insets.bottom }}>
      <View style={{ width: 88, height: 88, borderRadius: 44, backgroundColor: c.primary, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name="cellphone-arrow-down" size={44} color="#FFFFFF" />
      </View>
      <T v="h2" style={{ textAlign: 'center' }}>Mettez à jour BéninConnect</T>
      <Muted style={{ textAlign: 'center' }}>
        Cette version n'est plus prise en charge. Installez la dernière version pour continuer à utiliser l'application.
      </Muted>
      <Button title="Mettre à jour" icon="download" onPress={() => void Linking.openURL(STORE_URL)} style={{ alignSelf: 'stretch' }} />
      <Muted style={{ fontSize: 12 }}>{`Version installée : ${APP_VERSION}`}</Muted>
    </View>
  );
}
