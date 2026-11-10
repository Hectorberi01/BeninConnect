import { MockDataSource } from '@/data/mock/mockDataSource';
import { DEMO_EMAIL, DEMO_PASSWORD } from '@/data/repositories';
import { extractTags, insertMention, keptMentions, mentionQuery, parseSocialText } from '@/data/social';
import type { User } from '@/data/types';

const kofi = { id: 'u1', firstName: 'Kofi', lastName: 'Mensah' } as User;
const ama = { id: 'u2', firstName: 'Ama', lastName: 'Kossi' } as User;

describe('Texte social', () => {
  it('hashtags : extraits sans # en minuscules, sans doublon', () => {
    expect(extractTags('Fête à #Cotonou ce soir #cotonou #Bénin2026 #a mail@x.com#non')).toEqual(['cotonou', 'bénin2026']);
  });

  it('découpe texte, mentions connues et hashtags', () => {
    const parts = parseSocialText('Merci @Kofi Mensah et @Inconnu pour #Ouidah !', [kofi]);
    expect(parts).toEqual([
      { kind: 'text', text: 'Merci ' },
      { kind: 'mention', text: '@Kofi Mensah', userId: 'u1' },
      { kind: 'text', text: ' et @Inconnu pour ' },
      { kind: 'tag', text: '#Ouidah', tag: 'ouidah' },
      { kind: 'text', text: ' !' },
    ]);
  });

  it('saisie de mention : requête, insertion, mentions conservées', () => {
    expect(mentionQuery('Salut @Ko', 9)).toEqual({ query: 'Ko', start: 6 });
    expect(mentionQuery('mail@ko', 7)).toBeNull();
    const r = insertMention('Salut @Ko', 9, kofi);
    expect(r).toEqual({ text: 'Salut @Kofi Mensah ', cursor: 19 });
    expect(keptMentions(`${r.text}et Ama`, [kofi, ama])).toEqual(['u1']);
  });
});

async function signedIn() {
  const source = new MockDataSource({ latencyMs: 0, simulateErrors: () => false });
  const { token, user } = await source.login(DEMO_EMAIL, DEMO_PASSWORD);
  source.setToken(token);
  return { source, me: user };
}

describe('MockDataSource : lot social', () => {
  it('carrousel, mentions, hashtag', async () => {
    const { source, me } = await signedIn();
    const friend = source.store.users.find((u) => u.id !== me.id)!;
    const images = ['Posts/post_01.jpg', 'Posts/post_02.jpg'];
    const post = await source.createPost({
      content: 'Sortie à #GrandPopo', communityId: null, imagePath: null, visibility: 'Public', images, mentions: [friend.id, me.id, 'inconnu'],
    });
    expect(post.images).toEqual(images);
    expect(post.imagePath).toBe(images[0]);
    expect(post.mentionIds).toEqual([friend.id]);
    expect((await source.getPosts({ tag: 'grandpopo' })).map((p) => p.id)).toEqual([post.id]);
    await expect(source.createPost({ content: 'Trop de photos', communityId: null, imagePath: null, visibility: 'Public',
      images: Array.from({ length: 11 }, () => 'Posts/post_01.jpg') })).rejects.toMatchObject({ status: 400 });
  });

  it('enregistrements et épingles (3 au plus)', async () => {
    const { source, me } = await signedIn();
    const other = (await source.getFeed(0, 20)).items.find((p) => p.authorId !== me.id)!;
    expect(await source.toggleSave(other.id)).toBe(true);
    expect(await source.getSavedPostIds()).toEqual([other.id]);
    expect(await source.toggleSave(other.id)).toBe(false);
    expect(await source.getSavedPosts()).toEqual([]);

    const mine = [];
    for (let i = 0; i < 4; i++) mine.push(await source.createPost({ content: `Publication ${i}`, communityId: null, imagePath: null, visibility: 'Public' }));
    for (const p of mine.slice(0, 3)) await source.pinPost(p.id, true);
    await expect(source.pinPost(mine[3].id, true)).rejects.toMatchObject({ status: 400 });
    await expect(source.pinPost(other.id, true)).rejects.toMatchObject({ status: 403 });
    const profile = await source.getPosts({ authorId: me.id });
    expect(new Set(profile.slice(0, 3).map((p) => p.id))).toEqual(new Set(mine.slice(0, 3).map((p) => p.id)));
  });

  it('réponses rattachées au commentaire d\'origine, commentaire épinglé', async () => {
    const { source } = await signedIn();
    const post = await source.createPost({ content: 'Discussion', communityId: null, imagePath: null, visibility: 'Public' });
    const root = await source.addComment(post.id, 'Premier');
    const reply = await source.addComment(post.id, 'Réponse', { parentId: root.id });
    const nested = await source.addComment(post.id, 'Encore', { parentId: reply.id });
    expect(reply.parentId).toBe(root.id);
    expect(nested.parentId).toBe(root.id);
    expect((await source.pinComment(post.id, root.id)).pinnedCommentId).toBe(root.id);
    await expect(source.pinComment(post.id, reply.id)).rejects.toMatchObject({ status: 404 });
  });

  it('publication envoyée en message', async () => {
    const { source, me } = await signedIn();
    const post = (await source.getFeed(0, 5)).items[0];
    const friend = source.store.users.find((u) => u.id !== me.id)!;
    const conversation = await source.startConversation(friend.id);
    const message = await source.sendPost(conversation.id, post.id);
    expect(message.sharedPostId).toBe(post.id);
    expect(message.content).toBe('');
  });
});
