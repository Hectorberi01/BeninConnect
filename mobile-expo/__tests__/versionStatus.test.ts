import { legacyStatus, newInstallationId, REPORT_EVERY_MS, shouldReport } from '@/core/versionStatus';

describe('envoi de la version installée', () => {
  const last = { at: 1_000, userId: null, version: '1.2.0' };

  it('au démarrage, après connexion, puis au plus toutes les 6 h', () => {
    expect(shouldReport(null, null, '1.2.0', 1_000)).toBe(true);
    expect(shouldReport(last, null, '1.2.0', 1_000 + 60_000)).toBe(false);
    expect(shouldReport(last, 'membre', '1.2.0', 1_000 + 60_000)).toBe(true); // connexion
    expect(shouldReport(last, null, '1.3.0', 1_000 + 60_000)).toBe(true); // mise à jour installée
    expect(shouldReport(last, null, '1.2.0', 1_000 + REPORT_EVERY_MS)).toBe(true);
  });

  it('identifiant au format UUID v4', () => {
    expect(newInstallationId()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(newInstallationId()).not.toBe(newInstallationId());
  });

  it('ancienne route : obligatoire seulement sous le minimum', () => {
    expect(legacyStatus('1.0.0', '1.1.0', 'url')).toMatchObject({ status: 'UPDATE_REQUIRED', forceUpdate: true });
    expect(legacyStatus('1.1.0', '1.1.0', 'url').status).toBe('UNKNOWN');
    expect(legacyStatus('1.0.0', null, 'url').forceUpdate).toBe(false);
  });
});
