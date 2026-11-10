import * as v from '@/core/validation';
import type { DataSource } from '../repositories';
import { INTERESTS, REACTIONS } from '../repositories';
import type {
  AppNotification, Comment, Community, CommunityEvent, CommunityMember, Conversation, CreateEventRequest, EventFilter,
  MembershipStatus, Message, OutgoingFile, Paged, Post, PostFilter, User, LocalPhoto, LocalVideo, ReportReason, ReportTarget,
} from '../types';
import { AppError, displayName, isEventFull, membershipStatus, toggleReaction } from '../types';
import { COMMUNITY_CATEGORIES, EVENT_CATEGORIES } from './seed/seed';
import { extractTags, normalizeTag } from '../social';
import { createMockStore, type MockStore } from './factory';
import { newId } from './random';

const checkReaction = (emoji: string) => {
  if (!REACTIONS.includes(emoji.trim())) throw new AppError("Cette réaction n'est pas disponible.", 400);
  return emoji.trim();
};

export interface MockOptions {
  latencyMs: number;
  /** Mode développeur : environ un appel sur deux échoue. */
  simulateErrors: () => boolean;
}

const TOKEN_PREFIX = 'demo:';
const excerpt = (t: string) => (t.length <= 80 ? t : `${t.slice(0, 77).trimEnd()}…`);
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

/** Source hors ligne : même comportement que l'API (règles, erreurs, notifications), données en mémoire. */
export class MockDataSource implements DataSource {
  readonly kind = 'demo' as const;
  readonly demoVerificationCode = '123456';
  readonly hasDemoAccount = true;
  readonly store: MockStore;
  private me: string | null = null;

  constructor(private readonly options: MockOptions = { latencyMs: 180, simulateErrors: () => false }, store?: MockStore) {
    this.store = store ?? createMockStore();
  }

  // --- Infrastructure ---------------------------------------------------------

  private async net() {
    if (this.options.latencyMs > 0) await sleep(this.options.latencyMs);
    if (this.options.simulateErrors() && Math.random() < 0.5) {
      throw new AppError('Connexion simulée interrompue. Réessayez dans un instant.');
    }
  }

  private get userId(): string {
    if (!this.me) throw new AppError('Votre session a expiré. Reconnectez-vous.', 401);
    return this.me;
  }

  private find<T extends { id: string }>(list: T[], id: string, what: string): T {
    const item = list.find((x) => x.id === id);
    if (!item) throw new AppError(`${what} introuvable.`, 404);
    return item;
  }

  private replace<T extends { id: string }>(list: T[], updated: T) {
    list[list.findIndex((x) => x.id === updated.id)] = updated;
    return updated;
  }

  private notify(userId: string, n: Omit<AppNotification, 'id' | 'userId' | 'createdAt' | 'isRead'>) {
    this.store.notifications.push({ ...n, id: newId(), userId, createdAt: new Date().toISOString(), isRead: false });
  }

  private memberOf(userId: string) {
    return new Set(this.store.communities.filter((c) => c.memberIds.includes(userId)).map((c) => c.id));
  }

  /** Paires « bloqueur:bloqué ». */
  private readonly blocks = new Set<string>();

  private isBlocked(a: string, b: string) {
    return this.blocks.has(`${a}:${b}`) || this.blocks.has(`${b}:${a}`);
  }

  private visiblePosts(userId: string) {
    const memberOf = this.memberOf(userId);
    return this.store.posts.filter((p) => !this.isBlocked(userId, p.authorId) && (p.authorId === userId || p.visibility === 'Public'
      || (p.visibility === 'CommunityMembers' && p.communityId != null && memberOf.has(p.communityId))));
  }

  async blockUser(userId: string) {
    await this.net();
    if (userId === this.userId) throw new AppError('Vous ne pouvez pas vous bloquer vous-même.', 400);
    this.find(this.store.users, userId, 'Membre');
    this.blocks.add(`${this.userId}:${userId}`);
  }

  async unblockUser(userId: string) {
    await this.net();
    this.blocks.delete(`${this.userId}:${userId}`);
  }

  async getBlockedUsers() {
    await this.net();
    return this.store.users.filter((u) => this.blocks.has(`${this.userId}:${u.id}`));
  }

  private ensureNotBlocked(c: Conversation) {
    if (c.participantIds.some((p) => p !== this.userId && this.isBlocked(this.userId, p))) {
      throw new AppError('Vous ne pouvez plus échanger de messages avec ce membre.', 400);
    }
  }

  private validate(errors: Record<string, string | null>) {
    const first = Object.values(errors).find((e) => e);
    if (first) throw new AppError(first, 400);
  }

  // --- Authentification -------------------------------------------------------

  setToken(token: string | null) {
    this.me = token?.startsWith(TOKEN_PREFIX) ? token.slice(TOKEN_PREFIX.length) : null;
  }

  async login(email: string, password: string) {
    await this.net();
    this.validate({ email: v.email(email), password: v.required(password, 'Le mot de passe') });
    const key = email.trim().toLowerCase();
    if (this.store.credentials.get(key) !== password) throw new AppError('Email ou mot de passe incorrect.', 400);
    if (this.store.pendingVerifications.has(key)) throw new AppError("Vérifiez d'abord votre adresse email.", 400);
    const user = this.store.users.find((u) => u.email === key);
    if (!user) throw new AppError('Compte introuvable.', 404);
    return { token: TOKEN_PREFIX + user.id, user };
  }

  async register(r: Parameters<DataSource['register']>[0]) {
    await this.net();
    this.validate({
      firstName: v.length(r.firstName, 'Le prénom', 2, 40), lastName: v.length(r.lastName, 'Le nom', 2, 40),
      email: v.email(r.email), country: v.required(r.country, 'Le pays'), city: v.required(r.city, 'La ville'),
      password: v.password(r.password),
    });
    const key = r.email.trim().toLowerCase();
    if (this.store.credentials.has(key)) throw new AppError('Un compte existe déjà avec cette adresse email.', 409);
    const user: User = {
      id: newId(), firstName: r.firstName.trim(), lastName: r.lastName.trim(), email: key, profession: '',
      country: r.country.trim(), city: r.city.trim(), originCity: '', bio: '', interests: [], avatarColor: '#0E7490',
      coverImage: 'Profile/cover_02.jpg', joinedAt: new Date().toISOString(), isEmailVerified: false,
    };
    this.store.users.push(user);
    this.store.credentials.set(key, r.password);
    this.store.pendingVerifications.add(key);
    return user;
  }

  async verifyEmail(email: string, code: string) {
    await this.net();
    if (code.trim() !== this.demoVerificationCode) throw new AppError('Code de vérification incorrect.', 400);
    const key = email.trim().toLowerCase();
    const user = this.store.users.find((u) => u.email === key);
    if (!user) throw new AppError('Compte introuvable.', 404);
    const verified = this.replace(this.store.users, { ...user, isEmailVerified: true });
    if (this.store.pendingVerifications.delete(key)) {
      this.notify(user.id, { category: 'System', title: 'Bienvenue sur BéninConnect', body: 'Complétez votre profil et rejoignez vos premières communautés.', target: 'Profile', targetId: user.id });
    }
    return { token: TOKEN_PREFIX + user.id, user: verified };
  }

  async resendCode() { await this.net(); }

  async forgotPassword(email: string) {
    await this.net();
    this.validate({ email: v.email(email) });
  }

  async restore(token: string) {
    await this.net();
    const id = token.startsWith(TOKEN_PREFIX) ? token.slice(TOKEN_PREFIX.length) : '';
    return this.store.users.find((u) => u.id === id && u.isEmailVerified) ?? null;
  }

  // --- Membres ------------------------------------------------------------------

  async getUser(id: string) { await this.net(); return this.find(this.store.users, id, 'Membre'); }

  async getUsers(ids: string[]) {
    const set = new Set(ids);
    return this.store.users.filter((u) => set.has(u.id));
  }

  async searchUsers(search?: string) {
    await this.net();
    const q = search?.trim() ?? '';
    return this.store.users
      .filter((u) => u.isEmailVerified && (!q || [displayName(u), u.city, u.country, u.profession, ...u.interests].some((x) => v.matches(x, q))))
      .sort((a, b) => a.firstName.localeCompare(b.firstName));
  }

  async updateProfile(r: Parameters<DataSource['updateProfile']>[0]) {
    await this.net();
    this.validate({
      firstName: v.length(r.firstName, 'Le prénom', 2, 40), lastName: v.length(r.lastName, 'Le nom', 2, 40),
      country: v.required(r.country, 'Le pays'), city: v.required(r.city, 'La ville'),
      bio: r.bio.length > 300 ? 'La biographie ne doit pas dépasser 300 caractères.' : null,
      interests: r.interests.length > 6 ? "Maximum 6 centres d'intérêt." : r.interests.some((i) => !INTERESTS.includes(i)) ? "Centre d'intérêt inconnu." : null,
    });
    const user = this.find(this.store.users, this.userId, 'Compte');
    return this.replace(this.store.users, {
      ...user, firstName: r.firstName.trim(), lastName: r.lastName.trim(), profession: r.profession.trim(),
      country: r.country.trim(), city: r.city.trim(), bio: r.bio.trim(), interests: [...new Set(r.interests)],
      avatarImage: r.avatarImage == null ? user.avatarImage ?? null : r.avatarImage || null,
      coverImage: r.coverImage == null ? user.coverImage : r.coverImage || null,
    });
  }

  // En démo, la photo reste sur l'appareil : son adresse locale sert de chemin.
  /** Démo : la vidéo reste sur l'appareil. */
  async uploadVideo(video: LocalVideo, onProgress?: (ratio: number) => void) {
    await this.net();
    this.userId;
    onProgress?.(1);
    return video.uri;
  }

  async uploadImage(photo: LocalPhoto) {
    await this.net();
    this.userId;
    return photo.uri;
  }

  // --- Publications ---------------------------------------------------------------

  async getFeed(skip: number, take: number): Promise<Paged<Post>> {
    await this.net();
    const visible = this.visiblePosts(this.userId).sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    return { items: visible.slice(skip, skip + take), totalCount: visible.length, skip };
  }

  async getPost(id: string) {
    await this.net();
    const post = this.visiblePosts(this.userId).find((p) => p.id === id);
    if (!post) throw new AppError('Publication introuvable.', 404);
    return post;
  }

  async getPosts(f: PostFilter) {
    await this.net();
    const q = f.search?.trim();
    const tag = f.tag ? normalizeTag(f.tag) : null;
    // Profil : publications épinglées d'abord
    const pinned = (p: Post) => (f.authorId && p.pinnedAt ? p.pinnedAt : '');
    return this.visiblePosts(this.userId)
      .filter((p) => (!f.authorId || p.authorId === f.authorId) && (!f.communityId || p.communityId === f.communityId) && (!q || v.matches(p.content, q))
        && (!tag || extractTags(p.content).includes(tag)))
      .sort((a, b) => pinned(b).localeCompare(pinned(a)) || b.createdAt.localeCompare(a.createdAt))
      .slice(0, q || tag ? 30 : 200);
  }

  private readonly saved = new Map<string, string[]>();

  async toggleSave(postId: string) {
    await this.getPost(postId);
    const mine = this.saved.get(this.userId) ?? [];
    const saved = !mine.includes(postId);
    this.saved.set(this.userId, saved ? [postId, ...mine] : mine.filter((id) => id !== postId));
    return saved;
  }

  async getSavedPostIds() {
    await this.net();
    const visible = new Set(this.visiblePosts(this.userId).map((p) => p.id));
    return (this.saved.get(this.userId) ?? []).filter((id) => visible.has(id));
  }

  async getSavedPosts() {
    const ids = await this.getSavedPostIds();
    return ids.map((id) => this.store.posts.find((p) => p.id === id)!);
  }

  async pinPost(postId: string, pinned: boolean) {
    const post = await this.getPost(postId);
    if (post.authorId !== this.userId) throw new AppError('Vous ne pouvez épingler que vos propres publications.', 403);
    if (pinned && !post.pinnedAt && this.store.posts.filter((p) => p.authorId === this.userId && p.pinnedAt).length >= 3) {
      throw new AppError('Vous pouvez épingler 3 publications au plus. Désépinglez-en une d\'abord.', 400);
    }
    return this.replace(this.store.posts, { ...post, pinnedAt: pinned ? post.pinnedAt ?? new Date().toISOString() : null });
  }

  async pinComment(postId: string, commentId: string | null) {
    const post = await this.getPost(postId);
    if (post.authorId !== this.userId) throw new AppError('Seul l\'auteur de la publication peut épingler un commentaire.', 403);
    if (commentId && !this.store.comments.some((c) => c.id === commentId && c.postId === postId && !c.parentId)) {
      throw new AppError('Commentaire introuvable.', 404);
    }
    return this.replace(this.store.posts, { ...post, pinnedCommentId: commentId });
  }

  /** Membres mentionnés existants, hors auteur et membres bloqués. */
  private validMentions(ids: string[] | undefined) {
    return [...new Set(ids ?? [])].filter((id) => id !== this.userId && !this.isBlocked(this.userId, id)
      && this.store.users.some((u) => u.id === id)).slice(0, 20);
  }

  private announceMentions(ids: string[], postId: string, text: string, inComment: boolean) {
    const me = displayName(this.find(this.store.users, this.userId, 'Compte'));
    for (const id of ids) {
      this.notify(id, { category: 'Social', title: `${me} vous a mentionné dans ${inComment ? 'un commentaire' : 'une publication'}`,
        body: `« ${excerpt(text)} »`, target: 'Post', targetId: postId });
    }
  }

  async createPost(r: Parameters<DataSource['createPost']>[0]) {
    await this.net();
    this.validate({ content: v.length(r.content, 'La publication', 3, 2000) });
    if (r.visibility === 'CommunityMembers' && !r.communityId) throw new AppError('Choisissez une communauté pour une publication réservée aux membres.', 400);
    if (r.communityId && !this.memberOf(this.userId).has(r.communityId)) throw new AppError('Rejoignez la communauté pour y publier.', 403);
    if ((r.images?.length ?? 0) > 10) throw new AppError('Une publication contient au plus 10 photos.', 400);
    const images = r.videoPath ? [] : r.images ?? (r.imagePath ? [r.imagePath] : []);
    const mentionIds = this.validMentions(r.mentions);
    const post: Post = {
      id: newId(), authorId: this.userId, communityId: r.communityId, content: r.content.trim(), imagePath: images[0] ?? r.imagePath,
      createdAt: new Date().toISOString(), visibility: r.visibility, likedBy: [], commentCount: 0, shareCount: 0, videoPath: r.videoPath ?? null,
      images, mentionIds, pinnedAt: null, pinnedCommentId: null,
    };
    this.store.posts.push(post);
    this.announceMentions(mentionIds, post.id, post.content, false);
    return post;
  }

  async updatePost(postId: string, r: Parameters<DataSource['updatePost']>[1]) {
    const post = await this.getPost(postId);
    if (post.authorId !== this.userId) throw new AppError('Vous ne pouvez modifier que vos propres publications.', 403);
    this.validate({ content: v.length(r.content, 'La publication', 3, 2000) });
    if (r.visibility === 'CommunityMembers' && !post.communityId) throw new AppError('Choisissez une communauté pour une publication réservée aux membres.', 400);
    if ((r.images?.length ?? 0) > 10) throw new AppError('Une publication contient au plus 10 photos.', 400);
    const images = r.videoPath ? [] : r.images ?? (r.imagePath ? [r.imagePath] : []);
    const mentionIds = this.validMentions(r.mentions);
    this.announceMentions(mentionIds.filter((id) => !post.mentionIds?.includes(id)), post.id, r.content, false);
    return this.replace(this.store.posts, {
      ...post, content: r.content.trim(), imagePath: images[0] ?? r.imagePath, visibility: r.visibility, videoPath: r.videoPath ?? null,
      images, mentionIds, editedAt: new Date().toISOString(),
    });
  }

  async toggleLike(postId: string) {
    const post = await this.getPost(postId);
    const liked = !post.likedBy.includes(this.userId);
    const updated = this.replace(this.store.posts, {
      ...post, likedBy: liked ? [...post.likedBy, this.userId] : post.likedBy.filter((id) => id !== this.userId),
    });
    if (liked && post.authorId !== this.userId) {
      const me = this.find(this.store.users, this.userId, 'Compte');
      this.notify(post.authorId, { category: 'Social', title: `${displayName(me)} a aimé votre publication`, body: excerpt(post.content), target: 'Post', targetId: post.id });
    }
    return updated;
  }

  async sharePost(postId: string) {
    const post = await this.getPost(postId);
    return this.replace(this.store.posts, { ...post, shareCount: post.shareCount + 1 });
  }

  async deletePost(postId: string) {
    const post = await this.getPost(postId);
    if (post.authorId !== this.userId) throw new AppError('Vous ne pouvez supprimer que vos propres publications.', 403);
    this.store.posts = this.store.posts.filter((p) => p.id !== postId);
    this.store.comments = this.store.comments.filter((c) => c.postId !== postId);
  }

  private readonly reports = new Set<string>();

  async report(target: ReportTarget, targetId: string, _reason: ReportReason) {
    await this.net();
    const owner = target === 'Post' ? this.store.posts.find((p) => p.id === targetId)?.authorId
      : target === 'Comment' ? this.store.comments.find((c) => c.id === targetId)?.authorId
      : target === 'User' ? this.store.users.find((u) => u.id === targetId)?.id
      : this.store.communities.find((c) => c.id === targetId)?.ownerId;
    if (!owner) throw new AppError('Contenu introuvable.', 404);
    if (owner === this.userId) throw new AppError('Vous ne pouvez pas signaler votre propre contenu.', 400);
    const key = `${this.userId}:${target}:${targetId}`;
    if (this.reports.has(key)) throw new AppError('Vous avez déjà signalé ce contenu ; il est en cours d\'examen.', 409);
    this.reports.add(key);
  }

  async deleteAccount(password: string) {
    await this.net();
    const me = this.find(this.store.users, this.userId, 'Compte');
    if (this.store.credentials.get(me.email) !== password) throw new AppError('Mot de passe incorrect.', 400);
    this.store.credentials.delete(me.email);
    this.store.users = this.store.users.filter((u) => u.id !== me.id);
    this.store.posts = this.store.posts.filter((p) => p.authorId !== me.id);
    this.store.comments = this.store.comments.filter((c) => c.authorId !== me.id);
    this.me = null;
  }

  async getComments(postId: string) {
    await this.getPost(postId);
    return this.store.comments.filter((c) => c.postId === postId && !this.isBlocked(this.userId, c.authorId)).sort((a, b) => a.createdAt.localeCompare(b.createdAt));
  }

  async addComment(postId: string, content: string, o?: { parentId?: string | null; mentions?: string[] }) {
    const post = await this.getPost(postId);
    this.validate({ content: v.length(content, 'Le commentaire', 1, 1000) });
    // Réponse à une réponse : rattachée au commentaire d'origine (un seul niveau)
    let parent: Comment | undefined;
    if (o?.parentId) {
      const target = this.store.comments.find((c) => c.id === o.parentId && c.postId === postId);
      if (!target) throw new AppError('Commentaire introuvable.', 404);
      parent = target.parentId ? this.store.comments.find((c) => c.id === target.parentId) ?? target : target;
      if (target.authorId !== this.userId) {
        const me = this.find(this.store.users, this.userId, 'Compte');
        this.notify(target.authorId, { category: 'Social', title: `${displayName(me)} a répondu à votre commentaire`, body: `« ${excerpt(content.trim())} »`, target: 'Post', targetId: post.id });
      }
    }
    const mentionIds = this.validMentions(o?.mentions);
    const comment: Comment = {
      id: newId(), postId, authorId: this.userId, content: content.trim(), createdAt: new Date().toISOString(), reactions: [],
      parentId: parent?.id ?? null, mentionIds,
    };
    this.store.comments.push(comment);
    this.replace(this.store.posts, { ...post, commentCount: post.commentCount + 1 });
    if (post.authorId !== this.userId) {
      const me = this.find(this.store.users, this.userId, 'Compte');
      this.notify(post.authorId, { category: 'Social', title: `${displayName(me)} a commenté votre publication`, body: `« ${excerpt(comment.content)} »`, target: 'Post', targetId: post.id });
    }
    this.announceMentions(mentionIds, post.id, comment.content, true);
    return comment;
  }

  async reactToComment(postId: string, commentId: string, emoji: string) {
    await this.getPost(postId);
    const comment = this.store.comments.find((c) => c.id === commentId && c.postId === postId);
    if (!comment) throw new AppError('Commentaire introuvable.', 404);
    const reactions = toggleReaction(comment.reactions, this.userId, checkReaction(emoji));
    const reacted = reactions.some((r) => r.userId === this.userId);
    if (reacted && comment.authorId !== this.userId) {
      const me = this.find(this.store.users, this.userId, 'Compte');
      this.notify(comment.authorId, { category: 'Social', title: `${displayName(me)} a réagi ${emoji} à votre commentaire`, body: `« ${excerpt(comment.content)} »`, target: 'Post', targetId: postId });
    }
    return this.replace(this.store.comments, { ...comment, reactions });
  }

  // --- Communautés --------------------------------------------------------------------

  async getCommunities(search?: string) {
    await this.net();
    const q = search?.trim();
    return this.store.communities
      .filter((c) => !q || [c.name, c.description, c.city, c.country, c.category].some((x) => v.matches(x, q)))
      .sort((a, b) => b.memberIds.length - a.memberIds.length);
  }

  async getCommunity(id: string) { await this.net(); return this.find(this.store.communities, id, 'Communauté'); }

  async getCommunitiesByIds(ids: string[]) {
    const set = new Set(ids);
    return this.store.communities.filter((c) => set.has(c.id));
  }

  async getMemberCommunities(userId: string) {
    await this.net();
    return this.store.communities.filter((c) => c.memberIds.includes(userId)).sort((a, b) => a.name.localeCompare(b.name));
  }

  async getCommunityCategories() { return COMMUNITY_CATEGORIES; }

  async getMembers(communityId: string): Promise<CommunityMember[]> {
    await this.net();
    const c = this.find(this.store.communities, communityId, 'Communauté');
    if (c.visibility === 'Private' && !c.memberIds.includes(this.userId)) throw new AppError('Cette communauté est privée.', 403);
    const rank = { Owner: 2, Moderator: 1, Member: 0 } as const;
    return this.store.users
      .filter((u) => c.memberIds.includes(u.id))
      .map((user) => ({ user, role: user.id === c.ownerId ? 'Owner' as const : c.moderatorIds.includes(user.id) ? 'Moderator' as const : 'Member' as const }))
      .sort((a, b) => rank[b.role] - rank[a.role] || a.user.firstName.localeCompare(b.user.firstName));
  }

  async getMyJoinRequests() {
    await this.net();
    return this.store.joinRequests.filter((r) => r.userId === this.userId).map((r) => r.communityId);
  }

  async createCommunity(r: Parameters<DataSource['createCommunity']>[0]) {
    await this.net();
    this.validate({
      name: v.length(r.name, 'Le nom', 3, 60), description: v.length(r.description, 'La description', 10, 500),
      category: COMMUNITY_CATEGORIES.includes(r.category) ? null : 'Choisissez une catégorie.',
      country: v.required(r.country, 'Le pays'), city: v.required(r.city, 'La ville'),
    });
    if (this.store.communities.some((c) => c.name.toLowerCase() === r.name.trim().toLowerCase())) {
      throw new AppError('Une communauté porte déjà ce nom.', 409);
    }
    const created: Community = {
      id: newId(), name: r.name.trim(), description: r.description.trim(), category: r.category, country: r.country.trim(),
      city: r.city.trim(), visibility: r.visibility, coverImage: r.coverImage ?? 'Covers/community_01.jpg', ownerId: this.userId,
      moderatorIds: [], createdAt: new Date().toISOString(), memberIds: [this.userId],
    };
    this.store.communities.push(created);
    return created;
  }

  async joinCommunity(id: string): Promise<MembershipStatus> {
    const c = await this.getCommunity(id);
    const status = membershipStatus(c, this.userId, await this.getMyJoinRequests());
    if (status !== 'None') return status;
    if (c.visibility === 'Private') {
      this.store.joinRequests.push({ communityId: id, userId: this.userId });
      return 'Pending';
    }
    this.replace(this.store.communities, { ...c, memberIds: [...c.memberIds, this.userId] });
    this.notify(this.userId, { category: 'Communities', title: `Bienvenue dans ${c.name}`, body: 'Présentez-vous aux membres et découvrez les prochains événements.', target: 'Community', targetId: c.id });
    return 'Member';
  }

  async leaveCommunity(id: string): Promise<MembershipStatus> {
    const c = await this.getCommunity(id);
    if (c.ownerId === this.userId) throw new AppError('Le créateur ne peut pas quitter sa communauté.', 400);
    this.replace(this.store.communities, { ...c, memberIds: c.memberIds.filter((m) => m !== this.userId) });
    return 'None';
  }

  async cancelJoinRequest(id: string): Promise<MembershipStatus> {
    await this.net();
    this.store.joinRequests = this.store.joinRequests.filter((r) => !(r.communityId === id && r.userId === this.userId));
    return 'None';
  }

  // --- Messagerie -----------------------------------------------------------------------

  private mine(id: string) {
    const c = this.store.conversations.find((x) => x.id === id && x.participantIds.includes(this.userId));
    if (!c) throw new AppError('Conversation introuvable.', 404);
    return c;
  }

  private summary(c: Conversation) {
    const otherId = c.participantIds.find((p) => p !== this.userId)!;
    const msgs = this.store.messages.filter((m) => m.conversationId === c.id);
    return {
      conversation: c,
      otherParticipant: this.find(this.store.users, otherId, 'Membre'),
      lastMessage: msgs.reduce<Message | null>((last, m) => (!last || m.sentAt > last.sentAt ? m : last), null),
      unreadCount: msgs.filter((m) => m.senderId !== this.userId && !m.isRead).length,
      isBlocked: this.isBlocked(this.userId, otherId),
    };
  }

  async getConversations() {
    await this.net();
    return this.store.conversations
      .filter((c) => c.participantIds.includes(this.userId))
      .map((c) => this.summary(c))
      .sort((a, b) => (b.lastMessage?.sentAt ?? b.conversation.createdAt).localeCompare(a.lastMessage?.sentAt ?? a.conversation.createdAt));
  }

  async getConversation(id: string) { await this.net(); return this.summary(this.mine(id)); }

  async getMessages(conversationId: string) {
    await this.net();
    this.mine(conversationId);
    return this.store.messages.filter((m) => m.conversationId === conversationId).sort((a, b) => a.sentAt.localeCompare(b.sentAt));
  }

  private post(conversationId: string, senderId: string, content: string, isSimulated: boolean, sharedPostId: string | null = null) {
    if (!sharedPostId || content.trim()) this.validate({ content: v.length(content, 'Le message', 1, 2000) });
    const m: Message = {
      id: newId(), conversationId, senderId, content: content.trim(), sentAt: new Date().toISOString(), isRead: false, isSimulated, reactions: [], sharedPostId,
    };
    this.store.messages.push(m);
    return m;
  }

  async sendMessage(conversationId: string, content: string) {
    await this.net();
    const c = this.mine(conversationId);
    this.ensureNotBlocked(c);
    const message = this.post(conversationId, this.userId, content, false);
    const me = this.find(this.store.users, this.userId, 'Compte');
    for (const other of c.participantIds.filter((p) => p !== this.userId)) {
      this.notify(other, { category: 'Messages', title: `Nouveau message de ${displayName(me)}`, body: excerpt(message.content), target: 'Conversation', targetId: c.id });
    }
    return message;
  }

  async sendPost(conversationId: string, postId: string, content = '') {
    await this.getPost(postId);
    const c = this.mine(conversationId);
    this.ensureNotBlocked(c);
    const message = this.post(conversationId, this.userId, content, false, postId);
    const me = this.find(this.store.users, this.userId, 'Compte');
    for (const other of c.participantIds.filter((p) => p !== this.userId)) {
      this.notify(other, { category: 'Messages', title: `Nouveau message de ${displayName(me)}`, body: '📰 Publication partagée', target: 'Conversation', targetId: c.id });
    }
    return message;
  }

  async simulateReply(conversationId: string, content: string) {
    await this.net();
    const c = this.mine(conversationId);
    return this.post(conversationId, c.participantIds.find((p) => p !== this.userId)!, content, true);
  }

  async reactToMessage(conversationId: string, messageId: string, emoji: string) {
    await this.net();
    this.mine(conversationId);
    const message = this.store.messages.find((m) => m.id === messageId && m.conversationId === conversationId);
    if (!message) throw new AppError('Message introuvable.', 404);
    return this.replace(this.store.messages, { ...message, reactions: toggleReaction(message.reactions, this.userId, checkReaction(emoji)) });
  }

  private ownMessage(conversationId: string, messageId: string) {
    this.mine(conversationId);
    const message = this.store.messages.find((m) => m.id === messageId && m.conversationId === conversationId);
    if (!message) throw new AppError('Message introuvable.', 404);
    if (message.isDeleted) throw new AppError('Ce message a été supprimé.', 400);
    if (message.senderId !== this.userId) throw new AppError('Vous ne pouvez modifier que vos propres messages.', 403);
    return message;
  }

  async editMessage(conversationId: string, messageId: string, content: string) {
    await this.net();
    const message = this.ownMessage(conversationId, messageId);
    this.validate({ content: v.length(content, 'Le message', 1, 2000) });
    if (Date.now() - Date.parse(message.sentAt) > 15 * 60_000) throw new AppError('Un message ne peut plus être modifié 15 minutes après son envoi.', 400);
    return this.replace(this.store.messages, { ...message, content: content.trim(), editedAt: new Date().toISOString() });
  }

  async deleteMessage(conversationId: string, messageId: string) {
    await this.net();
    const message = this.ownMessage(conversationId, messageId);
    this.attachmentUris.delete(messageId);
    return this.replace(this.store.messages, { ...message, content: '', reactions: [], isDeleted: true, attachment: null });
  }

  /** Démo : le fichier reste sur l'appareil, son adresse locale sert d'URL. */
  private readonly attachmentUris = new Map<string, string>();

  async sendAttachment(conversationId: string, file: OutgoingFile, caption?: string) {
    await this.net();
    this.ensureNotBlocked(this.mine(conversationId));
    const kind = file.mimeType.startsWith('image/') ? 'Image' : file.mimeType.startsWith('audio/') ? 'Audio' : 'File';
    if (kind === 'File' && file.mimeType !== 'application/pdf') throw new AppError('Format non pris en charge : envoyez une photo, un PDF ou un fichier audio.', 400);
    const m: Message = {
      id: newId(), conversationId, senderId: this.userId, content: caption?.trim() ?? '', sentAt: new Date().toISOString(), isRead: false,
      isSimulated: false, reactions: [], attachment: { kind, fileName: file.name, contentType: file.mimeType, size: 0, durationMs: file.durationMs ?? null },
    };
    this.attachmentUris.set(m.id, file.uri);
    this.store.messages.push(m);
    return m;
  }

  attachmentUrl(message: Message) {
    return this.attachmentUris.get(message.id) ?? null;
  }

  async markConversationRead(conversationId: string) {
    await this.net();
    this.mine(conversationId);
    let count = 0;
    this.store.messages = this.store.messages.map((m) => {
      if (m.conversationId === conversationId && m.senderId !== this.userId && !m.isRead) {
        count++;
        return { ...m, isRead: true };
      }
      return m;
    });
    return count;
  }

  async startConversation(otherUserId: string) {
    await this.net();
    if (otherUserId === this.userId) throw new AppError('Impossible de démarrer une conversation avec soi-même.', 400);
    this.find(this.store.users, otherUserId, 'Membre');
    const existing = this.store.conversations.find((c) => c.participantIds.length === 2 && c.participantIds.includes(this.userId) && c.participantIds.includes(otherUserId));
    if (existing) return existing;
    const created = { id: newId(), participantIds: [this.userId, otherUserId], createdAt: new Date().toISOString() };
    this.store.conversations.push(created);
    return created;
  }

  async getUnreadMessageCount() {
    await this.net();
    const mine = new Set(this.store.conversations.filter((c) => c.participantIds.includes(this.userId)).map((c) => c.id));
    return this.store.messages.filter((m) => mine.has(m.conversationId) && m.senderId !== this.userId && !m.isRead).length;
  }

  // --- Événements -------------------------------------------------------------------------

  async getEvents(f: EventFilter) {
    await this.net();
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const week = today.getTime() + 7 * 86_400_000;
    const month = today.getTime() + 31 * 86_400_000;
    const q = f.search?.trim();
    return this.store.events
      .filter((e) => new Date(e.endsAt) >= now)
      .filter((e) => !q || [e.title, e.description, e.city].some((x) => v.matches(x, q)))
      .filter((e) => !f.category || e.category === f.category)
      .filter((e) => !f.city || e.city === f.city)
      .filter((e) => {
        const start = new Date(e.startsAt).getTime();
        switch (f.period) {
          case 'ThisWeek': return start < week;
          case 'ThisMonth': return start < month;
          case 'Later': return start >= month;
          default: return true;
        }
      })
      .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
  }

  async getEvent(id: string) { await this.net(); return this.find(this.store.events, id, 'Événement'); }

  async getCommunityEvents(communityId: string) {
    return (await this.getEvents({})).filter((e) => e.communityId === communityId);
  }

  async getEventCategories() { return EVENT_CATEGORIES; }

  async getEventCities() { return [...new Set(this.store.events.map((e) => e.city))].sort(); }

  async createEvent(r: CreateEventRequest) {
    await this.net();
    this.validate({
      title: v.length(r.title, 'Le titre', 5, 80), description: v.length(r.description, 'La description', 20, 1000),
      category: EVENT_CATEGORIES.includes(r.category) ? null : 'Choisissez une catégorie.',
      city: v.required(r.city, 'La ville'), address: v.required(r.address, "L'adresse"),
      start: new Date(r.startsAt) <= new Date() ? "L'événement doit avoir lieu dans le futur." : null,
      end: new Date(r.endsAt) <= new Date(r.startsAt) ? "La fin de l'événement doit être postérieure à son début." : null,
      capacity: r.capacity != null && r.capacity < 2 ? 'La capacité doit être d\'au moins 2 places.' : null,
    });
    if (r.communityId && !this.memberOf(this.userId).has(r.communityId)) throw new AppError('Rejoignez la communauté pour y organiser un événement.', 403);
    const created: CommunityEvent = {
      id: newId(), ...r, title: r.title.trim(), description: r.description.trim(), city: r.city.trim(), country: r.country.trim(),
      address: r.address.trim(), organizerId: this.userId, imagePath: r.imagePath ?? 'Events/event_01.jpg', participantIds: [this.userId],
    };
    this.store.events.push(created);
    return created;
  }

  async toggleParticipation(eventId: string) {
    const e = await this.getEvent(eventId);
    const participating = !e.participantIds.includes(this.userId);
    if (participating && isEventFull(e)) throw new AppError('Cet événement est complet.', 400);
    const event = this.replace(this.store.events, {
      ...e, participantIds: participating ? [...e.participantIds, this.userId] : e.participantIds.filter((p) => p !== this.userId),
    });
    return { isParticipating: participating, event };
  }

  // --- Notifications ------------------------------------------------------------------------

  async getNotifications(category?: AppNotification['category']) {
    await this.net();
    return this.store.notifications
      .filter((n) => n.userId === this.userId && (!category || n.category === category))
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }

  async getUnreadNotificationCount() {
    await this.net();
    return this.store.notifications.filter((n) => n.userId === this.userId && !n.isRead).length;
  }

  // Mode démo : pas de serveur, donc pas de notification push.
  async registerPushDevice() {}

  async unregisterPushDevice() {}

  async markNotificationRead(id: string) {
    await this.net();
    const n = this.store.notifications.find((x) => x.id === id && x.userId === this.userId);
    if (!n) throw new AppError('Notification introuvable.', 404);
    return this.replace(this.store.notifications, { ...n, isRead: true });
  }

  async markAllNotificationsRead() {
    await this.net();
    let count = 0;
    this.store.notifications = this.store.notifications.map((n) => {
      if (n.userId === this.userId && !n.isRead) {
        count++;
        return { ...n, isRead: true };
      }
      return n;
    });
    return count;
  }
}
