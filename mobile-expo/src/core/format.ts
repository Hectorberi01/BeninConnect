// Formats de date en français, indépendants de la locale de l'appareil.

const SHORT_MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.'];
const LONG_MONTHS = ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre'];
const DAYS = ['dimanche', 'lundi', 'mardi', 'mercredi', 'jeudi', 'vendredi', 'samedi'];

const pad = (n: number) => String(n).padStart(2, '0');
export const capitalize = (s: string) => (s ? s[0].toUpperCase() + s.slice(1) : s);
const sameDay = (a: Date, b: Date) => a.toDateString() === b.toDateString();

export function relative(isoDate: string, now = new Date()) {
  const d = new Date(isoDate);
  const minutes = (now.getTime() - d.getTime()) / 60_000;
  if (minutes < 1) return "À l'instant";
  if (minutes < 60) return `Il y a ${Math.floor(minutes)} min`;
  if (minutes < 1440) return `Il y a ${Math.floor(minutes / 60)} h`;
  if (minutes < 2880) return 'Hier';
  if (minutes < 10080) return `Il y a ${Math.floor(minutes / 1440)} j`;
  return shortDate(isoDate, now);
}

export function shortDate(isoDate: string, now = new Date()) {
  const d = new Date(isoDate);
  const base = `${d.getDate()} ${SHORT_MONTHS[d.getMonth()]}`;
  return d.getFullYear() === now.getFullYear() ? base : `${base} ${d.getFullYear()}`;
}

export const clock = (isoDate: string) => {
  const d = new Date(isoDate);
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

export const longDate = (isoDate: string) => {
  const d = new Date(isoDate);
  return `${capitalize(DAYS[d.getDay()])} ${d.getDate()} ${LONG_MONTHS[d.getMonth()]} ${d.getFullYear()}`;
};

export const monthShort = (isoDate: string) => SHORT_MONTHS[new Date(isoDate).getMonth()].replace('.', '').toUpperCase();
export const monthYear = (isoDate: string) => {
  const d = new Date(isoDate);
  return `${LONG_MONTHS[d.getMonth()]} ${d.getFullYear()}`;
};

export function chat(isoDate: string, now = new Date()) {
  const d = new Date(isoDate);
  if (sameDay(d, now)) return clock(isoDate);
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (sameDay(d, yesterday)) return 'Hier';
  if (now.getTime() - d.getTime() < 7 * 86_400_000) return capitalize(DAYS[d.getDay()]);
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${String(d.getFullYear()).slice(2)}`;
}

export function dayLabel(isoDate: string, now = new Date()) {
  const d = new Date(isoDate);
  if (sameDay(d, now)) return "Aujourd'hui";
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  return sameDay(d, yesterday) ? 'Hier' : longDate(isoDate);
}

export const eventSchedule = (start: string, end: string) =>
  sameDay(new Date(start), new Date(end))
    ? `${longDate(start)} · ${clock(start)} – ${clock(end)}`
    : `Du ${new Date(start).getDate()} ${LONG_MONTHS[new Date(start).getMonth()]} au ${new Date(end).getDate()} ${LONG_MONTHS[new Date(end).getMonth()]} · ${clock(start)}`;

export const plural = (n: number, singular: string, pluralForm = `${singular}s`) => `${n} ${n > 1 ? pluralForm : singular}`;

/** Taille de fichier lisible : 820 Ko, 3,4 Mo. */
export function fileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} o`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} Ko`;
  return `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} Mo`;
}

/** Durée m:ss (messages vocaux). */
export function duration(ms: number) {
  const total = Math.max(0, Math.round(ms / 1000));
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
}
