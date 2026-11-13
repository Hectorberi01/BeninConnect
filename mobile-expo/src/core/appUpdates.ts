import Constants from 'expo-constants';
import * as Updates from 'expo-updates';
import { useEffect, useState } from 'react';
import { AppState, Platform } from 'react-native';
import { usePreferences } from './preferences';
import { compareVersions } from './version';

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

/** Vrai quand l'API exige une version plus récente (écran « Mettez à jour »). */
export function useUpdateRequired() {
  const apiUrl = usePreferences((s) => s.apiUrl);
  const [required, setRequired] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const check = async () => {
      try {
        const res = await fetch(`${apiUrl}/api/app/version`, { headers: { Accept: 'application/json' } });
        if (!res.ok) return;
        const { minimumVersion } = (await res.json()) as { minimumVersion?: string | null };
        if (!cancelled) setRequired(!!minimumVersion && compareVersions(APP_VERSION, minimumVersion) < 0);
      } catch {
        // API injoignable : on ne bloque jamais l'utilisateur
      }
    };
    void check();
    const sub = AppState.addEventListener('change', (s) => { if (s === 'active') void check(); });
    return () => { cancelled = true; sub.remove(); };
  }, [apiUrl]);

  return required;
}
