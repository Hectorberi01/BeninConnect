import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as SecureStore from 'expo-secure-store';
import * as Updates from 'expo-updates';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, Platform } from 'react-native';
import { data } from '@/data';
import type { AppVersionStatus } from '@/data/types';
import { usePreferences } from './preferences';
import { currentPushToken } from './push';
import { useSession } from './session';
import { type LastReport, legacyStatus, newInstallationId, shouldReport } from './versionStatus';

/** Version de l'application (app.json), identique pour toutes les mises à jour à chaud d'une même version. */
export const APP_VERSION = Constants.expoConfig?.version ?? '0.0.0';

export const STORE_URL = Platform.select({
  ios: 'https://apps.apple.com/app/id6832112483',
  default: 'https://play.google.com/store/apps/details?id=com.hbatech.beninconnect.rn',
});

// Une mise à jour téléchargée est appliquée au retour dans l'app après cette absence (sinon au prochain lancement)
const RELOAD_AFTER_BACKGROUND_MS = 10 * 60 * 1000;
const CHECK_EVERY_MS = 30 * 60 * 1000;

/** Mises à jour à chaud (EAS Update) : téléchargées en silence, appliquées sans action de l'utilisateur. */
export function useOtaUpdates() {
  useEffect(() => {
    if (__DEV__ || !Updates.isEnabled) return;
    let ready = false;
    let lastCheck = 0;
    let backgroundSince = 0;

    const check = async () => {
      if (ready || Date.now() - lastCheck < CHECK_EVERY_MS) return;
      lastCheck = Date.now();
      try {
        const { isAvailable } = await Updates.checkForUpdateAsync();
        if (isAvailable) ready = (await Updates.fetchUpdateAsync()).isNew;
      } catch {
        // hors ligne ou serveur indisponible : nouvel essai plus tard
      }
    };

    void check();
    const sub = AppState.addEventListener('change', (state) => {
      if (state === 'background') backgroundSince = Date.now();
      if (state !== 'active') return;
      if (ready && backgroundSince && Date.now() - backgroundSince >= RELOAD_AFTER_BACKGROUND_MS) {
        void Updates.reloadAsync();
        return;
      }
      void check();
    });
    return () => sub.remove();
  }, []);
}

// expo-application est natif : absent d'un binaire plus ancien (mise à jour à chaud), on se replie sur expo-constants
function nativeApplication(): { nativeApplicationVersion: string | null; nativeBuildVersion: string | null } | null {
  try {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    return require('expo-application');
  } catch {
    return null;
  }
}

const INSTALLATION_KEY = 'beninconnect.installationId';

/** Identifiant tiré au premier lancement, gardé dans le trousseau (Keychain / Keystore). */
async function installationId() {
  try {
    const saved = await SecureStore.getItemAsync(INSTALLATION_KEY);
    if (saved) return saved;
    const id = newInstallationId();
    await SecureStore.setItemAsync(INSTALLATION_KEY, id);
    return id;
  } catch {
    return null;
  }
}

export async function getAppVersionInfo() {
  const app = nativeApplication();
  return {
    platform: Platform.OS === 'ios' ? ('ios' as const) : ('android' as const),
    version: app?.nativeApplicationVersion ?? APP_VERSION,
    buildNumber: app?.nativeBuildVersion
      ?? (Platform.OS === 'ios' ? Constants.expoConfig?.ios?.buildNumber : Constants.expoConfig?.android?.versionCode?.toString()) ?? null,
    osVersion: Device.osVersion ?? null,
  };
}

/**
 * Statut de mise à jour : la version installée est envoyée au démarrage, après connexion et au retour
 * au premier plan (au plus toutes les 6 h). Obligatoire : écran bloquant ; disponible : bandeau.
 */
export function useAppVersionStatus() {
  const loaded = usePreferences((s) => s.loaded);
  const apiUrl = usePreferences((s) => s.apiUrl);
  const userId = useSession((s) => s.user?.id ?? null);
  const [status, setStatus] = useState<AppVersionStatus | null>(null);
  const last = useRef<LastReport | null>(null);
  const user = useRef(userId);
  user.current = userId;

  const check = useCallback(async () => {
    if (Platform.OS === 'web') return;
    const info = await getAppVersionInfo();
    if (!shouldReport(last.current, user.current, info.version)) return;
    last.current = { at: Date.now(), userId: user.current, version: info.version };
    try {
      const id = await installationId();
      if (!id) throw new Error('Identifiant indisponible');
      setStatus(await data().reportInstallation({ installationId: id, ...info, pushToken: currentPushToken() }));
    } catch {
      // API plus ancienne ou injoignable : seule la version minimale compte, sans jamais bloquer à tort
      try {
        const res = await fetch(`${apiUrl}/api/app/version?platform=${info.platform}`, { headers: { Accept: 'application/json' } });
        if (res.ok) setStatus(legacyStatus(info.version, ((await res.json()) as { minimumVersion?: string | null }).minimumVersion, STORE_URL));
      } catch {
        last.current = null; // nouvel essai au prochain retour dans l'app
      }
    }
  }, [apiUrl]);

  useEffect(() => {
    if (loaded) void check();
  }, [loaded, userId, check]);

  useEffect(() => {
    const sub = AppState.addEventListener('change', (s) => { if (s === 'active' && loaded) void check(); });
    return () => sub.remove();
  }, [loaded, check]);

  return status;
}
