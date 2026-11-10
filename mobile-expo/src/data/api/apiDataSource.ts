import { PRODUCTION_API_URL } from '@/core/config';
import type { DataSource } from '../repositories';
import type {
  AppNotification, AuthResult, Comment, Community, CommunityEvent, CommunityMember, Conversation, ConversationSummary,
  CreateCommunityRequest, CreateEventRequest, CreatePostRequest, EventFilter, MembershipStatus, Message, NotificationCategory,
  LocalPhoto, LocalVideo, OutgoingFile, Paged, Post, PostFilter, ReportReason, ReportTarget, RegisterRequest, UpdatePostRequest, UpdateProfileRequest, User,
} from '../types';
import { Platform } from 'react-native';
import { AppError } from '../types';
import { xhrFetch } from './xhrFetch';

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE';

/** Client de l'API ASP.NET Core (server/) : JWT, JSON, erreurs ProblemDetails converties en messages lisibles. */
export class ApiDataSource implements DataSource {
  readonly kind = 'api' as const;
  private token: string | null = null;

  /** API de production : vrais emails, pas de compte de démonstration. */
  readonly isProduction: boolean;
  readonly demoVerificationCode: string | null;
  readonly hasDemoAccount: boolean;

  constructor(
    private readonly baseUrl: string,
    private readonly fetchImpl: typeof fetch = (...args) => fetch(...args),
    private readonly timeoutMs = 15_000,
    // Envois de fichiers : XMLHttpRequest sur mobile (expo/fetch ne lit pas les fichiers { uri }).
    private readonly uploadImpl: typeof fetch = Platform.OS === 'web' ? fetchImpl : xhrFetch,
  ) {
    this.isProduction = baseUrl.replace(/\/+$/, '') === PRODUCTION_API_URL;
    this.demoVerificationCode = this.isProduction ? null : '123456';
    this.hasDemoAccount = !this.isProduction;
  }

  /** Temps réel (SignalR) : adresse de l'API et jeton courant. */
  get apiUrl() {
    return this.baseUrl.replace(/\/$/, '');
  }

  get currentToken() {
    return this.token;
  }

  setToken(token: string | null) {
    this.token = token;
  }

  private async request<T>(method: Method, path: string, body?: unknown, timeoutMs = this.timeoutMs): Promise<T> {
    const form = body instanceof FormData;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    let response: Response;
    try {
      response = await (form ? this.uploadImpl : this.fetchImpl)(`${this.baseUrl.replace(/\/$/, '')}/${path.replace(/^\//, '')}`, {
        method,
        headers: {
          Accept: 'application/json',
          // ngrok gratuit : évite sa page d'avertissement HTML à la place de la réponse.
          'ngrok-skip-browser-warning': '1',
          ...(body !== undefined && !form ? { 'Content-Type': 'application/json' } : {}),
          ...(this.token ? { Authorization: `Bearer ${this.token}` } : {}),
        },
        body: form ? body : body !== undefined ? JSON.stringify(body) : undefined,
        signal: controller.signal,
      });
    } catch (error) {
      if (__DEV__) console.warn(`[api] ${method} ${path}`, error);
      if (controller.signal.aborted) throw new AppError("Le serveur met trop de temps à répondre. Vérifiez votre connexion et réessayez.");
      throw new AppError("Impossible de joindre le serveur. Vérifiez votre connexion et l'adresse de l'API.");
    } finally {
      clearTimeout(timer);
    }

    if (response.ok) {
      if (response.status === 204) return undefined as T;
      return (await response.json()) as T;
    }

    let detail: string | undefined;
    try {
      const problem = (await response.json()) as { title?: string; detail?: string };
      detail = problem.detail || problem.title;
    } catch {
      // corps vide ou non JSON
    }

    if (response.status === 401) throw new AppError('Votre session a expiré. Reconnectez-vous.', 401);
    if (response.status >= 500) throw new AppError('Le serveur a rencontré une erreur. Réessayez.', response.status);
    throw new AppError(detail ?? 'La requête a été refusée.', response.status);
  }

  private get = <T>(path: string) => this.request<T>('GET', path);
  private post = <T>(path: string, body: unknown = {}) => this.request<T>('POST', path, body);
  private q = (params: Record<string, string | undefined>) => {
    const s = Object.entries(params).filter(([, v]) => v).map(([k, v]) => `${k}=${encodeURIComponent(v!)}`).join('&');
    return s ? `?${s}` : '';
  };

  // Authentification
  login = (email: string, password: string) => this.post<AuthResult>('api/auth/login', { email, password });
  register = (r: RegisterRequest) => this.post<User>('api/auth/register', r);
  verifyEmail = (email: string, code: string) => this.post<AuthResult>('api/auth/verify-email', { email, code });
  resendCode = (email: string) => this.post<void>('api/auth/resend-code', { email });
  forgotPassword = (email: string) => this.post<void>('api/auth/forgot-password', { email });

  async restore(token: string) {
    this.token = token;
    try {
      return await this.get<User>('api/auth/me');
    } catch {
      this.token = null;
      return null;
    }
  }

  // Membres
  getUser = (id: string) => this.get<User>(`api/users/${id}`);
  getUsers = async (ids: string[]) => (ids.length ? this.post<User[]>('api/users/batch', { ids: [...new Set(ids)] }) : []);
  searchUsers = (search?: string) => this.get<User[]>(`api/users${this.q({ search })}`);
  updateProfile = (r: UpdateProfileRequest) => this.request<User>('PUT', 'api/users/me', r);

  // Publications
  getFeed = (skip: number, take: number) => this.get<Paged<Post>>(`api/feed?skip=${skip}&take=${take}`);
  getPost = (id: string) => this.get<Post>(`api/posts/${id}`);
  getPosts = (f: PostFilter) => this.get<Post[]>(`api/posts${this.q({ ...f })}`);
  toggleSave = async (id: string) => (await this.post<{ saved: boolean }>(`api/posts/${id}/save`)).saved;
  getSavedPosts = () => this.get<Post[]>('api/posts/saved');
  getSavedPostIds = () => this.get<string[]>('api/posts/saved/ids');
  pinPost = (id: string, pinned: boolean) => this.request<Post>(pinned ? 'POST' : 'DELETE', `api/posts/${id}/pin`);
  pinComment = (id: string, commentId: string | null) => this.request<Post>('PUT', `api/posts/${id}/pinned-comment`, { commentId });
  createPost = (r: CreatePostRequest) => this.post<Post>('api/posts', r);
  updatePost = (id: string, r: UpdatePostRequest) => this.request<Post>('PUT', `api/posts/${id}`, r);
  toggleLike = (id: string) => this.post<Post>(`api/posts/${id}/like`);
  sharePost = (id: string) => this.post<Post>(`api/posts/${id}/share`);
  deletePost = async (id: string) => { await this.request<void>('DELETE', `api/posts/${id}`); };
  report = async (targetType: ReportTarget, targetId: string, reason: ReportReason, details?: string) => {
    await this.post<{ id: string }>('api/reports', { targetType, targetId, reason, details: details ?? null });
  };
  deleteAccount = async (password: string) => { await this.post<void>('api/users/me/delete', { password }); };
  blockUser = async (id: string) => { await this.post<void>(`api/users/${id}/block`); };
  unblockUser = async (id: string) => { await this.request<void>('DELETE', `api/users/${id}/block`); };
  getBlockedUsers = () => this.get<User[]>('api/users/me/blocked');
  getComments = (id: string) => this.get<Comment[]>(`api/posts/${id}/comments`);
  async uploadImage(photo: LocalPhoto) {
    const type = photo.mimeType || 'image/jpeg';
    const name = photo.fileName || `photo.${type.split('/')[1] ?? 'jpg'}`;
    const form = new FormData();
    if (Platform.OS === 'web') {
      form.append('file', await (await fetch(photo.uri)).blob(), name);
    } else {
      // Format React Native : le fichier est lu par le moteur réseau natif.
      form.append('file', { uri: photo.uri, name, type } as unknown as Blob);
    }
    // Photo de plusieurs Mo : délai plus long que les autres requêtes (réseau mobile lent)
    return (await this.request<{ path: string }>('POST', 'api/media', form, 90_000)).path;
  }

  /** XMLHttpRequest direct : progression d'envoi (une vidéo peut prendre une minute sur réseau mobile). */
  uploadVideo(video: LocalVideo, onProgress?: (ratio: number) => void) {
    return new Promise<string>((resolve, reject) => {
      const form = new FormData();
      form.append('file', { uri: video.uri, name: video.fileName || 'video.mp4', type: video.mimeType || 'video/mp4' } as unknown as Blob);
      const xhr = new XMLHttpRequest();
      xhr.open('POST', `${this.apiUrl}/api/media/video`);
      xhr.setRequestHeader('Accept', 'application/json');
      if (this.token) xhr.setRequestHeader('Authorization', `Bearer ${this.token}`);
      xhr.timeout = 10 * 60_000;
      xhr.upload.onprogress = (e) => { if (e.lengthComputable) onProgress?.(e.loaded / e.total); };
      xhr.onload = () => {
        let body: { path?: string; detail?: string; title?: string } = {};
        try { body = JSON.parse(xhr.responseText || '{}'); } catch { /* réponse non JSON */ }
        if (xhr.status >= 200 && xhr.status < 300 && body.path) return resolve(body.path);
        if (xhr.status === 401) return reject(new AppError('Votre session a expiré. Reconnectez-vous.', 401));
        if (xhr.status === 413) return reject(new AppError('La vidéo ne doit pas dépasser 60 Mo.', 413));
        reject(new AppError(body.detail || body.title || "Envoi de la vidéo impossible.", xhr.status || undefined));
      };
      xhr.onerror = () => reject(new AppError("Impossible de joindre le serveur. Vérifiez votre connexion."));
      xhr.ontimeout = () => reject(new AppError("L'envoi de la vidéo a pris trop de temps. Réessayez en Wi-Fi."));
      xhr.send(form);
    });
  }

  addComment = (id: string, content: string, o?: { parentId?: string | null; mentions?: string[] }) =>
    this.post<Comment>(`api/posts/${id}/comments`, { content, parentId: o?.parentId ?? null, mentions: o?.mentions ?? [] });
  reactToComment = (postId: string, commentId: string, emoji: string) =>
    this.post<Comment>(`api/posts/${postId}/comments/${commentId}/reactions`, { emoji });

  // Communautés
  getCommunities = (search?: string) => this.get<Community[]>(`api/communities${this.q({ search })}`);
  getCommunity = (id: string) => this.get<Community>(`api/communities/${id}`);
  getCommunitiesByIds = async (ids: string[]) => (ids.length ? this.post<Community[]>('api/communities/batch', { ids: [...new Set(ids)] }) : []);
  getMemberCommunities = (userId: string) => this.get<Community[]>(`api/users/${userId}/communities`);
  getCommunityCategories = () => this.get<string[]>('api/communities/categories');
  getMembers = (id: string) => this.get<CommunityMember[]>(`api/communities/${id}/members`);
  getMyJoinRequests = () => this.get<string[]>('api/communities/join-requests/mine');
  createCommunity = (r: CreateCommunityRequest) => this.post<Community>('api/communities', r);
  joinCommunity = async (id: string) => (await this.post<{ status: MembershipStatus }>(`api/communities/${id}/join`)).status;
  leaveCommunity = async (id: string) => (await this.request<{ status: MembershipStatus }>('DELETE', `api/communities/${id}/membership`)).status;
  cancelJoinRequest = async (id: string) => (await this.request<{ status: MembershipStatus }>('DELETE', `api/communities/${id}/join-request`)).status;

  // Messagerie
  getConversations = () => this.get<ConversationSummary[]>('api/conversations');
  getConversation = (id: string) => this.get<ConversationSummary>(`api/conversations/${id}`);
  getMessages = (id: string) => this.get<Message[]>(`api/conversations/${id}/messages`);
  sendMessage = (id: string, content: string) => this.post<Message>(`api/conversations/${id}/messages`, { content });
  sendPost = (id: string, postId: string, content?: string) => this.post<Message>(`api/conversations/${id}/messages`, { content: content ?? '', postId });
  editMessage = (id: string, messageId: string, content: string) =>
    this.request<Message>('PUT', `api/conversations/${id}/messages/${messageId}`, { content });
  deleteMessage = (id: string, messageId: string) => this.request<Message>('DELETE', `api/conversations/${id}/messages/${messageId}`);
  deleteConversation = async (id: string) => { await this.request('DELETE', `api/conversations/${id}`); };
  async sendAttachment(id: string, file: OutgoingFile, caption?: string) {
    const form = new FormData();
    if (Platform.OS === 'web') {
      form.append('file', await (await fetch(file.uri)).blob(), file.name);
    } else {
      form.append('file', { uri: file.uri, name: file.name, type: file.mimeType } as unknown as Blob);
    }
    if (caption?.trim()) form.append('content', caption.trim());
    if (file.durationMs) form.append('durationMs', String(Math.round(file.durationMs)));
    // Jusqu'à 20 Mo : délai long (réseau mobile)
    return this.request<Message>('POST', `api/conversations/${id}/attachments`, form, 120_000);
  }

  /** Le jeton est dans l'URL : l'image, le lecteur audio et le navigateur ne peuvent pas envoyer d'en-tête. */
  attachmentUrl(message: Message) {
    if (!message.attachment || !this.token) return null;
    return `${this.apiUrl}/api/messages/${message.id}/attachment?access_token=${encodeURIComponent(this.token)}`;
  }

  simulateReply = (id: string, content: string) => this.post<Message>(`api/conversations/${id}/demo-reply`, { content });
  reactToMessage = (id: string, messageId: string, emoji: string) =>
    this.post<Message>(`api/conversations/${id}/messages/${messageId}/reactions`, { emoji });
  markConversationRead = async (id: string) => (await this.post<{ count: number }>(`api/conversations/${id}/read`)).count;
  startConversation = (otherUserId: string) => this.post<Conversation>('api/conversations/direct', { otherUserId });
  getUnreadMessageCount = async () => (await this.get<{ count: number }>('api/conversations/unread-count')).count;

  // Événements
  getEvents = (f: EventFilter) =>
    this.get<CommunityEvent[]>(`api/events${this.q({ search: f.search, category: f.category, city: f.city, period: f.period ?? 'All' })}`);
  getEvent = (id: string) => this.get<CommunityEvent>(`api/events/${id}`);
  getCommunityEvents = (id: string) => this.get<CommunityEvent[]>(`api/communities/${id}/events`);
  getEventCategories = () => this.get<string[]>('api/events/categories');
  getEventCities = () => this.get<string[]>('api/events/cities');
  createEvent = (r: CreateEventRequest) => this.post<CommunityEvent>('api/events', r);
  toggleParticipation = (id: string) => this.post<{ isParticipating: boolean; event: CommunityEvent }>(`api/events/${id}/participation`);

  // Notifications
  getNotifications = (category?: NotificationCategory) => this.get<AppNotification[]>(`api/notifications${this.q({ category })}`);
  getUnreadNotificationCount = async () => (await this.get<{ count: number }>('api/notifications/unread-count')).count;
  markNotificationRead = (id: string) => this.post<AppNotification>(`api/notifications/${id}/read`);
  registerPushDevice = (token: string, platform: 'ios' | 'android', mutedCategories: NotificationCategory[]) =>
    this.post<void>('api/notifications/devices', { token, platform, mutedCategories });
  unregisterPushDevice = (token: string) => this.post<void>('api/notifications/devices/unregister', { token });
  markAllNotificationsRead = async () => (await this.post<{ count: number }>('api/notifications/read-all')).count;
  deleteNotification = async (id: string) => { await this.request('DELETE', `api/notifications/${id}`); };
  clearNotifications = async (category?: NotificationCategory) =>
    (await this.request<{ count: number }>('DELETE', `api/notifications${this.q({ category })}`)).count;
}
