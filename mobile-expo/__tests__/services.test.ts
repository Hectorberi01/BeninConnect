import { ApiDataSource } from '@/data/api/apiDataSource';
import { CITIES, cityLabel, findCity } from '@/data/cities';
import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';
import { servicePrice } from '@/data/types';

describe('Services', () => {
  it('prix affichés', () => {
    expect(servicePrice({ priceType: 'Quote', priceAmount: null, currency: 'EUR' })).toBe('Sur devis');
    expect(servicePrice({ priceType: 'From', priceAmount: 60, currency: 'EUR' })).toMatch(/^à partir de 60/);
    expect(servicePrice({ priceType: 'Hourly', priceAmount: 15, currency: 'EUR' })).toMatch(/\/ h$/);
    expect(servicePrice({ priceType: 'Fixed', priceAmount: 15000, currency: 'XOF' })).toMatch(/15/);
  });

  it('villes : libellé et recherche', () => {
    expect(findCity('cotonou')?.country).toBe('BJ');
    expect(cityLabel(findCity('Paris')!)).toBe('Paris (France)');
    expect(new Set(CITIES.map(cityLabel)).size).toBe(CITIES.length);
  });

  it('mode démo : recherche, demande, prestation terminée puis avis', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    source.setToken((await source.login(DEMO_EMAIL, DEMO_PASSWORD)).token);
    const paris = findCity('Paris')!;
    const page = await source.searchServices({ lat: paris.lat, lng: paris.lng, radiusKm: 25, sort: 'Distance' });
    expect(page.items.map((p) => p.city)).toEqual(['Paris', 'Montreuil']);
    const provider = await source.getProvider(page.items[0].id);
    expect(provider.phone).toBeNull();
    const request = await source.createServiceRequest(provider.offerings[0].id, 'Bonjour, disponible samedi ?', null, 'Paris');
    await expect(source.reviewServiceRequest(request.id, 5, null)).rejects.toThrow('terminée');
    await source.respondServiceRequest(request.id, 'accept');
    expect((await source.getProvider(provider.id)).phone).not.toBeNull();
    await source.respondServiceRequest(request.id, 'complete');
    await source.reviewServiceRequest(request.id, 5, 'Parfait');
    expect((await source.getServiceRequests('Client'))[0].reviewId).not.toBeNull();
  });

  it('API : recherche et demandes', async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const impl = (async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return { ok: true, status: 200, json: async () => ({ items: [], nextCursor: null, total: 0, hasProfile: false, profile: null }) } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://localhost:5080', impl);
    await api.searchServices({ categoryId: 'c1', lat: 48.85, lng: 2.35, radiusKm: 10, atHome: true, sort: 'Distance' }, 'abc');
    expect(calls[0].url).toBe('http://localhost:5080/api/services/search?categoryId=c1&lat=48.85&lng=2.35&radiusKm=10&atHome=true&sort=Distance&cursor=abc&take=20');
    expect(await api.getMyProvider()).toBeNull();
    await api.respondServiceRequest('r1', 'decline', 'Complet');
    expect(calls[2].url).toBe('http://localhost:5080/api/services/requests/r1/decline');
    expect(JSON.parse(calls[2].init.body as string)).toEqual({ reason: 'Complet' });
  });
});
