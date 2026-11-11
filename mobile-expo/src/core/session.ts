import * as SecureStore from 'expo-secure-store';
import { create } from 'zustand';
import { activeReviewApi, data, type ReviewApi, setReviewApi } from '@/data';
import type { AuthResult, User } from '@/data/types';

const TOKEN_KEY = 'beninconnect.token';
const REVIEW_KEY = 'beninconnect.reviewApi';

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
    set({ user: result.user });
  },
  update: (user) => set((s) => (s.user?.id === user.id ? { user } : s)),
  signOut: async () => {
    await Promise.all(signOutHooks.map((h) => h().catch(() => undefined)));
    data().setToken(null);
    setReviewApi(null);
    await writeToken(null);
    await writeReview(null);
    set({ user: null });
  },
  restore: async () => {
    const token = await readToken();
    if (!token) return null;
    setReviewApi(await readReview());
    const user = await data().restore(token).catch(() => null);
    if (!user) {
      setReviewApi(null);
      await writeToken(null);
      await writeReview(null);
      return null;
    }
    data().setToken(token);
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
