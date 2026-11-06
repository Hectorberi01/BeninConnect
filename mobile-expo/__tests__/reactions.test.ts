import { ApiDataSource } from '@/data/api/apiDataSource';
import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';
import { groupReactions, toggleReaction } from '@/data/types';

async function signedIn() {
  const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
  const { token, user } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
  source.setToken(token);
  return { source, me: user };
}

describe('réactions', () => {
  it('ajout, remplacement, retrait (une par membre)', () => {
    const a = toggleReaction([], 'u1', '❤️');
    const b = toggleReaction(a, 'u1', '😂');
    expect(b).toEqual([{ userId: 'u1', emoji: '😂' }]);
    expect(toggleReaction(b, 'u1', '😂')).toEqual([]);
  });

  it('regroupement par emoji', () => {
    const groups = groupReactions([{ userId: 'a', emoji: '❤️' }, { userId: 'me', emoji: '❤️' }, { userId: 'b', emoji: '👍' }], 'me');
    expect(groups).toEqual([{ emoji: '❤️', count: 2, mine: true }, { emoji: '👍', count: 1, mine: false }]);
  });

  it('réagir à un commentaire notifie son auteur', async () => {
    const { source, me } = await signedIn();
    const comment = source.store.comments.find((c) => c.authorId !== me.id && source.store.posts.some((p) => p.id === c.postId && p.visibility === 'Public'))!;
    const before = source.store.notifications.filter((n) => n.userId === comment.authorId).length;
    const reacted = await source.reactToComment(comment.postId, comment.id, '🙏');
    expect(reacted.reactions).toContainEqual({ userId: me.id, emoji: '🙏' });
    expect(source.store.notifications.filter((n) => n.userId === comment.authorId)).toHaveLength(before + 1);
    const removed = await source.reactToComment(comment.postId, comment.id, '🙏');
    expect(removed.reactions.some((r) => r.userId === me.id)).toBe(false);
    await expect(source.reactToComment(comment.postId, comment.id, '🍕')).rejects.toThrow("Cette réaction n'est pas disponible.");
  });

  it('réagir à un message de sa conversation', async () => {
    const { source, me } = await signedIn();
    const [summary] = await source.getConversations();
    const [message] = await source.getMessages(summary.conversation.id);
    const reacted = await source.reactToMessage(summary.conversation.id, message.id, '👍');
    expect(reacted.reactions.filter((r) => r.userId === me.id)).toEqual([{ userId: me.id, emoji: '👍' }]);
  });

  it('jeu de démonstration avec quelques réactions', async () => {
    const { source } = await signedIn();
    expect(source.store.comments.some((c) => c.reactions.length > 0)).toBe(true);
    expect(source.store.messages.some((m) => m.reactions.length > 0)).toBe(true);
  });

  it('routes API', async () => {
    const calls: string[] = [];
    const fetchImpl = (async (url: string) => {
      calls.push(url);
      return { ok: true, status: 200, json: async () => ({}) } as Response;
    }) as unknown as typeof fetch;
    const api = new ApiDataSource('http://x', fetchImpl);
    await api.reactToComment('p1', 'c1', '❤️');
    await api.reactToMessage('k1', 'm1', '👍');
    expect(calls).toEqual(['http://x/api/posts/p1/comments/c1/reactions', 'http://x/api/conversations/k1/messages/m1/reactions']);
  });
});
