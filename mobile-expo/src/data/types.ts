// Modèles partagés par les sources de données (mock et API). Mêmes formes que les DTO de l'API (server/).
// Les dates sont des chaînes ISO 8601.

export type PostVisibility = 'Public' | 'CommunityMembers' | 'OnlyMe';
export type CommunityVisibility = 'Public' | 'Private';
export type MembershipStatus = 'None' | 'Pending' | 'Member' | 'Owner';
export type CommunityRole = 'Member' | 'Moderator' | 'Owner';
export type EventPeriod = 'All' | 'ThisWeek' | 'ThisMonth' | 'Later';
export type NotificationCategory = 'Social' | 'Communities' | 'Messages' | 'Events' | 'System';
/** Contenu signalable à la modération. */
export type ReportTarget = 'Post' | 'Comment' | 'User' | 'Community';
export type ReportReason = 'Spam' | 'Harassment' | 'Inappropriate' | 'FalseInformation' | 'Other';
export type NotificationTarget = 'None' | 'Post' | 'Community' | 'Conversation' | 'Event' | 'Profile';

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  profession: string;
  country: string;
  city: string;
  originCity: string;
  bio: string;
  interests: string[];
  avatarColor: string;
  /** Photo de profil (uploads/… côté API, fichier local en démo) ; sinon initiales. */
  avatarImage?: string | null;
  coverImage: string | null;
  joinedAt: string;
  isEmailVerified: boolean;
  /** Compte officiel BéninConnect : badge vérifié, ne reçoit pas de messages. */
  isOfficial?: boolean;
}

export interface Post {
  id: string;
  authorId: string;
  communityId: string | null;
  content: string;
  imagePath: string | null;
  createdAt: string;
  visibility: PostVisibility;
  likedBy: string[];
  commentCount: number;
  shareCount: number;
  /** Date de la dernière modification par l'auteur. */
  editedAt?: string | null;
  /** Vidéo (imagePath : sa miniature). */
  videoPath?: string | null;
  /** Carrousel de photos (10 au plus) ; imagePath = la première. */
  images?: string[];
  /** Membres mentionnés (@Prénom Nom dans le texte). */
  mentionIds?: string[];
  /** Épinglée en tête du profil de l'auteur. */
  pinnedAt?: string | null;
  /** Commentaire épinglé par l'auteur. */
  pinnedCommentId?: string | null;
}

/** Photos d'une publication (carrousel, ou l'image seule des anciennes publications). */
export const postImages = (p: Post) =>
  p.images?.length ? p.images : p.imagePath && !p.videoPath ? [p.imagePath] : [];

export interface Reaction {
  userId: string;
  emoji: string;
}

export interface Comment {
  id: string;
  postId: string;
  authorId: string;
  content: string;
  createdAt: string;
  reactions: Reaction[];
  /** Réponse à ce commentaire (un seul niveau). */
  parentId?: string | null;
  mentionIds?: string[];
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  skip: number;
}

export interface Community {
  id: string;
  name: string;
  description: string;
  category: string;
  country: string;
  city: string;
  visibility: CommunityVisibility;
  coverImage: string | null;
  ownerId: string;
  moderatorIds: string[];
  createdAt: string;
  memberIds: string[];
}

export interface CommunityMember {
  user: User;
  role: CommunityRole;
}

export interface Conversation {
  id: string;
  participantIds: string[];
  createdAt: string;
}

export type AttachmentKind = 'Image' | 'Audio' | 'File';

/** Pièce jointe privée d'un message : téléchargée via data().attachmentUrl(message). */
export interface Attachment {
  kind: AttachmentKind;
  fileName: string;
  contentType: string;
  size: number;
  durationMs?: number | null;
}

/** Fichier de l'appareil à envoyer dans une discussion. */
export interface OutgoingFile {
  uri: string;
  name: string;
  mimeType: string;
  durationMs?: number;
}

export interface Message {
  id: string;
  conversationId: string;
  senderId: string;
  content: string;
  sentAt: string;
  isRead: boolean;
  isSimulated: boolean;
  reactions: Reaction[];
  editedAt?: string | null;
  /** Supprimé par l'expéditeur : contenu vide, affiché « Message supprimé ». */
  isDeleted?: boolean;
  attachment?: Attachment | null;
  /** Publication envoyée dans la discussion. */
  sharedPostId?: string | null;
}

/** Aperçu d'un message (liste des conversations). */
export function messagePreview(m: Message) {
  if (m.isDeleted) return 'Message supprimé';
  if (m.sharedPostId && !m.content) return '📰 Publication partagée';
  switch (m.attachment?.kind) {
    case 'Image': return m.content ? `📷 ${m.content}` : '📷 Photo';
    case 'Audio': return '🎤 Message vocal';
    case 'File': return `📄 ${m.attachment.fileName}`;
    default: return m.content;
  }
}

export interface ConversationSummary {
  conversation: Conversation;
  otherParticipant: User;
  lastMessage: Message | null;
  unreadCount: number;
  /** L'un des deux a bloqué l'autre : plus aucun message possible. */
  isBlocked?: boolean;
  /** Présence de l'autre membre (temps réel). */
  otherOnline?: boolean;
  otherLastSeenAt?: string | null;
}

export interface CommunityEvent {
  id: string;
  title: string;
  description: string;
  category: string;
  city: string;
  country: string;
  address: string;
  startsAt: string;
  endsAt: string;
  organizerId: string;
  communityId: string | null;
  imagePath: string | null;
  capacity: number | null;
  participantIds: string[];
}

export interface AppNotification {
  id: string;
  userId: string;
  category: NotificationCategory;
  title: string;
  body: string;
  createdAt: string;
  isRead: boolean;
  target: NotificationTarget;
  targetId: string | null;
}

export interface PostFilter {
  authorId?: string;
  communityId?: string;
  search?: string;
  /** Hashtag, sans #. */
  tag?: string;
}

export interface EventFilter {
  search?: string;
  category?: string;
  city?: string;
  period?: EventPeriod;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  country: string;
  city: string;
  password: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  profession: string;
  country: string;
  city: string;
  bio: string;
  interests: string[];
  /** null/absent : inchangé ; chaîne vide : retirée. */
  avatarImage?: string | null;
  coverImage?: string | null;
}

export interface UpdatePostRequest {
  content: string;
  imagePath: string | null;
  visibility: PostVisibility;
  videoPath?: string | null;
  images?: string[];
  mentions?: string[];
}

export interface CreatePostRequest {
  content: string;
  communityId: string | null;
  imagePath: string | null;
  visibility: PostVisibility;
  videoPath?: string | null;
  images?: string[];
  mentions?: string[];
}

export interface CreateCommunityRequest {
  name: string;
  description: string;
  category: string;
  country: string;
  city: string;
  visibility: CommunityVisibility;
  coverImage: string | null;
}

export interface CreateEventRequest {
  title: string;
  description: string;
  category: string;
  city: string;
  country: string;
  address: string;
  startsAt: string;
  endsAt: string;
  capacity: number | null;
  communityId: string | null;
  imagePath: string | null;
}

export interface AuthResult {
  token: string;
  user: User;
}

export const displayName = (u: Pick<User, 'firstName' | 'lastName'>) => `${u.firstName} ${u.lastName}`.trim();
export const initials = (u: Pick<User, 'firstName' | 'lastName'>) =>
  `${u.firstName.charAt(0)}${u.lastName.charAt(0)}`.toUpperCase();
export const location = (x: { city: string; country: string }) => [x.city, x.country].filter(Boolean).join(', ');

export function membershipStatus(c: Community, userId: string, pending: string[]): MembershipStatus {
  if (c.ownerId === userId) return 'Owner';
  if (c.memberIds.includes(userId)) return 'Member';
  return pending.includes(c.id) ? 'Pending' : 'None';
}

export const isEventFull = (e: CommunityEvent) => e.capacity != null && e.participantIds.length >= e.capacity;

/** Erreur métier au message lisible (affiché tel quel à l'utilisateur). */
export class AppError extends Error {
  constructor(message: string, readonly status?: number) {
    super(message);
    this.name = 'AppError';
  }
}

/** Compte de revue Apple / Google : l'API de production indique l'API de revue à utiliser (HTTP 421). */
export class ReviewRedirectError extends AppError {
  constructor(readonly url: string, message: string) {
    super(message, 421);
    this.name = 'ReviewRedirectError';
  }
}

export const errorMessage = (e: unknown) =>
  e instanceof AppError ? e.message : 'Une erreur inattendue est survenue. Réessayez.';

/** Réactions regroupées par emoji, dans l'ordre d'apparition. */
export function groupReactions(reactions: Reaction[], me: string) {
  const groups = new Map<string, { emoji: string; count: number; mine: boolean }>();
  for (const r of reactions) {
    const g = groups.get(r.emoji) ?? { emoji: r.emoji, count: 0, mine: false };
    g.count++;
    g.mine ||= r.userId === me;
    groups.set(r.emoji, g);
  }
  return [...groups.values()];
}

/** Même emoji : retire ; autre : remplace ; sinon ajoute (une réaction par membre). */
export function toggleReaction(reactions: Reaction[], userId: string, emoji: string): Reaction[] {
  const mine = reactions.find((r) => r.userId === userId);
  const others = reactions.filter((r) => r.userId !== userId);
  return mine?.emoji === emoji ? others : [...others, { userId, emoji }];
}

/** Photo choisie sur l'appareil, pas encore envoyée. */
export interface LocalPhoto {
  uri: string;
  mimeType?: string | null;
  fileName?: string | null;
}

/** Vidéo de l'appareil (compressée en 720p à la sélection), avec sa miniature. */
export interface LocalVideo extends LocalPhoto {
  durationMs: number;
  thumbnailUri: string | null;
}

// --- Emploi (offres importées de France Travail, ReliefWeb…) -------------------------------

export type JobContract = 'Cdi' | 'Cdd' | 'Interim' | 'Stage' | 'Alternance' | 'Freelance' | 'Autre';
export type JobSource = 'FranceTravail' | 'ReliefWeb' | 'Adzuna' | 'Remotive';
export type JobOfferStatus = 'Active' | 'Expired' | 'Hidden';

export interface JobOfferSummary {
  id: string;
  title: string;
  companyName: string | null;
  companyLogoUrl: string | null;
  locationLabel: string;
  country: string;
  department: string | null;
  contractType: JobContract;
  contractLabel: string | null;
  isRemote: boolean;
  publishedAt: string;
  source: JobSource;
  sourceLabel: string;
  status: JobOfferStatus;
  isFavorite: boolean;
}

export interface JobOffer extends JobOfferSummary {
  description: string;
  postalCode: string | null;
  latitude: number | null;
  longitude: number | null;
  workingTime: string | null;
  salaryLabel: string | null;
  sectorLabel: string | null;
  applyUrl: string;
  closingAt: string | null;
  reportedByMe: boolean;
}

export interface JobPage {
  items: JobOfferSummary[];
  nextCursor: string | null;
}

export interface JobFilter {
  q?: string;
  country?: string;
  department?: string;
  contract?: JobContract;
  remote?: boolean;
}

export interface LabelCount {
  label: string;
  count: number;
}

export interface JobFilters {
  total: number;
  remote: number;
  countries: LabelCount[];
  departments: LabelCount[];
  contracts: LabelCount[];
  sources: LabelCount[];
}

export const CONTRACT_LABELS: Record<JobContract, string> = {
  Cdi: 'CDI', Cdd: 'CDD', Interim: 'Intérim', Stage: 'Stage', Alternance: 'Alternance', Freelance: 'Freelance', Autre: 'Autre',
};

const COUNTRY_NAMES: Record<string, string> = {
  FR: 'France', BJ: 'Bénin', TG: 'Togo', NG: 'Nigeria', NE: 'Niger', BF: 'Burkina Faso', GH: 'Ghana', CI: "Côte d'Ivoire", SN: 'Sénégal',
  ML: 'Mali', GN: 'Guinée', CM: 'Cameroun', BE: 'Belgique', CH: 'Suisse', CA: 'Canada', US: 'États-Unis', GB: 'Royaume-Uni',
};

export const countryName = (code: string) => COUNTRY_NAMES[code] ?? code;

// --- Services entre membres (mise en relation, aucun paiement dans l'application) --------

export type ProviderStatus = 'Draft' | 'PendingReview' | 'Active' | 'Suspended';
export type VerificationLevel = 'None' | 'Identity' | 'Pro';
export type LegalStatus = 'Individual' | 'MicroEntrepreneur' | 'Company';
export type ContactPreference = 'InApp' | 'Phone' | 'WhatsApp';
export type PriceType = 'Fixed' | 'From' | 'Hourly' | 'Quote';
export type ServiceRequestStatus = 'Sent' | 'Viewed' | 'Accepted' | 'Declined' | 'Completed' | 'Cancelled';
export type ServiceHiddenReason = 'None' | 'AlertKeyword' | 'Reports' | 'Admin';
export type DocumentKind = 'Identity' | 'Qualification' | 'Registration';
export type DocumentStatus = 'Pending' | 'Approved' | 'Rejected';
export type ServiceReportTarget = 'Profile' | 'Offering' | 'Review';
export type ServiceSort = 'Relevance' | 'Distance' | 'Rating' | 'Recent';

export interface ServiceCategory {
  id: string;
  slug: string;
  name: string;
  icon: string;
  parentId: string | null;
  sortOrder: number;
  isActive: boolean;
  requiresQualification: boolean;
  activeProviders: number;
  children: ServiceCategory[];
}

export interface ServiceOffering {
  id: string;
  categoryId: string;
  categoryName: string;
  requiresQualification: boolean;
  title: string;
  description: string;
  priceType: PriceType;
  priceAmount: number | null;
  currency: string;
  durationMinutes: number | null;
  images: string[];
  isActive: boolean;
  hiddenReason: ServiceHiddenReason;
  alertKeyword: string | null;
}

export interface ProviderCard {
  id: string;
  userId: string;
  displayName: string;
  photoPath: string | null;
  city: string;
  country: string;
  categoryName: string;
  categoryIcon: string;
  ratingAverage: number;
  ratingCount: number;
  priceFrom: number | null;
  currency: string;
  verificationLevel: VerificationLevel;
  servesAtHome: boolean;
  hasPremises: boolean;
  distanceKm: number | null;
  responseRate: number;
  isFavorite: boolean;
}

export interface ProviderPage {
  items: ProviderCard[];
  nextCursor: string | null;
  total: number;
}

export interface ServiceReview {
  id: string;
  authorId: string;
  authorName: string;
  rating: number;
  comment: string | null;
  createdAt: string;
  updatedAt: string | null;
  providerReply: string | null;
  repliedAt: string | null;
  verified: boolean;
  canEdit: boolean;
}

export interface ReviewPage {
  items: ServiceReview[];
  nextCursor: string | null;
  total: number;
}

export interface ProviderDetail {
  id: string;
  userId: string;
  displayName: string;
  bio: string | null;
  photoPath: string | null;
  city: string;
  country: string;
  serviceRadiusKm: number;
  servesAtHome: boolean;
  hasPremises: boolean;
  legalStatus: LegalStatus;
  verificationLevel: VerificationLevel;
  ratingAverage: number;
  ratingCount: number;
  responseRate: number;
  medianResponseMinutes: number | null;
  contactPreference: ContactPreference;
  phone: string | null;
  memberSince: string | null;
  isFavorite: boolean;
  isMine: boolean;
  hasRegulatedServices: boolean;
  offerings: ServiceOffering[];
  reviews: ReviewPage;
}

export interface ProviderDocument {
  id: string;
  kind: DocumentKind;
  originalName: string;
  contentType: string;
  size: number;
  status: DocumentStatus;
  reviewNote: string | null;
  uploadedAt: string;
  reviewedAt: string | null;
}

/** Données du profil saisies par le prestataire. */
export interface ProviderInput {
  displayName: string;
  bio: string | null;
  photoPath: string | null;
  phone: string | null;
  phoneIsPublic: boolean;
  contactPreference: ContactPreference;
  legalStatus: LegalStatus;
  siret: string | null;
  ifu: string | null;
  country: string;
  city: string;
  postalCode: string | null;
  latitude: number;
  longitude: number;
  serviceRadiusKm: number;
  servesAtHome: boolean;
  hasPremises: boolean;
}

export interface MyProvider extends ProviderInput {
  id: string;
  status: ProviderStatus;
  reviewNote: string | null;
  qualificationDeclared: boolean;
  verificationLevel: VerificationLevel;
  hiddenReason: ServiceHiddenReason;
  ratingAverage: number;
  ratingCount: number;
  responseRate: number;
  medianResponseMinutes: number | null;
  currency: string;
  offerings: ServiceOffering[];
  documents: ProviderDocument[];
}

export interface OfferingInput {
  categoryId: string;
  title: string;
  description: string | null;
  priceType: PriceType;
  priceAmount: number | null;
  durationMinutes: number | null;
  images: string[];
  isActive: boolean;
}

export interface ServiceRequestItem {
  id: string;
  status: ServiceRequestStatus;
  offeringId: string;
  offeringTitle: string;
  providerId: string;
  providerName: string;
  providerPhoto: string | null;
  clientId: string;
  clientName: string;
  message: string;
  preferredDate: string | null;
  city: string | null;
  conversationId: string | null;
  declineReason: string | null;
  createdAt: string;
  viewedAt: string | null;
  respondedAt: string | null;
  completedAt: string | null;
  providerPhone: string | null;
  isProvider: boolean;
  canReview: boolean;
  reviewId: string | null;
}

export interface ServicesInfo {
  intermediation: string;
  qualifications: string;
  reviews: string;
  taxNotice: string;
  taxLinks: { label: string; url: string }[];
}

export interface ServiceSearch {
  categoryId?: string;
  q?: string;
  lat?: number;
  lng?: number;
  radiusKm?: number;
  city?: string;
  atHome?: boolean;
  minRating?: number;
  priceMax?: number;
  verifiedOnly?: boolean;
  sort?: ServiceSort;
}

export const LEGAL_STATUS_LABELS: Record<LegalStatus, string> = {
  Individual: 'Particulier', MicroEntrepreneur: 'Micro-entrepreneur', Company: 'Société',
};

export const REQUEST_STATUS_LABELS: Record<ServiceRequestStatus, string> = {
  Sent: 'Envoyée', Viewed: 'Vue', Accepted: 'Acceptée', Declined: 'Refusée', Completed: 'Terminée', Cancelled: 'Annulée',
};

export const DOCUMENT_KIND_LABELS: Record<DocumentKind, string> = {
  Identity: "Pièce d'identité", Qualification: 'Diplôme ou qualification', Registration: 'Inscription au registre (Kbis, Sirene, IFU)',
};

/** « à partir de 60 € », « 15 €/h », « Sur devis ». */
export function servicePrice(o: { priceType: PriceType; priceAmount: number | null; currency: string }) {
  if (o.priceType === 'Quote' || o.priceAmount == null) return 'Sur devis';
  const amount = new Intl.NumberFormat('fr-FR', { style: 'currency', currency: o.currency, maximumFractionDigits: o.currency === 'XOF' ? 0 : 2 })
    .format(o.priceAmount);
  return o.priceType === 'From' ? `à partir de ${amount}` : o.priceType === 'Hourly' ? `${amount} / h` : amount;
}
