import { countryName } from './cities';

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.'];
const WEEKDAYS = ['dim.', 'lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.'];

/** « 2026-12-12 » → « sam. 12 déc. » (date locale, sans décalage de fuseau). */
export function tripDay(day: string, withYear = false) {
  const [y, m, d] = day.split('-').map(Number);
  const date = new Date(y!, m! - 1, d!);
  return `${WEEKDAYS[date.getDay()]} ${d} ${MONTHS[m! - 1]}${withYear ? ` ${y}` : ''}`;
}

/** Date du jour + n jours au format AAAA-MM-JJ. */
export function isoDay(offset = 0) {
  const d = new Date(Date.now() + offset * 86_400_000);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** « 12/12/2026 » → « 2026-12-12 » ; null si vide, undefined si invalide. */
export function parseDay(text: string): string | null | undefined {
  const t = text.trim();
  if (!t) return null;
  const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(t);
  if (!m) return undefined;
  const d = new Date(Number(m[3]), Number(m[2]) - 1, Number(m[1]));
  if (d.getMonth() !== Number(m[2]) - 1) return undefined;
  return `${m[3]}-${m[2]!.padStart(2, '0')}-${m[1]!.padStart(2, '0')}`;
}

export const formatDay = (day: string | null | undefined) => (day ? day.split('-').reverse().join('/') : '');

/** Lieu de recherche : un pays entier ou une ville. */
export interface Place {
  country: string;
  city: string | null;
}

export const placeLabel = (p: Place) => (p.city ? `${p.city} (${countryName(p.country)})` : `${countryName(p.country)} : toutes les villes`);

