// Règles de validation locales (mêmes messages que l'API et la version Avalonia).

const EMAIL = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

export const required = (value: string | null | undefined, label: string) =>
  !value || !value.trim() ? `${label} est obligatoire.` : null;

export function length(value: string | null | undefined, label: string, min: number, max: number) {
  const n = value?.trim().length ?? 0;
  if (n === 0) return `${label} est obligatoire.`;
  if (n < min) return `${label} doit contenir au moins ${min} caractères.`;
  if (n > max) return `${label} ne doit pas dépasser ${max} caractères.`;
  return null;
}

export const email = (value: string | null | undefined) =>
  !value?.trim() ? "L'adresse email est obligatoire." : EMAIL.test(value.trim()) ? null : "L'adresse email n'est pas valide.";

export function password(value: string | null | undefined) {
  if (!value) return 'Le mot de passe est obligatoire.';
  if (value.length < 8) return 'Le mot de passe doit contenir au moins 8 caractères.';
  if (!/\d/.test(value) || !/\p{L}/u.test(value)) return 'Le mot de passe doit contenir des lettres et au moins un chiffre.';
  return null;
}

/** Retourne les erreurs non nulles ; formulaire valide si l'objet est vide. */
export function collect<T extends Record<string, string | null>>(errors: T): Partial<Record<keyof T, string>> {
  return Object.fromEntries(Object.entries(errors).filter(([, v]) => v)) as Partial<Record<keyof T, string>>;
}

export const normalize = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

/** Recherche insensible à la casse et aux accents. */
export const matches = (value: string | null | undefined, query: string) => !!value && normalize(value).includes(normalize(query));
