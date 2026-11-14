import { formatDay, isoDay, parseDay, placeLabel, tripDay } from '@/data/voyageFormat';
import { ApiDataSource } from '@/data/api/apiDataSource';
import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';
import { kg, tripPrice } from '@/data/types';

describe('Voyages', () => {
  it('dates, lieux et prix', () => {
    expect(parseDay('12/12/2026')).toBe('2026-12-12');
    expect(parseDay('31/02/2026')).toBeUndefined();
    expect(parseDay('')).toBeNull();
    expect(formatDay('2026-12-05')).toBe('05/12/2026');
    expect(tripDay('2026-12-12')).toBe('sam. 12 déc.');
    expect(isoDay(0)).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(placeLabel({ country: 'BJ', city: null })).toBe('Bénin : toutes les villes');
    expect(placeLabel({ country: 'FR', city: 'Lyon' })).toBe('Lyon (France)');
    expect(tripPrice({ pricePerKg: null, currency: 'EUR' })).toBe('Prix à convenir');
    expect(tripPrice({ pricePerKg: 5000, currency: 'XOF' })).toMatch(/FCFA\/kg$/);
    expect(kg(2.5)).toBe('2,5 kg');
  });

  it('mode démo : recherche, réservation, code, avis', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    source.setToken((await source.login(DEMO_EMAIL, DEMO_PASSWORD)).token);
    const page = await source.searchTrips({ from: 'FR', to: 'BJ', toCity: 'cotonou' });
    expect(page.items).toHaveLength(1);
    const trip = page.items[0]!;
    await expect(source.requestBooking(trip.id, { weightKg: 2, category: 'Clothes', description: 'Vêtements', certified: false }))
      .rejects.toThrow('Certifiez');
    const booking = await source.requestBooking(trip.id, { weightKg: 2, category: 'Clothes', description: 'Vêtements pour la famille', certified: true });
    expect((await source.getTrip(trip.id)).canBook).toBe(false);
    const code = await source.issueDeliveryCode(booking.id);
    expect(code.code).toMatch(/^\d{6}$/);
    await source.reviewBooking(booking.id, 5, 'Parfait');
    expect((await source.getMyBookings())[0]!.canReview).toBe(false);
  });

  it('API : adresses et corps des requêtes', async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const impl = (async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return { ok: true, status: 200, json: async () => ({ items: [], nextCursor: null, total: 0 }) } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://localhost:5080', impl);
    await api.searchTrips({ from: 'FR', to: 'BJ', toCity: 'Cotonou', category: 'Documents' }, 'abc');
    expect(calls[0]!.url).toBe('http://localhost:5080/api/trips?from=FR&to=BJ&toCity=Cotonou&category=Documents&cursor=abc&take=20');
    await api.respondBooking('b1', 'decline', 'Plus de place');
    expect(calls[1]!.url).toBe('http://localhost:5080/api/bookings/b1/decline');
    expect(JSON.parse(calls[1]!.init.body as string)).toEqual({ reason: 'Plus de place' });
    await api.deliverBooking('b1', '123456');
    expect(JSON.parse(calls[2]!.init.body as string)).toEqual({ code: '123456' });
    await api.requestBooking('t1', { weightKg: 3, category: 'Clothes', description: 'Vêtements', certified: true });
    expect(calls[3]!.url).toBe('http://localhost:5080/api/trips/t1/bookings');
  });
});
