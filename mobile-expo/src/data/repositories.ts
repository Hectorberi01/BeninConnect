import type { PrivacySettings, AppInstallationInfo, AppVersionStatus, HomeBanner,
  AppNotification, AuthResult, Comment, Community, CommunityEvent, CommunityMember, Conversation, ConversationSummary,
  CreateCommunityRequest, CreateEventRequest, CreatePostRequest, DocumentKind, EventFilter, JobFilter, JobFilters, JobOffer, JobOfferSummary, JobPage,
  MembershipStatus, Message, MyProvider, OfferingInput, ProviderCard, ProviderDetail, ProviderDocument, ProviderInput, ProviderPage, ReviewPage,
  ServiceCategory, ServiceOffering, ServiceReportTarget, ServiceRequestItem, ServiceReview, ServiceSearch, ServicesInfo,
  Booking, BookingInput, DeliveryCode, MyTrip, RouteAlert, RouteAlertInput, TripDetail, TripInput, TripPage, TripSearch, VoyageReportTarget,
  VoyageReview, VoyageReviewPage, VoyagesInfo, VoyageStats,
  NotificationCategory, OutgoingFile, Paged, Post, ReportReason, ReportTarget, PostFilter, RegisterRequest, UpdatePostRequest, UpdateProfileRequest, User, LocalPhoto, LocalVideo } from './types';

/**
 * Contrat unique de la couche données : implémenté par le mock hors ligne et par le client HTTP de l'API.
 * L'utilisateur courant est implicite (session mock ou jeton JWT).
 */
export interface DataSource {
  readonly kind: 'demo' | 'api';
  /** Adresse de l'API (source « api » seulement). */
  readonly apiUrl?: string;
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
  /** Code reçu par email (6 chiffres) et nouveau mot de passe. */
  resetPassword(email: string, code: string, newPassword: string): Promise<void>;
  /** Restaure la session à partir du jeton mémorisé (null si invalide). */
  /** null : jeton refusé (expiré, compte supprimé) ; exception si le serveur est injoignable. */
  restore(token: string): Promise<User | null>;
  setToken(token: string | null): void;

  // Membres
  getUser(id: string): Promise<User>;
  getUsers(ids: string[]): Promise<User[]>;
  searchUsers(search?: string): Promise<User[]>;
  updateProfile(request: UpdateProfileRequest): Promise<User>;
  /** Suppression définitive du compte connecté (mot de passe de confirmation). */
  deleteAccount(password: string): Promise<void>;
  /** Blocage : plus de contenus ni de messages entre les deux membres. */
  blockUser(userId: string): Promise<void>;
  unblockUser(userId: string): Promise<void>;
  getBlockedUsers(): Promise<User[]>;
  getPrivacy(): Promise<PrivacySettings>;
  updatePrivacy(settings: PrivacySettings): Promise<PrivacySettings>;

  // Publications
  getFeed(skip: number, take: number): Promise<Paged<Post>>;
  getPost(id: string): Promise<Post>;
  getPosts(filter: PostFilter): Promise<Post[]>;
  /** Enregistrements (visibles par moi seul). */
  toggleSave(postId: string): Promise<boolean>;
  getSavedPosts(): Promise<Post[]>;
  getSavedPostIds(): Promise<string[]>;
  /** Épingle sur le profil (3 au plus) ; réservé à l'auteur. */
  pinPost(postId: string, pinned: boolean): Promise<Post>;
  /** Commentaire épinglé (null : retiré) ; réservé à l'auteur de la publication. */
  pinComment(postId: string, commentId: string | null): Promise<Post>;
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
  addComment(postId: string, content: string, options?: { parentId?: string | null; mentions?: string[] }): Promise<Comment>;
  /** Envoie une photo de l'appareil ; retourne le chemin à enregistrer (profil, couverture, publication). */
  uploadImage(photo: LocalPhoto): Promise<string>;
  /** Vidéo d'une publication (60 Mo) ; onProgress reçoit 0 à 1. Retourne le chemin à enregistrer (videoPath). */
  uploadVideo(video: LocalVideo, onProgress?: (ratio: number) => void): Promise<string>;
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
  /** Créateur et modérateurs : Covers/… ou uploads/…. */
  changeCommunityCover(communityId: string, coverImage: string): Promise<Community>;
  joinCommunity(id: string): Promise<MembershipStatus>;
  leaveCommunity(id: string): Promise<MembershipStatus>;
  cancelJoinRequest(id: string): Promise<MembershipStatus>;

  // Messagerie
  getConversations(): Promise<ConversationSummary[]>;
  getConversation(id: string): Promise<ConversationSummary>;
  getMessages(conversationId: string): Promise<Message[]>;
  /** replyToId : message cité (même discussion). */
  sendMessage(conversationId: string, content: string, replyToId?: string | null): Promise<Message>;
  /** Envoie une publication dans la discussion (avec un mot facultatif). */
  sendPost(conversationId: string, postId: string, content?: string): Promise<Message>;
  /** Scénario de démonstration explicite : réponse marquée « simulée ». */
  simulateReply(conversationId: string, content: string): Promise<Message>;
  reactToMessage(conversationId: string, messageId: string, emoji: string): Promise<Message>;
  /** Par l'expéditeur, dans les 15 minutes suivant l'envoi. */
  editMessage(conversationId: string, messageId: string, content: string): Promise<Message>;
  /** Par l'expéditeur : le message reste affiché « Message supprimé ». */
  deleteMessage(conversationId: string, messageId: string): Promise<Message>;
  /** Retire la discussion de ma liste (pour moi seulement) ; elle revient au prochain message. */
  deleteConversation(conversationId: string): Promise<void>;
  /** Photo, PDF ou message vocal (20 Mo), avec une légende facultative. */
  sendAttachment(conversationId: string, file: OutgoingFile, caption?: string, replyToId?: string | null): Promise<Message>;
  /** Lien signé de la pièce jointe (10 min), pour l'image, le lecteur audio ou le navigateur ; le jeton n'est jamais dans l'URL. */
  attachmentLink(message: Message): Promise<string | null>;
  markConversationRead(conversationId: string): Promise<number>;
  startConversation(otherUserId: string): Promise<Conversation>;
  /** Discussion avec le compte officiel (support), créée au besoin. */
  openSupport(): Promise<Conversation>;
  getUnreadMessageCount(): Promise<number>;

  // Événements
  getEvents(filter: EventFilter): Promise<CommunityEvent[]>;
  getEvent(id: string): Promise<CommunityEvent>;
  getCommunityEvents(communityId: string): Promise<CommunityEvent[]>;
  getEventCategories(): Promise<string[]>;
  getEventCities(): Promise<string[]>;
  createEvent(request: CreateEventRequest): Promise<CommunityEvent>;
  toggleParticipation(eventId: string): Promise<{ isParticipating: boolean; event: CommunityEvent }>;

  // Emploi (offres importées, candidature chez la source)
  searchJobs(filter: JobFilter, cursor?: string | null): Promise<JobPage>;
  /** Offre expirée : encore lisible ; masquée : introuvable. */
  getJob(id: string): Promise<JobOffer>;
  getJobFilters(): Promise<JobFilters>;
  getFavoriteJobs(): Promise<JobOfferSummary[]>;
  setJobFavorite(id: string, favorite: boolean): Promise<void>;
  /** Un signalement par membre et par offre. */
  reportJob(id: string, reason: string): Promise<void>;

  // Services entre membres (mise en relation, aucun paiement)
  getServiceCategories(): Promise<ServiceCategory[]>;
  getServicesInfo(): Promise<ServicesInfo>;
  searchServices(filter: ServiceSearch, cursor?: string | null): Promise<ProviderPage>;
  getProvider(id: string): Promise<ProviderDetail>;
  getProviderReviews(id: string, cursor?: string | null): Promise<ReviewPage>;
  setProviderFavorite(id: string, favorite: boolean): Promise<void>;
  getFavoriteProviders(): Promise<ProviderCard[]>;
  /** null : pas encore de profil prestataire. */
  getMyProvider(): Promise<MyProvider | null>;
  saveMyProvider(input: ProviderInput, qualificationDeclared: boolean): Promise<MyProvider>;
  submitMyProvider(): Promise<MyProvider>;
  saveOffering(id: string | null, input: OfferingInput): Promise<ServiceOffering>;
  deleteOffering(id: string): Promise<void>;
  /** Photo de profil ou de portfolio (5 Mo, métadonnées retirées par le serveur). */
  uploadServiceImage(photo: LocalPhoto): Promise<string>;
  uploadProviderDocument(kind: DocumentKind, file: OutgoingFile): Promise<ProviderDocument>;
  deleteProviderDocument(id: string): Promise<void>;
  createServiceRequest(offeringId: string, message: string, preferredDate: string | null, city: string | null): Promise<ServiceRequestItem>;
  getServiceRequests(role: 'Client' | 'Provider'): Promise<ServiceRequestItem[]>;
  /** Ouverte par le prestataire : passe en « vue ». */
  getServiceRequest(id: string): Promise<ServiceRequestItem>;
  respondServiceRequest(id: string, action: 'accept' | 'decline' | 'complete' | 'cancel', reason?: string): Promise<ServiceRequestItem>;
  reviewServiceRequest(requestId: string, rating: number, comment: string | null): Promise<ServiceReview>;
  editServiceReview(id: string, rating: number, comment: string | null): Promise<ServiceReview>;
  replyServiceReview(id: string, reply: string): Promise<ServiceReview>;
  reportService(target: ServiceReportTarget, id: string, reason: string): Promise<void>;

  // Voyages : des membres proposent des kilos sur leur trajet (mise en relation, aucun paiement)
  /** Module affiché par l'équipe (masqué : écrans et sections cachés). */
  getVoyagesStatus(): Promise<{ enabled: boolean }>;
  getVoyagesInfo(): Promise<VoyagesInfo>;
  searchTrips(filter: TripSearch, cursor?: string | null): Promise<TripPage>;
  getTrip(id: string): Promise<TripDetail>;
  publishTrip(input: TripInput): Promise<TripDetail>;
  updateTrip(id: string, input: TripInput): Promise<TripDetail>;
  cancelTrip(id: string): Promise<TripDetail>;
  getMyTrips(): Promise<MyTrip[]>;
  requestBooking(tripId: string, input: BookingInput): Promise<Booking>;
  getMyBookings(): Promise<Booking[]>;
  respondBooking(id: string, action: 'accept' | 'decline' | 'cancel', reason?: string): Promise<Booking>;
  /** Nouveau code de remise (l'ancien ne marche plus) : montré une seule fois à l'expéditeur. */
  issueDeliveryCode(id: string): Promise<DeliveryCode>;
  deliverBooking(id: string, code: string): Promise<Booking>;
  disputeBooking(id: string, reason: string): Promise<Booking>;
  reviewBooking(id: string, rating: number, comment: string | null): Promise<VoyageReview>;
  getVoyageStats(userId: string): Promise<VoyageStats>;
  getVoyageReviews(userId: string): Promise<VoyageReviewPage>;
  getRouteAlerts(): Promise<RouteAlert[]>;
  createRouteAlert(input: RouteAlertInput): Promise<RouteAlert>;
  deleteRouteAlert(id: string): Promise<void>;
  reportVoyage(target: VoyageReportTarget, id: string, reason: string): Promise<void>;

  // Notifications
  getNotifications(category?: NotificationCategory): Promise<AppNotification[]>;
  getUnreadNotificationCount(): Promise<number>;
  markNotificationRead(id: string): Promise<AppNotification>;
  /** Notifications push : téléphone enregistré pour le membre connecté (catégories coupées incluses). */
  registerPushDevice(token: string, platform: 'ios' | 'android', mutedCategories: NotificationCategory[]): Promise<void>;
  unregisterPushDevice(token: string): Promise<void>;
  markAllNotificationsRead(): Promise<number>;
  deleteNotification(id: string): Promise<void>;
  /** Supprime toutes mes notifications (ou celles d'une catégorie) ; renvoie le nombre supprimé. */
  clearNotifications(category?: NotificationCategory): Promise<number>;

  // Versions de l'application
  /** Version installée sur ce téléphone (membre lu dans le jeton) ; renvoie le statut de mise à jour. */
  reportInstallation(info: AppInstallationInfo): Promise<AppVersionStatus>;

  /** Bannières de l'accueil en ligne (vide : bannières par défaut de l'application). */
  getHomeBanners(): Promise<HomeBanner[]>;
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
