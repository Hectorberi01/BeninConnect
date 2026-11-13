import { ApiDataSource } from '@/data/api/apiDataSource';
import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';

describe('Emploi', () => {
  it('mode démo : recherche, filtres, favoris et signalement unique', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    source.setToken((await source.login(DEMO_EMAIL, DEMO_PASSWORD)).token);

    const all = await source.searchJobs({});
    expect(all.items.length).toBeGreaterThan(0);
    expect((await source.searchJobs({ country: 'BJ' })).items.every((j) => j.country === 'BJ')).toBe(true);
    expect((await source.searchJobs({ contract: 'Alternance' })).items).toHaveLength(1);
    expect((await source.getJobFilters()).countries.map((c) => c.label)).toContain('FR');

    const id = all.items[0].id;
    await source.setJobFavorite(id, true);
    expect((await source.getFavoriteJobs()).map((j) => j.id)).toEqual([id]);
    await source.setJobFavorite(id, false);
    expect(await source.getFavoriteJobs()).toHaveLength(0);

    await source.reportJob(id, 'Arnaque');
    expect((await source.getJob(id)).reportedByMe).toBe(true);
    await expect(source.reportJob(id, 'Encore')).rejects.toThrow('déjà signalé');
  });

  it('API : filtres et curseur dans l’adresse', async () => {
    const urls: string[] = [];
    const impl = (async (url: string) => {
      urls.push(url);
      return { ok: true, status: 200, json: async () => ({ items: [], nextCursor: null }) } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://localhost:5080', impl);
    await api.searchJobs({ q: 'dev', contract: 'Cdi', remote: true }, 'abc');
    expect(urls[0]).toBe('http://localhost:5080/api/jobs?q=dev&contract=Cdi&remote=true&cursor=abc&take=20');
  });
});
