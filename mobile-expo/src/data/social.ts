import type { User } from './types';
import { displayName } from './types';

// Hashtags et mentions : mêmes règles que le serveur (Feed/Domain/Post.cs, SocialText).

const TAG = /(?<![\p{L}\p{N}_])#([\p{L}\p{N}_]{2,40})/gu;

/** Hashtag comparable : sans #, en minuscules. */
export const normalizeTag = (tag: string) => tag.trim().replace(/^#+/, '').toLowerCase();

export const extractTags = (text: string) => [...new Set([...text.matchAll(TAG)].map((m) => normalizeTag(m[1])))];

export type TextPart =
  | { kind: 'text'; text: string }
  | { kind: 'tag'; text: string; tag: string }
  | { kind: 'mention'; text: string; userId: string };

/** Découpe un texte en morceaux : texte, #hashtag, @Prénom Nom (membres connus). */
export function parseSocialText(text: string, mentioned: User[] = []): TextPart[] {
  const names = mentioned
    .map((u) => ({ id: u.id, label: `@${displayName(u)}` }))
    .sort((a, b) => b.label.length - a.label.length);
  const parts: TextPart[] = [];
  let buffer = '';
  const flush = () => {
    if (buffer) parts.push({ kind: 'text', text: buffer });
    buffer = '';
  };

  for (let i = 0; i < text.length;) {
    const c = text[i];
    const prev = i > 0 ? text[i - 1] : ' ';
    const boundary = !/[\p{L}\p{N}_]/u.test(prev);
    if (c === '@' && boundary) {
      const hit = names.find((n) => text.startsWith(n.label, i));
      if (hit) {
        flush();
        parts.push({ kind: 'mention', text: hit.label, userId: hit.id });
        i += hit.label.length;
        continue;
      }
    }
    if (c === '#' && boundary) {
      const m = /^#([\p{L}\p{N}_]{2,40})/u.exec(text.slice(i));
      if (m) {
        flush();
        parts.push({ kind: 'tag', text: m[0], tag: normalizeTag(m[1]) });
        i += m[0].length;
        continue;
      }
    }
    buffer += c;
    i++;
  }
  flush();
  return parts;
}

/** Mention en cours de saisie : « @Ko » juste avant le curseur (null sinon). */
export function mentionQuery(text: string, cursor: number) {
  const before = text.slice(0, cursor);
  const m = /(?:^|[^\p{L}\p{N}_])@([\p{L}\p{N}_-]{0,30})$/u.exec(before);
  return m ? { query: m[1], start: cursor - m[1].length - 1 } : null;
}

/** Remplace la mention en cours par « @Prénom Nom ». */
export function insertMention(text: string, cursor: number, user: User) {
  const q = mentionQuery(text, cursor);
  if (!q) return { text, cursor };
  const label = `@${displayName(user)} `;
  const next = text.slice(0, q.start) + label + text.slice(cursor);
  return { text: next, cursor: q.start + label.length };
}

/** Garde les membres choisis dont la mention figure encore dans le texte. */
export const keptMentions = (text: string, chosen: User[]) =>
  [...new Set(chosen.filter((u) => text.includes(`@${displayName(u)}`)).map((u) => u.id))];
