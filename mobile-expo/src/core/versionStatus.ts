import type { AppVersionStatus } from '@/data/types';
import { compareVersions } from './version';

/** Entre deux envois de la version installée (sauf changement de compte ou de version). */
export const REPORT_EVERY_MS = 6 * 60 * 60 * 1000;

export interface LastReport {
  at: number;
  userId: string | null;
  version: string;
}

/** Envoi au démarrage, après connexion, puis au retour au premier plan au plus toutes les 6 h. */
export function shouldReport(last: LastReport | null, userId: string | null, version: string, now = Date.now()) {
  return !last || last.userId !== userId || last.version !== version || now - last.at >= REPORT_EVERY_MS;
}

/** Identifiant d'installation (v4) : pas un secret, seulement un repère stable pour ce téléphone. */
export function newInstallationId(random: () => number = Math.random) {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = Math.floor(random() * 16);
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

/** Ancienne route (minimumVersion seulement) : même statut, sans dernière version connue. */
export function legacyStatus(version: string, minimumVersion: string | null | undefined, storeUrl: string): AppVersionStatus {
  const required = !!minimumVersion && compareVersions(version, minimumVersion) < 0;
  return {
    currentVersion: version, latestVersion: null, minimumSupportedVersion: minimumVersion ?? null,
    status: required ? 'UPDATE_REQUIRED' : 'UNKNOWN', forceUpdate: required, storeUrl,
  };
}
