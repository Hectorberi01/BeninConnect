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
}

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
}

/** Aperçu d'un message (liste des conversations). */
export function messagePreview(m: Message) {
  if (m.isDeleted) return 'Message supprimé';
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
}

export interface CreatePostRequest {
  content: string;
  communityId: string | null;
  imagePath: string | null;
  visibility: PostVisibility;
  videoPath?: string | null;
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
