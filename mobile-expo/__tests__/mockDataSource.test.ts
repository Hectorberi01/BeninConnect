import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';
import { AppError } from '@/data/types';

async function signedIn() {
  const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
  const { token, user } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
  source.setToken(token);
  return { source, me: user };
}

describe('MockDataSource', () => {
  it('jeu de données de démonstration', async () => {
    const { source } = await signedIn();
    expect(source.store.users).toHaveLength(40);
    expect(source.store.communities).toHaveLength(15);
    expect(source.store.events).toHaveLength(25);
  });

  it('connexion : erreurs et restauration de session', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    await expect(source.login(DEMO_EMAIL, 'mauvais1')).rejects.toThrow('Email ou mot de passe incorrect.');
    const { token, user } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
    expect(await source.restore(token)).toMatchObject({ id: user.id });
    expect(await source.restore('demo:inconnu')).toBeNull();
  });

  it('sans session : erreur 401', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    await expect(source.getFeed(0, 10)).rejects.toMatchObject({ status: 401 });
  });

  it('inscription puis vérification', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
    const r = { firstName: 'Ana', lastName: 'Test', email: 'ana@test.dev', country: 'France', city: 'Lyon', password: 'Motdepasse1' };
    await source.register(r);
    await expect(source.login(r.email, r.password)).rejects.toThrow(/Vérifiez/);
    await expect(source.verifyEmail(r.email, '000000')).rejects.toBeInstanceOf(AppError);
    const result = await source.verifyEmail(r.email, source.demoVerificationCode);
    expect(result.user.email).toBe(r.email);
  });

  it('fil paginé sans doublon', async () => {
    const { source } = await signedIn();
    const first = await source.getFeed(0, 10);
    const second = await source.getFeed(10, 10);
    expect(first.items).toHaveLength(10);
    expect(first.totalCount).toBeGreaterThan(20);
    expect(new Set([...first.items, ...second.items].map((p) => p.id)).size).toBe(20);
  });

  it("j'aime et commentaire", async () => {
    const { source, me } = await signedIn();
    const post = (await source.getFeed(0, 1)).items[0];
    const liked = await source.toggleLike(post.id);
    expect(liked.likedBy.includes(me.id)).toBe(!post.likedBy.includes(me.id));
    await source.addComment(post.id, 'Super initiative !');
    expect((await source.getPost(post.id)).commentCount).toBe(post.commentCount + 1);
  });

  it('adhésion : publique immédiate, privée en attente', async () => {
    const { source, me } = await signedIn();
    const others = source.store.communities.filter((c) => !c.memberIds.includes(me.id));
    const open = others.find((c) => c.visibility === 'Public')!;
    const closed = others.find((c) => c.visibility === 'Private')!;
    expect(await source.joinCommunity(open.id)).toBe('Member');
    await source.cancelJoinRequest(closed.id);
    expect(await source.getMyJoinRequests()).not.toContain(closed.id);
    expect(await source.joinCommunity(closed.id)).toBe('Pending');
    expect(await source.getMyJoinRequests()).toContain(closed.id);
    expect(await source.leaveCommunity(open.id)).toBe('None');
  });

  it('messagerie : envoi, réponse simulée, lecture', async () => {
    const { source, me } = await signedIn();
    const [summary] = await source.getConversations();
    await source.sendMessage(summary.conversation.id, 'Salut !');
    const reply = await source.simulateReply(summary.conversation.id, 'Réponse de démonstration');
    expect(reply.isSimulated).toBe(true);
    expect(reply.senderId).not.toBe(me.id);
    await source.markConversationRead(summary.conversation.id);
    expect((await source.getConversation(summary.conversation.id)).unreadCount).toBe(0);
  });

  it('participation à un événement', async () => {
    const { source, me } = await signedIn();
    const event = (await source.getEvents({})).find((e) => !e.participantIds.includes(me.id) && (e.capacity == null || e.participantIds.length < e.capacity))!;
    const joined = await source.toggleParticipation(event.id);
    expect(joined.isParticipating).toBe(true);
    const left = await source.toggleParticipation(event.id);
    expect(left.isParticipating).toBe(false);
  });

  it('notifications : tout marquer comme lu', async () => {
    const { source } = await signedIn();
    expect(await source.getUnreadNotificationCount()).toBeGreaterThan(0);
    await source.markAllNotificationsRead();
    expect(await source.getUnreadNotificationCount()).toBe(0);
  });

  it('erreurs simulées', async () => {
    const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => true });
    const random = jest.spyOn(Math, 'random').mockReturnValue(0.1);
    await expect(source.login(DEMO_EMAIL, DEMO_PASSWORD)).rejects.toThrow(/Connexion simulée/);
    random.mockRestore();
  });
});
