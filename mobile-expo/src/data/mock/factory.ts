import type { AppNotification, Comment, Community, CommunityEvent, Conversation, Message, Post, User } from '../types';
import { displayName } from '../types';
import { DEMO_EMAIL, DEMO_PASSWORD } from '../repositories';
import { REACTIONS } from '../repositories';
import { SeededRandom } from './random';
import {
  AVATAR_COLORS, COMMENT_POOL, SEED_COMMUNITIES, SEED_CONVERSATIONS, SEED_EVENTS, SEED_POSTS, SEED_USERS,
} from './seed/seed';

/** État en mémoire partagé par toute la source mock (restauré à chaque lancement). */
export interface MockStore {
  users: User[];
  communities: Community[];
  joinRequests: { communityId: string; userId: string }[];
  posts: Post[];
  comments: Comment[];
  conversations: Conversation[];
  messages: Message[];
  events: CommunityEvent[];
  notifications: AppNotification[];
  /** email → mot de passe fictif (session uniquement). */
  credentials: Map<string, string>;
  pendingVerifications: Set<string>;
  demoUserId: string;
}

const iso = (d: Date) => d.toISOString();
const addHours = (d: Date, h: number) => new Date(d.getTime() + h * 3_600_000);
const addMinutes = (d: Date, m: number) => new Date(d.getTime() + m * 60_000);
const addDays = (d: Date, n: number) => new Date(d.getTime() + n * 86_400_000);
const pad = (n: number) => String(n).padStart(2, '0');
const slug = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().replace(/[^a-z]/g, '');
const excerpt = (t: string) => (t.length <= 80 ? t : `${t.slice(0, 77).trimEnd()}…`);

export function createMockStore(now = new Date()): MockStore {
  const random = new SeededRandom(2026);
  // Aléatoire séparé pour les réactions : le reste du jeu de données ne change pas.
  const reactionRandom = new SeededRandom(2027);
  const seedReactions = (candidates: string[], probability: number, max: number) => {
    if (candidates.length === 0 || reactionRandom.next() >= probability) return [];
    const picked = reactionRandom.shuffle([...candidates]).slice(0, reactionRandom.int(1, max + 1));
    return picked.map((userId) => ({ userId, emoji: REACTIONS[reactionRandom.int(0, REACTIONS.length)] }));
  };

  const users: User[] = SEED_USERS.map((s, i) => ({
    id: random.uuid(),
    firstName: s.firstName,
    lastName: s.lastName,
    email: i === 0 ? DEMO_EMAIL : `${slug(s.firstName)}.${slug(s.lastName)}@exemple.test`,
    profession: s.profession,
    country: s.country,
    city: s.city,
    originCity: s.originCity,
    bio: s.bio,
    interests: [...s.interests],
    avatarColor: AVATAR_COLORS[i % AVATAR_COLORS.length],
    avatarImage: null,
    coverImage: `Profile/cover_${pad((i % 6) + 1)}.jpg`,
    joinedAt: iso(addDays(now, -random.int(30, 900))),
    isEmailVerified: true,
  }));
  const demo = users[0];

  // Communautés (les auteurs de publications d'une communauté en sont membres).
  const authorsBy = new Map<string, Set<number>>();
  for (const p of SEED_POSTS) {
    if (p.community) {
      if (!authorsBy.has(p.community)) authorsBy.set(p.community, new Set());
      authorsBy.get(p.community)!.add(p.author);
    }
  }

  const keys = new Map<string, string>();
  const communities: Community[] = SEED_COMMUNITIES.map((s, i) => {
    const members = new Set([...s.members, s.ownerIndex, ...(authorsBy.get(s.key) ?? [])]);
    if (s.demoIsMember) members.add(0);
    else members.delete(0);
    const moderator = s.members.find((m) => m !== s.ownerIndex && m !== 0) ?? s.ownerIndex;
    const c: Community = {
      id: random.uuid(),
      name: s.name,
      description: s.description,
      category: s.category,
      city: s.city,
      country: s.country,
      visibility: s.visibility,
      coverImage: `Covers/community_${pad(i + 1)}.jpg`,
      ownerId: users[s.ownerIndex].id,
      moderatorIds: [users[moderator].id],
      createdAt: iso(addDays(now, -random.int(60, 1200))),
      memberIds: [...members].map((m) => users[m].id),
    };
    keys.set(s.key, c.id);
    return c;
  });
  const joinRequests = [{ communityId: keys.get('parents')!, userId: demo.id }];

  const posts: Post[] = SEED_POSTS.map((s) => {
    const communityId = s.community ? keys.get(s.community)! : null;
    const isPrivate = communityId ? communities.find((c) => c.id === communityId)!.visibility === 'Private' : false;
    const likers = random.shuffle(users.filter((_, i) => i !== s.author && i !== 0)).slice(0, random.int(0, 24)).map((u) => u.id);
    if (s.author !== 0 && random.next() < 0.25) likers.push(demo.id);
    return {
      id: random.uuid(),
      authorId: users[s.author].id,
      communityId,
      content: s.text,
      imagePath: s.image > 0 ? `Posts/post_${pad(s.image)}.jpg` : null,
      createdAt: iso(addMinutes(addHours(now, -s.hoursAgo), -random.int(0, 50))),
      visibility: isPrivate ? 'CommunityMembers' : 'Public',
      likedBy: likers,
      commentCount: 0,
      shareCount: random.int(0, 9),
    };
  });

  // 200 commentaires, davantage sur les publications récentes.
  const comments: Comment[] = [];
  const ordered = [...posts].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  const counts = new Array(ordered.length).fill(0);
  for (let i = 0; i < 200; i++) counts[Math.min(Math.floor(Math.pow(random.next(), 1.6) * ordered.length), ordered.length - 1)]++;
  ordered.forEach((post, p) => {
    const created = new Date(post.createdAt);
    const span = Math.max(1, (now.getTime() - created.getTime()) / 60_000 - 1);
    for (let c = 0; c < counts[p]; c++) {
      let author: User;
      do author = users[random.int(0, users.length)];
      while (author.id === post.authorId);
      comments.push({
        id: random.uuid(),
        postId: post.id,
        authorId: author.id,
        content: COMMENT_POOL[random.int(0, COMMENT_POOL.length)],
        createdAt: iso(addMinutes(created, random.next() * span)),
        reactions: seedReactions(users.filter((u) => u.id !== author.id).map((u) => u.id), 0.3, 3),
      });
    }
    post.commentCount = counts[p];
  });

  // 12 conversations, 150 messages (« > » = message du compte de démonstration).
  const conversations: Conversation[] = [];
  const messages: Message[] = [];
  for (const s of SEED_CONVERSATIONS) {
    const other = users[s.otherUser];
    const times: Date[] = new Array(s.lines.length);
    times[s.lines.length - 1] = addMinutes(addHours(now, -s.hoursAgo), -random.int(0, 40));
    for (let i = s.lines.length - 2; i >= 0; i--) times[i] = addMinutes(times[i + 1], -random.int(2, 95));
    const conversation: Conversation = { id: random.uuid(), participantIds: [demo.id, other.id], createdAt: iso(addMinutes(times[0], -1)) };
    conversations.push(conversation);
    let unread = s.unreadCount;
    for (let i = s.lines.length - 1; i >= 0; i--) {
      const fromDemo = s.lines[i].startsWith('>');
      const isRead = fromDemo || unread <= 0;
      if (!fromDemo && unread > 0) unread--;
      messages.push({
        id: random.uuid(),
        conversationId: conversation.id,
        senderId: fromDemo ? demo.id : other.id,
        content: fromDemo ? s.lines[i].slice(1).trim() : s.lines[i],
        sentAt: iso(times[i]),
        isRead,
        isSimulated: false,
        reactions: seedReactions([fromDemo ? other.id : demo.id], 0.15, 1),
      });
    }
  }
  messages.sort((a, b) => a.sentAt.localeCompare(b.sentAt));

  // 25 événements à venir.
  const events: CommunityEvent[] = SEED_EVENTS.map((s) => {
    const day = new Date(now.getFullYear(), now.getMonth(), now.getDate() + s.daysFromNow, s.startHour);
    const organizer = users[s.organizer];
    const target = Math.min(s.participants, users.length - 2, s.capacity ?? Number.MAX_SAFE_INTEGER);
    const participants = random.shuffle(users.filter((u) => u.id !== demo.id && u.id !== organizer.id))
      .slice(0, Math.max(0, target - 1)).map((u) => u.id);
    participants.push(organizer.id);
    if (s.demoParticipates) participants.push(demo.id);
    return {
      id: random.uuid(),
      title: s.title,
      description: s.description,
      category: s.category,
      city: s.city,
      country: s.country,
      address: s.address,
      startsAt: iso(day),
      endsAt: iso(addHours(day, s.durationHours)),
      organizerId: organizer.id,
      communityId: s.community ? keys.get(s.community)! : null,
      imagePath: `Events/event_${pad((s.image % 10) + 1)}.jpg`,
      capacity: s.capacity,
      participantIds: participants,
    };
  });

  // 30 notifications.
  const notifications: AppNotification[] = [];
  let minutes = 6;
  const add = (category: AppNotification['category'], title: string, body: string, target: AppNotification['target'], targetId: string | null, unread: boolean) => {
    notifications.push({ id: random.uuid(), userId: demo.id, category, title, body, createdAt: iso(addMinutes(now, -minutes)), isRead: !unread, target, targetId });
    minutes += random.int(20, 460);
  };
  const myPosts = posts.filter((p) => p.authorId === demo.id).sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  const others = users.slice(1);
  for (let i = 0; i < 8; i++) {
    const post = myPosts[i % myPosts.length];
    const actor = others[random.int(0, others.length)];
    add('Social', i % 2 === 0 ? `${displayName(actor)} a aimé votre publication` : `${displayName(actor)} a commenté votre publication`,
      i % 2 === 0 ? excerpt(post.content) : `« ${COMMENT_POOL[random.int(0, COMMENT_POOL.length)]} »`, 'Post', post.id, i < 3);
  }
  for (let i = 0; i < 6; i++) {
    const c = conversations[i];
    const other = users.find((u) => u.id === c.participantIds[1])!;
    const last = messages.filter((m) => m.conversationId === c.id && m.senderId === other.id).at(-1);
    add('Messages', `Nouveau message de ${displayName(other)}`, excerpt(last?.content ?? ''), 'Conversation', c.id, i < 2);
  }
  const mine = communities.filter((c) => c.memberIds.includes(demo.id));
  for (let i = 0; i < 6; i++) {
    const c = mine[i % mine.length];
    const actor = others[random.int(0, others.length)];
    add('Communities', i % 2 === 0 ? `Nouvelle publication dans ${c.name}` : `${displayName(actor)} a rejoint ${c.name}`,
      i % 2 === 0 ? `${displayName(actor)} a partagé une publication.` : 'Souhaitez-lui la bienvenue !', 'Community', c.id, i < 2);
  }
  [...events].sort((a, b) => a.startsAt.localeCompare(b.startsAt)).slice(0, 6).forEach((e, i) => {
    const d = new Date(e.startsAt);
    add('Events', i % 2 === 0 ? `Rappel : ${e.title}` : `Nouvel événement : ${e.title}`,
      `${e.city} · ${pad(d.getDate())}/${pad(d.getMonth() + 1)} à ${pad(d.getHours())}:${pad(d.getMinutes())}`, 'Event', e.id, i < 2);
  });
  add('System', 'Complétez votre profil', "Ajoutez vos centres d'intérêt pour recevoir de meilleures suggestions.", 'Profile', demo.id, true);
  add('System', 'Mode démonstration', 'Toutes les données de cette version sont fictives et restent sur votre appareil.', 'None', null, false);
  add('System', 'Nouvelles communautés à découvrir', 'Trois communautés correspondent à vos centres d\'intérêt.', 'None', null, false);
  add('System', 'Bienvenue sur BéninConnect', 'Connectez-vous à votre communauté, partout dans le monde.', 'None', null, false);

  return {
    users, communities, joinRequests, posts, comments, conversations, messages, events, notifications,
    credentials: new Map([[DEMO_EMAIL, DEMO_PASSWORD]]),
    pendingVerifications: new Set(),
    demoUserId: demo.id,
  };
}
