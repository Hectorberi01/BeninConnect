import { imageSource } from '@/core/images';
import { usePreferences } from '@/core/preferences';
import { ApiDataSource } from '@/data/api/apiDataSource';
import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';

jest.mock('@react-native-async-storage/async-storage', () => require('@react-native-async-storage/async-storage/jest/async-storage-mock'));

describe('photos', () => {
  it("source d'image selon le chemin", () => {
    usePreferences.setState({ apiUrl: 'http://192.168.1.10:5080/' });
    expect(imageSource('Posts/post_01.jpg')).toBeDefined();
    expect(imageSource('uploads/abc.jpg')).toMatchObject({ uri: 'http://192.168.1.10:5080/uploads/abc.jpg' });
    expect(imageSource('file:///tmp/photo.jpg')).toEqual({ uri: 'file:///tmp/photo.jpg' });
    expect(imageSource('inconnu.jpg')).toBeUndefined();
    expect(imageSource(null)).toBeUndefined();
  });

  it('démo : photo de profil et couverture, puis retrait', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    const { token, user } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
    source.setToken(token);
    const path = await source.uploadImage({ uri: 'file:///photo.jpg' });
    const base = { firstName: user.firstName, lastName: user.lastName, profession: user.profession, country: user.country, city: user.city, bio: user.bio, interests: user.interests };
    const updated = await source.updateProfile({ ...base, avatarImage: path, coverImage: path });
    expect([updated.avatarImage, updated.coverImage]).toEqual([path, path]);
    const kept = await source.updateProfile({ ...base, avatarImage: '' });
    expect([kept.avatarImage, kept.coverImage]).toEqual([null, path]);
  });

  it('API : envoi multipart sans en-tête JSON', async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const fetchImpl = (async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return { ok: true, status: 200, json: async () => ({ path: 'uploads/0123.jpg' }) } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://x', fetchImpl, 15_000, fetchImpl);
    expect(await api.uploadImage({ uri: 'file:///p.jpg', mimeType: 'image/jpeg' })).toBe('uploads/0123.jpg');
    expect(calls[0].url).toBe('http://x/api/media');
    expect(calls[0].init.body).toBeInstanceOf(FormData);
    expect((calls[0].init.headers as Record<string, string>)['Content-Type']).toBeUndefined();
  });
});
