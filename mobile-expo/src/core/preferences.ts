import AsyncStorage from '@react-native-async-storage/async-storage';
import { create } from 'zustand';

export type ThemePreference = 'light' | 'dark' | 'system';
export type DataSourceKind = 'demo' | 'api';

export interface Preferences {
  loaded: boolean;
  theme: ThemePreference;
  onboardingCompleted: boolean;
  rememberMe: boolean;
  dataSource: DataSourceKind;
  apiUrl: string;
  privateProfile: boolean;
  showCity: boolean;
  allowMessages: boolean;
  notifySocial: boolean;
  notifyCommunities: boolean;
  notifyMessages: boolean;
  notifyEvents: boolean;
  simulateErrors: boolean;
}

// API exposée par le tunnel ngrok (HTTPS : fonctionne sur l'iPhone hors du Wi‑Fi du Mac).
export const DEFAULT_API_URL = 'https://unstormy-lorriane-poikiloblastic.ngrok-free.dev';
const OLD_DEFAULT_API_URLS = ['http://localhost:5080'];

const defaults: Omit<Preferences, 'loaded'> = {
  theme: 'light',
  onboardingCompleted: false,
  rememberMe: true,
  dataSource: 'demo',
  apiUrl: DEFAULT_API_URL,
  privateProfile: false,
  showCity: true,
  allowMessages: true,
  notifySocial: true,
  notifyCommunities: true,
  notifyMessages: true,
  notifyEvents: true,
  simulateErrors: false,
};

const KEY = 'beninconnect.preferences';

interface PreferencesStore extends Preferences {
  load: () => Promise<void>;
  set: (patch: Partial<Omit<Preferences, 'loaded'>>) => void;
}

/** Préférences locales (thème, onboarding, source de données...). Aucun secret ici. */
export const usePreferences = create<PreferencesStore>((set, get) => ({
  ...defaults,
  loaded: false,
  load: async () => {
    try {
      const raw = await AsyncStorage.getItem(KEY);
      const saved = raw ? JSON.parse(raw) : {};
      // Ancienne adresse par défaut enregistrée : remplacée par la nouvelle.
      if (OLD_DEFAULT_API_URLS.includes(saved.apiUrl)) saved.apiUrl = DEFAULT_API_URL;
      set({ ...defaults, ...saved, loaded: true });
    } catch {
      set({ ...defaults, loaded: true });
    }
  },
  set: (patch) => {
    set(patch);
    const { load: _load, set: _set, loaded: _loaded, ...values } = { ...get(), ...patch };
    AsyncStorage.setItem(KEY, JSON.stringify(values)).catch(() => undefined);
  },
}));
