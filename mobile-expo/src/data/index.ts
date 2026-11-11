import { DEV_TOOLS, PRODUCTION_API_URL } from '@/core/config';
import { usePreferences } from '@/core/preferences';
import { ApiDataSource } from './api/apiDataSource';
import { MockDataSource } from './mock/mockDataSource';
import type { DataSource } from './repositories';
import { ReviewRedirectError } from './types';

let instance: DataSource | null = null;

/** Compte de revue Apple / Google : API de revue suivie jusqu'à la déconnexion. */
export interface ReviewApi {
  url: string;
  email: string;
}

let review: ReviewApi | null = null;

export const activeReviewApi = () => review;

/** Bascule vers l'API de revue (ou revient à l'API normale avec null) : la source est recréée. */
export function setReviewApi(next: ReviewApi | null) {
  if (next?.url === review?.url && next?.email === review?.email) return;
  review = next;
  instance = null;
}

/**
 * Connexion, inscription, code et mot de passe oublié : suit la redirection d'un compte de revue,
 * puis refait l'appel sur l'API de revue. Un autre email revient à l'API normale.
 */
export async function authRequest<T>(email: string, call: (source: DataSource) => Promise<T>): Promise<T> {
  const normalized = email.trim().toLowerCase();
  if (review && review.email !== normalized) setReviewApi(null);
  try {
    return await call(getDataSource());
  } catch (e) {
    if (!(e instanceof ReviewRedirectError) || review) throw e;
    setReviewApi({ url: e.url, email: normalized });
    return call(getDataSource());
  }
}

/**
 * Source de données choisie au démarrage (Paramètres › Source de données).
 * Changer de source demande de relancer l'application, comme la version Avalonia.
 */
export function getDataSource(): DataSource {
  if (!instance) {
    const prefs = usePreferences.getState();
    if (review) return (instance = new ApiDataSource(review.url, undefined, undefined, undefined, { review: true }));
    // Production : toujours l'API de production, quels que soient les réglages enregistrés.
    if (!DEV_TOOLS) return (instance = new ApiDataSource(PRODUCTION_API_URL));
    instance = prefs.dataSource === 'api'
      ? new ApiDataSource(prefs.apiUrl)
      : new MockDataSource({ latencyMs: 180, simulateErrors: () => usePreferences.getState().simulateErrors });
  }
  return instance;
}

/** Tests : injecte une source. */
export function setDataSource(source: DataSource | null) {
  instance = source;
}

export const data = () => getDataSource();
