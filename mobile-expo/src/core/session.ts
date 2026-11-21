import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import { create } from 'zustand';
import { activeReviewApi, data, type ReviewApi, setReviewApi } from '@/data';
import type { AuthResult, User } from '@/data/types';

const TOKEN_KEY = 'beninconnect.token';
const REVIEW_KEY = 'beninconnect.reviewApi';
// Dernier profil connu : ouverture de l'app hors ligne
const USER_KEY = 'beninconnect.lastUser';

async function readCachedUser(): Promise<User | null> {
  try {
    const raw = await AsyncStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
  } catch {
    return null;
  }
}

let remembered = false;

function cacheUser(user: User | null) {
  if (user && !remembered) return;
  (user ? AsyncStorage.setItem(USER_KEY, JSON.stringify(user)) : AsyncStorage.removeItem(USER_KEY)).catch(() => undefined);
}

// Actions à faire tant que le jeton est encore valide (ex. désinscription du téléphone aux notifications push).
const signOutHooks: (() => Promise<void>)[] = [];
export function beforeSignOut(hook: () => Promise<void>) {
  signOutHooks.push(hook);
}

interface SessionStore {
  user: User | null;
  signIn: (result: AuthResult, remember: boolean) => Promise<void>;
  update: (user: User) => void;
  signOut: () => Promise<void>;
  /** Restaure la session mémorisée (« Rester connecté »). */
  restore: () => Promise<User | null>;
}

// Jeton stocké dans le trousseau iOS / Keystore Android (expo-secure-store).
async function readToken() {
  try {
    return await SecureStore.getItemAsync(TOKEN_KEY);
  } catch {
    return null;
  }
}

async function writeToken(token: string | null) {
  try {
    if (token) await SecureStore.setItemAsync(TOKEN_KEY, token);
    else await SecureStore.deleteItemAsync(TOKEN_KEY);
  } catch {
    // stockage indisponible (web) : session non mémorisée
  }
}

// API de revue (compte Apple / Google) : gardée avec le jeton pour « Rester connecté »
async function readReview(): Promise<ReviewApi | null> {
  try {
    const raw = await SecureStore.getItemAsync(REVIEW_KEY);
    return raw ? (JSON.parse(raw) as ReviewApi) : null;
  } catch {
    return null;
  }
}

async function writeReview(value: ReviewApi | null) {
  try {
    if (value) await SecureStore.setItemAsync(REVIEW_KEY, JSON.stringify(value));
    else await SecureStore.deleteItemAsync(REVIEW_KEY);
  } catch {
    // stockage indisponible (web)
  }
}

export const useSession = create<SessionStore>((set) => ({
  user: null,
  signIn: async (result, remember) => {
    data().setToken(result.token);
    await writeToken(remember ? result.token : null);
    await writeReview(remember ? activeReviewApi() : null);
    remembered = remember;
    cacheUser(remember ? result.user : null);
    set({ user: result.user });
  },
  update: (user) => set((s) => {
    if (s.user?.id !== user.id) return s;
    cacheUser(user);
    return { user };
  }),
  signOut: async () => {
    await Promise.all(signOutHooks.map((h) => h().catch(() => undefined)));
    data().setToken(null);
    setReviewApi(null);
    await writeToken(null);
    await writeReview(null);
    cacheUser(null);
    remembered = false;
    set({ user: null });
  },
  restore: async () => {
    const token = await readToken();
    if (!token) return null;
    setReviewApi(await readReview());
    remembered = true;
    let user: User | null;
    try {
      user = await data().restore(token);
    } catch {
      // Hors ligne ou serveur indisponible : session gardée avec le dernier profil connu
      user = await readCachedUser();
      if (user) {
        data().setToken(token);
        set({ user });
      }
      return user;
    }
    if (!user) {
      // Jeton refusé (expiré, compte supprimé) : seule raison d'effacer la session
      setReviewApi(null);
      await writeToken(null);
      await writeReview(null);
      cacheUser(null);
      return null;
    }
    data().setToken(token);
    cacheUser(user);
    set({ user });
    return user;
  },
}));

/** Utilisateur connecté (les écrans protégés ne s'affichent qu'avec une session). */
export function useMe(): User {
  const user = useSession((s) => s.user);
  if (!user) throw new Error('Aucune session active.');
  return user;
}
