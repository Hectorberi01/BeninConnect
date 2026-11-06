import * as v from '@/core/validation';
import type { Href } from 'expo-router';
import type { AppNotification, CommunityRole, PostVisibility } from '@/data/types';

// Règles d'interface partagées par plusieurs écrans (testées unitairement).

export const MAX_POST_LENGTH = 2000;
export const MAX_INTERESTS = 6;
export const MAX_BIO = 300;

export function validatePost(content: string, visibility: PostVisibility, communityId: string | null) {
  const n = content.trim().length;
  if (n === 0) return 'Écrivez quelque chose avant de publier.';
  if (n < 3) return 'La publication doit contenir au moins 3 caractères.';
  if (n > MAX_POST_LENGTH) return `La publication ne doit pas dépasser ${MAX_POST_LENGTH} caractères.`;
  if (visibility === 'CommunityMembers' && !communityId) return 'Choisissez une communauté pour une publication réservée aux membres.';
  return null;
}

export const roleLabel = (r: CommunityRole) => (r === 'Owner' ? 'Créateur' : r === 'Moderator' ? 'Modérateur' : null);

const pad = (n: number) => String(n).padStart(2, '0');
export const formatDateInput = (d: Date) => `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;

/** « JJ/MM/AAAA » + « HH:MM » en date locale (null si invalide). */
export function parseLocalDateTime(date: string, time: string): Date | null {
  const dm = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(date.trim());
  const tm = /^(\d{1,2})[:h](\d{2})$/.exec(time.trim());
  if (!dm || !tm) return null;
  const [day, month, year, hours, minutes] = [+dm[1], +dm[2], +dm[3], +tm[1], +tm[2]];
  const d = new Date(year, month - 1, day, hours, minutes);
  const valid = d.getFullYear() === year && d.getMonth() === month - 1 && d.getDate() === day && hours < 24 && minutes < 60;
  return valid ? d : null;
}

export interface EventForm {
  title: string; description: string; category: string | null; city: string; address: string;
  date: string; time: string; duration: string; capacity: string;
}

export function validateEvent(f: EventForm, now = new Date()) {
  const start = parseLocalDateTime(f.date, f.time);
  const hours = /^\d+$/.test(f.duration.trim()) ? +f.duration : NaN;
  const capacity = f.capacity.trim();
  return v.collect({
    title: v.length(f.title, 'Le titre', 5, 80),
    description: v.length(f.description, 'La description', 20, 1000),
    category: f.category ? null : 'Choisissez une catégorie.',
    city: v.required(f.city, 'La ville'),
    address: v.required(f.address, "L'adresse"),
    date: !start ? 'Choisissez une date et une heure (JJ/MM/AAAA, HH:MM).' : start <= now ? "L'événement doit avoir lieu dans le futur." : null,
    duration: hours >= 1 && hours <= 72 ? null : 'Durée entre 1 et 72 heures.',
    capacity: !capacity || (/^\d+$/.test(capacity) && +capacity >= 2 && +capacity <= 10000)
      ? null : 'Capacité entre 2 et 10 000 (laisser vide pour illimité).',
  });
}

export const DEMO_REPLIES = [
  'Merci pour ton message ! (réponse de démonstration)',
  'Bien reçu, je te redis ça rapidement. (réponse de démonstration)',
  'Super idée, on en parle ce week-end ? (réponse de démonstration)',
  'Avec plaisir ! (réponse de démonstration)',
];

/** Écran ouvert par une notification (null : simple message). */
export function notificationHref(n: AppNotification): Href | null {
  if (!n.targetId) return null;
  switch (n.target) {
    case 'Post': return `/post/${n.targetId}`;
    case 'Community': return `/community/${n.targetId}`;
    case 'Conversation': return `/conversation/${n.targetId}`;
    case 'Event': return `/events/${n.targetId}`;
    case 'Profile': return `/user/${n.targetId}`;
    default: return null;
  }
}

export const SEARCH_SUGGESTIONS = ['Paris', 'Montréal', 'Étudiants', 'Emploi', 'Cuisine', 'Tech', 'Concert'];
