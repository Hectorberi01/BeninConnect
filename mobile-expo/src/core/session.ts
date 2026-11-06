import * as SecureStore from 'expo-secure-store';
import { create } from 'zustand';
import { data } from '@/data';
import type { AuthResult, User } from '@/data/types';

const TOKEN_KEY = 'beninconnect.token';

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

export const useSession = create<SessionStore>((set) => ({
  user: null,
  signIn: async (result, remember) => {
    data().setToken(result.token);
    await writeToken(remember ? result.token : null);
    set({ user: result.user });
  },
  update: (user) => set((s) => (s.user?.id === user.id ? { user } : s)),
  signOut: async () => {
    data().setToken(null);
    await writeToken(null);
    set({ user: null });
  },
  restore: async () => {
    const token = await readToken();
    if (!token) return null;
    const user = await data().restore(token).catch(() => null);
    if (!user) {
      await writeToken(null);
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
