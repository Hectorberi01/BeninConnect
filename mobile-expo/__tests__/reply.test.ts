import { ApiDataSource } from '@/data/api/apiDataSource';
import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';

describe('Réponse à un message', () => {
  it('API : replyToId envoyé seulement pour une réponse', async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const impl = (async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return { ok: true, status: 200, json: async () => ({}) } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://localhost:5080', impl);
    await api.sendMessage('c1', 'Bonjour');
    await api.sendMessage('c1', 'Oui', 'm1');
    expect(JSON.parse(calls[0]!.init.body as string)).toEqual({ content: 'Bonjour' });
    expect(JSON.parse(calls[1]!.init.body as string)).toEqual({ content: 'Oui', replyToId: 'm1' });
  });

  it('mode démo : la citation reprend le message cité', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    source.setToken((await source.login(DEMO_EMAIL, DEMO_PASSWORD)).token);
    const conversation = (await source.getConversations())[0]!.conversation;
    const first = await source.sendMessage(conversation.id, 'On se voit à quelle heure ?');
    const reply = await source.sendMessage(conversation.id, 'À 18 h', first.id);
    expect(reply.replyTo).toMatchObject({ id: first.id, excerpt: 'On se voit à quelle heure ?', isDeleted: false });
    await expect(source.sendMessage(conversation.id, 'Test', 'inconnu')).rejects.toThrow('introuvable');
  });
});
