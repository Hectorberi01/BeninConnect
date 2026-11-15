import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Linking from 'expo-linking';
import { useEffect, useState } from 'react';
import { Pressable, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { STORE_URL } from '@/core/appUpdates';
import { useColors } from '@/core/theme';
import type { AppVersionStatus } from '@/data/types';
import { Icon, Muted, T } from './ui';

const DISMISSED_KEY = 'beninconnect.updateDismissed';

/** Nouvelle version conseillée (non obligatoire) : bandeau discret, masqué jusqu'à la version suivante une fois fermé. */
export function UpdateAvailableBanner({ status }: { status: AppVersionStatus }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const latest = status.latestVersion ?? '';
  const [hidden, setHidden] = useState(true);

  useEffect(() => {
    AsyncStorage.getItem(DISMISSED_KEY).then((v) => setHidden(v === latest)).catch(() => setHidden(false));
  }, [latest]);

  if (hidden) return null;
  const dismiss = () => {
    setHidden(true);
    AsyncStorage.setItem(DISMISSED_KEY, latest).catch(() => undefined);
  };

  return (
    <View pointerEvents="box-none" style={{ position: 'absolute', left: 12, right: 12, top: insets.top + 8 }}>
      <Pressable accessibilityRole="button" onPress={() => void Linking.openURL(status.storeUrl || STORE_URL)}
        style={{ flexDirection: 'row', alignItems: 'center', gap: 12, padding: 14, borderRadius: 16, backgroundColor: c.surface,
          borderWidth: 1, borderColor: c.border, boxShadow: '0 8px 24px rgba(15,42,34,0.18)' }}>
        <View style={{ width: 36, height: 36, borderRadius: 18, backgroundColor: c.primaryLight, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="cellphone-arrow-down" size={20} color={c.primary} />
        </View>
        <View style={{ flex: 1 }}>
          <T v="label">{latest ? `BéninConnect ${latest} est disponible` : 'Nouvelle version disponible'}</T>
          <Muted style={{ fontSize: 13 }}>Touchez pour mettre à jour</Muted>
        </View>
        <Pressable accessibilityLabel="Fermer" hitSlop={12} onPress={dismiss}>
          <Icon name="close" size={20} color={c.textSecondary} />
        </Pressable>
      </Pressable>
    </View>
  );
}
