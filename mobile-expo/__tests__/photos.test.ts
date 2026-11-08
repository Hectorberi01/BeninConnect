import { imageSource } from '@/core/images';
import { usePreferences } from '@/core/preferences';
import { ApiDataSource } from '@/data/api/apiDataSource';
import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';
import { messagePreview } from '@/data/types';

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

describe('modifications', () => {
  it('démo : modifier sa publication, modifier puis supprimer son message', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    const { token, user } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
    source.setToken(token);

    const post = await source.createPost({ content: 'Première version', communityId: null, imagePath: null, visibility: 'Public' });
    const edited = await source.updatePost(post.id, { content: 'Version corrigée', imagePath: null, visibility: 'OnlyMe' });
    expect([edited.content, edited.visibility, !!edited.editedAt]).toEqual(['Version corrigée', 'OnlyMe', true]);
    const other = (await source.getFeed(0, 50)).items.find((p) => p.authorId !== user.id)!;
    await expect(source.updatePost(other.id, { content: 'Piraté', imagePath: null, visibility: 'Public' })).rejects.toMatchObject({ status: 403 });

    const conversation = (await source.getConversations())[0].conversation;
    const sent = await source.sendMessage(conversation.id, 'Bonjour');
    const changed = await source.editMessage(conversation.id, sent.id, 'Bonjour à toi');
    expect([changed.content, !!changed.editedAt]).toEqual(['Bonjour à toi', true]);
    const removed = await source.deleteMessage(conversation.id, sent.id);
    expect([removed.isDeleted, removed.content]).toEqual([true, '']);
    await expect(source.editMessage(conversation.id, sent.id, 'Retour')).rejects.toMatchObject({ status: 400 });
    const received = (await source.getMessages(conversation.id)).find((m) => m.senderId !== user.id)!;
    await expect(source.deleteMessage(conversation.id, received.id)).rejects.toMatchObject({ status: 403 });
  });

  it('API : PUT de la publication, PUT et DELETE du message', async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const fetchImpl = (async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return { ok: true, status: 200, json: async () => ({}) } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://x/', fetchImpl);
    await api.updatePost('p1', { content: 'Texte', imagePath: null, visibility: 'Public' });
    await api.editMessage('c1', 'm1', 'Corrigé');
    await api.deleteMessage('c1', 'm1');
    expect(calls.map((c) => `${c.init.method} ${c.url}`)).toEqual([
      'PUT http://x/api/posts/p1', 'PUT http://x/api/conversations/c1/messages/m1', 'DELETE http://x/api/conversations/c1/messages/m1',
    ]);
    expect(api.apiUrl).toBe('http://x');
  });
});

describe('pièces jointes', () => {
  it('démo : envoi d\'un PDF puis aperçu et suppression', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    const { token } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
    source.setToken(token);
    const conversation = (await source.getConversations())[0].conversation;
    const sent = await source.sendAttachment(conversation.id, { uri: 'file:///devis.pdf', name: 'devis.pdf', mimeType: 'application/pdf' });
    expect(sent.attachment).toMatchObject({ kind: 'File', fileName: 'devis.pdf' });
    expect(source.attachmentUrl(sent)).toBe('file:///devis.pdf');
    expect(messagePreview(sent)).toBe('📄 devis.pdf');
    await expect(source.sendAttachment(conversation.id, { uri: 'file:///a.exe', name: 'a.exe', mimeType: 'application/x-msdownload' }))
      .rejects.toMatchObject({ status: 400 });
    const removed = await source.deleteMessage(conversation.id, sent.id);
    expect([removed.attachment, source.attachmentUrl(removed), messagePreview(removed)]).toEqual([null, null, 'Message supprimé']);
  });

  it('API : envoi multipart et URL de téléchargement avec jeton', async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const upload = (async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return { ok: true, status: 201, json: async () => ({ id: 'm1', attachment: { kind: 'Audio' } }) } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://x', upload, 15_000, upload);
    api.setToken('jeton+/=');
    await api.sendAttachment('c1', { uri: 'file:///v.m4a', name: 'v.m4a', mimeType: 'audio/mp4', durationMs: 4200.6 });
    expect(calls[0].url).toBe('http://x/api/conversations/c1/attachments');
    expect(calls[0].init.body).toBeInstanceOf(FormData);
    expect(api.attachmentUrl({ id: 'm1', attachment: { kind: 'Audio' } } as never)).toBe('http://x/api/messages/m1/attachment?access_token=jeton%2B%2F%3D');
    expect(api.attachmentUrl({ id: 'm2' } as never)).toBeNull();
  });
});
