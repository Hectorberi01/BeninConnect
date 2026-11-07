import { DEV_TOOLS, PRODUCTION_API_URL } from '@/core/config';
import { usePreferences } from '@/core/preferences';
import { ApiDataSource } from './api/apiDataSource';
import { MockDataSource } from './mock/mockDataSource';
import type { DataSource } from './repositories';

let instance: DataSource | null = null;

/**
 * Source de données choisie au démarrage (Paramètres › Source de données).
 * Changer de source demande de relancer l'application, comme la version Avalonia.
 */
export function getDataSource(): DataSource {
  if (!instance) {
    const prefs = usePreferences.getState();
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
