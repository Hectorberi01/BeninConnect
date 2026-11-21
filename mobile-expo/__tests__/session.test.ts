import { ApiDataSource } from '@/data/api/apiDataSource';
import { setDataSource } from '@/data';
import { onSessionExpired } from '@/data/sessionEvents';
import { useSession } from '@/core/session';
import type { User } from '@/data/types';

jest.mock('@react-native-async-storage/async-storage', () => require('@react-native-async-storage/async-storage/jest/async-storage-mock'));
const mockSecure = new Map<string, string>();
jest.mock('expo-secure-store', () => ({
  getItemAsync: async (k: string) => mockSecure.get(k) ?? null,
  setItemAsync: async (k: string, v: string) => { mockSecure.set(k, v); },
  deleteItemAsync: async (k: string) => { mockSecure.delete(k); },
}));

const USER = { id: 'u1', firstName: 'Ama', lastName: 'K', email: 'ama@exemple.test' } as User;

/** Serveur simulé : réponse fixe, ou panne réseau si status = 0. */
function server(status: number, body?: unknown) {
  return (async () => {
    if (status === 0) throw new TypeError('Network request failed');
    return { ok: status >= 200 && status < 300, status, json: async () => body ?? {} } as Response;
  }) as unknown as typeof fetch;
}

async function signedInWith(fetchImpl: typeof fetch) {
  setDataSource(new ApiDataSource('http://x', server(200, USER)));
  await useSession.getState().signIn({ token: 'jeton', user: USER }, true);
  useSession.setState({ user: null });
  setDataSource(new ApiDataSource('http://x', fetchImpl));
}

describe('session', () => {
  beforeEach(() => mockSecure.clear());

  it('hors ligne : la session est gardée avec le dernier profil connu', async () => {
    await signedInWith(server(0));
    const user = await useSession.getState().restore();
    expect(user?.id).toBe('u1');
    expect(mockSecure.get('beninconnect.token')).toBe('jeton');
  });

  it('serveur en panne (500) : la session est gardée', async () => {
    await signedInWith(server(500));
    expect((await useSession.getState().restore())?.id).toBe('u1');
    expect(mockSecure.get('beninconnect.token')).toBe('jeton');
  });

  it('jeton refusé (401) : la session est effacée', async () => {
    await signedInWith(server(401));
    expect(await useSession.getState().restore()).toBeNull();
    expect(mockSecure.has('beninconnect.token')).toBe(false);
  });

  it('401 pendant l\'utilisation : la fin de session est signalée une seule fois par appel authentifié', async () => {
    const expired = jest.fn();
    onSessionExpired(expired);
    const api = new ApiDataSource('http://x', server(401));
    await expect(api.getFeed(0, 10)).rejects.toMatchObject({ status: 401 });
    expect(expired).not.toHaveBeenCalled(); // pas de jeton : rien à expirer
    api.setToken('jeton');
    await expect(api.getFeed(0, 10)).rejects.toMatchObject({ status: 401 });
    expect(expired).toHaveBeenCalledTimes(1);
    onSessionExpired(null);
  });
});
