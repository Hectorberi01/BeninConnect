// Stockage local (préférences) : version de test fournie par la bibliothèque
jest.mock('@react-native-async-storage/async-storage', () => require('@react-native-async-storage/async-storage/jest/async-storage-mock'));

import { activeReviewApi, authRequest, setReviewApi } from '@/data';
import type { DataSource } from '@/data/repositories';
import { AppError, ReviewRedirectError } from '@/data/types';

// Source factice : la production redirige le compte de revue, l'API de revue l'accepte
function call(log: string[]) {
  return async (source: DataSource) => {
    const url = (source as { apiUrl?: string }).apiUrl ?? source.kind;
    log.push(url);
    if (url === 'https://review-api.monbenin.com') return 'jeton-revue';
    throw new ReviewRedirectError('https://review-api.monbenin.com', 'Mettez à jour.');
  };
}

describe('authRequest', () => {
  afterEach(() => setReviewApi(null));

  it('suit la redirection puis reste sur l\'API de revue pour ce compte', async () => {
    const log: string[] = [];
    await expect(authRequest(' AppReview@MonBenin.com ', call(log))).resolves.toBe('jeton-revue');
    expect(log[1]).toBe('https://review-api.monbenin.com');
    expect(activeReviewApi()).toEqual({ url: 'https://review-api.monbenin.com', email: 'appreview@monbenin.com' });
  });

  it('un autre email revient à l\'API normale', async () => {
    setReviewApi({ url: 'https://review-api.monbenin.com', email: 'appreview@monbenin.com' });
    await authRequest('membre@exemple.test', async () => 'ok');
    expect(activeReviewApi()).toBeNull();
  });

  it('une seconde redirection n\'est pas suivie (pas de boucle)', async () => {
    const looping = async () => { throw new ReviewRedirectError('https://review-api.monbenin.com', 'Boucle'); };
    await expect(authRequest('appreview@monbenin.com', looping)).rejects.toBeInstanceOf(AppError);
  });
});
