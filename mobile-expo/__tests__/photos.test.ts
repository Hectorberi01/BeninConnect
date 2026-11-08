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

describe('suppression', () => {
  it('démo : seul l\'auteur supprime sa publication, puis le compte est supprimé', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    const { token, user } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
    source.setToken(token);
    const mine = await source.createPost({ content: 'Publication à supprimer', visibility: 'Public' } as never);
    const other = (await source.getFeed(0, 50)).items.find((p) => p.authorId !== user.id)!;
    await expect(source.deletePost(other.id)).rejects.toMatchObject({ status: 403 });
    await source.deletePost(mine.id);
    await expect(source.getPost(mine.id)).rejects.toMatchObject({ status: 404 });

    await expect(source.deleteAccount('mauvais')).rejects.toMatchObject({ status: 400 });
    await source.deleteAccount(DEMO_PASSWORD);
    await expect(source.login(DEMO_EMAIL, DEMO_PASSWORD)).rejects.toBeDefined();
  });

  it('API : DELETE de la publication et POST de suppression du compte', async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const fetchImpl = (async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return { ok: true, status: 204, json: async () => undefined } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://x', fetchImpl);
    await api.deletePost('p1');
    await api.deleteAccount('Secret123!');
    expect([calls[0].init.method, calls[0].url]).toEqual(['DELETE', 'http://x/api/posts/p1']);
    expect([calls[1].init.method, calls[1].url, calls[1].init.body]).toEqual(['POST', 'http://x/api/users/me/delete', JSON.stringify({ password: 'Secret123!' })]);
  });
});

describe('signalement', () => {
  it('démo : signaler un contenu une seule fois, jamais le sien', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    const { token, user } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
    source.setToken(token);
    const posts = (await source.getFeed(0, 50)).items;
    const other = posts.find((p) => p.authorId !== user.id)!;
    const mine = posts.find((p) => p.authorId === user.id)!;
    await source.report('Post', other.id, 'Spam');
    await expect(source.report('Post', other.id, 'Spam')).rejects.toMatchObject({ status: 409 });
    await expect(source.report('Post', mine.id, 'Spam')).rejects.toMatchObject({ status: 400 });
  });

  it('API : POST /api/reports', async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const fetchImpl = (async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return { ok: true, status: 200, json: async () => ({ id: 'r1' }) } as Response;
    }) as unknown as typeof fetch;
    await new ApiDataSource('http://x', fetchImpl).report('Comment', 'c1', 'Harassment');
    expect(calls[0].url).toBe('http://x/api/reports');
    expect(JSON.parse(calls[0].init.body as string)).toEqual({ targetType: 'Comment', targetId: 'c1', reason: 'Harassment', details: null });
  });
});
