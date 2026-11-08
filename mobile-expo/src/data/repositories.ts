import type {
  AppNotification, AuthResult, Comment, Community, CommunityEvent, CommunityMember, Conversation, ConversationSummary,
  CreateCommunityRequest, CreateEventRequest, CreatePostRequest, EventFilter, MembershipStatus, Message,
  NotificationCategory, OutgoingFile, Paged, Post, ReportReason, ReportTarget, RegisterRequest, UpdatePostRequest, UpdateProfileRequest, User, LocalPhoto } from './types';

/**
 * Contrat unique de la couche données : implémenté par le mock hors ligne et par le client HTTP de l'API.
 * L'utilisateur courant est implicite (session mock ou jeton JWT).
 */
export interface DataSource {
  readonly kind: 'demo' | 'api';
  /** Code de vérification fixe (démo, API de développement) ; null en production : le code arrive par email. */
  readonly demoVerificationCode: string | null;
  /** Le compte demo@beninconnect.test existe (absent en production). */
  readonly hasDemoAccount: boolean;

  // Authentification
  login(email: string, password: string): Promise<AuthResult>;
  register(request: RegisterRequest): Promise<User>;
  verifyEmail(email: string, code: string): Promise<AuthResult>;
  resendCode(email: string): Promise<void>;
  forgotPassword(email: string): Promise<void>;
  /** Restaure la session à partir du jeton mémorisé (null si invalide). */
  restore(token: string): Promise<User | null>;
  setToken(token: string | null): void;

  // Membres
  getUser(id: string): Promise<User>;
  getUsers(ids: string[]): Promise<User[]>;
  searchUsers(search?: string): Promise<User[]>;
  updateProfile(request: UpdateProfileRequest): Promise<User>;
  /** Suppression définitive du compte connecté (mot de passe de confirmation). */
  deleteAccount(password: string): Promise<void>;

  // Publications
  getFeed(skip: number, take: number): Promise<Paged<Post>>;
  getPost(id: string): Promise<Post>;
  getPosts(filter: { authorId?: string; communityId?: string; search?: string }): Promise<Post[]>;
  createPost(request: CreatePostRequest): Promise<Post>;
  /** Réservé à l'auteur ; la communauté ne change pas. */
  updatePost(postId: string, request: UpdatePostRequest): Promise<Post>;
  toggleLike(postId: string): Promise<Post>;
  sharePost(postId: string): Promise<Post>;
  /** Réservé à l'auteur. */
  deletePost(postId: string): Promise<void>;
  /** Signalement à la modération (publication, commentaire, membre ou communauté). */
  report(target: ReportTarget, targetId: string, reason: ReportReason, details?: string): Promise<void>;
  getComments(postId: string): Promise<Comment[]>;
  addComment(postId: string, content: string): Promise<Comment>;
  /** Envoie une photo de l'appareil ; retourne le chemin à enregistrer (profil, couverture, publication). */
  uploadImage(photo: LocalPhoto): Promise<string>;
  /** Même emoji : retire la réaction ; autre emoji : la remplace. */
  reactToComment(postId: string, commentId: string, emoji: string): Promise<Comment>;

  // Communautés
  getCommunities(search?: string): Promise<Community[]>;
  getCommunity(id: string): Promise<Community>;
  getCommunitiesByIds(ids: string[]): Promise<Community[]>;
  getMemberCommunities(userId: string): Promise<Community[]>;
  getCommunityCategories(): Promise<string[]>;
  getMembers(communityId: string): Promise<CommunityMember[]>;
  getMyJoinRequests(): Promise<string[]>;
  createCommunity(request: CreateCommunityRequest): Promise<Community>;
  joinCommunity(id: string): Promise<MembershipStatus>;
  leaveCommunity(id: string): Promise<MembershipStatus>;
  cancelJoinRequest(id: string): Promise<MembershipStatus>;

  // Messagerie
  getConversations(): Promise<ConversationSummary[]>;
  getConversation(id: string): Promise<ConversationSummary>;
  getMessages(conversationId: string): Promise<Message[]>;
  sendMessage(conversationId: string, content: string): Promise<Message>;
  /** Scénario de démonstration explicite : réponse marquée « simulée ». */
  simulateReply(conversationId: string, content: string): Promise<Message>;
  reactToMessage(conversationId: string, messageId: string, emoji: string): Promise<Message>;
  /** Par l'expéditeur, dans les 15 minutes suivant l'envoi. */
  editMessage(conversationId: string, messageId: string, content: string): Promise<Message>;
  /** Par l'expéditeur : le message reste affiché « Message supprimé ». */
  deleteMessage(conversationId: string, messageId: string): Promise<Message>;
  /** Photo, PDF ou message vocal (20 Mo), avec une légende facultative. */
  sendAttachment(conversationId: string, file: OutgoingFile, caption?: string): Promise<Message>;
  /** Adresse de la pièce jointe, utilisable par l'image, le lecteur audio ou le navigateur (accès vérifié par le serveur). */
  attachmentUrl(message: Message): string | null;
  markConversationRead(conversationId: string): Promise<number>;
  startConversation(otherUserId: string): Promise<Conversation>;
  getUnreadMessageCount(): Promise<number>;

  // Événements
  getEvents(filter: EventFilter): Promise<CommunityEvent[]>;
  getEvent(id: string): Promise<CommunityEvent>;
  getCommunityEvents(communityId: string): Promise<CommunityEvent[]>;
  getEventCategories(): Promise<string[]>;
  getEventCities(): Promise<string[]>;
  createEvent(request: CreateEventRequest): Promise<CommunityEvent>;
  toggleParticipation(eventId: string): Promise<{ isParticipating: boolean; event: CommunityEvent }>;

  // Notifications
  getNotifications(category?: NotificationCategory): Promise<AppNotification[]>;
  getUnreadNotificationCount(): Promise<number>;
  markNotificationRead(id: string): Promise<AppNotification>;
  /** Notifications push : téléphone enregistré pour le membre connecté (catégories coupées incluses). */
  registerPushDevice(token: string, platform: 'ios' | 'android', mutedCategories: NotificationCategory[]): Promise<void>;
  unregisterPushDevice(token: string): Promise<void>;
  markAllNotificationsRead(): Promise<number>;
}

export const INTERESTS = [
  'Entrepreneuriat', 'Tech & numérique', 'Cuisine béninoise', 'Musique', 'Football', 'Études',
  'Vie associative', 'Voyages', 'Patrimoine & culture', 'Mode', 'Littérature', 'Investissement',
  'Santé & bien-être', 'Photographie', 'Danse', 'Cinéma', 'Agriculture', 'Immobilier',
];

/** Réactions proposées (mêmes valeurs que l'API). */
export const REACTIONS = ['❤️', '👍', '😂', '😮', '😢', '🙏'];

export const DEMO_EMAIL = 'demo@beninconnect.test';
export const DEMO_PASSWORD = 'Demo123!';
