/** Villes proposées (position approximative du centre-ville) : recherche « près de » et zone des prestataires. */
export interface City {
  name: string;
  country: string;
  lat: number;
  lng: number;
}

export const CITIES: City[] = [
  // France
  { name: 'Paris', country: 'FR', lat: 48.8566, lng: 2.3522 },
  { name: 'Marseille', country: 'FR', lat: 43.2965, lng: 5.3698 },
  { name: 'Lyon', country: 'FR', lat: 45.764, lng: 4.8357 },
  { name: 'Toulouse', country: 'FR', lat: 43.6047, lng: 1.4442 },
  { name: 'Nice', country: 'FR', lat: 43.7102, lng: 7.262 },
  { name: 'Nantes', country: 'FR', lat: 47.2184, lng: -1.5536 },
  { name: 'Montpellier', country: 'FR', lat: 43.6108, lng: 3.8767 },
  { name: 'Strasbourg', country: 'FR', lat: 48.5734, lng: 7.7521 },
  { name: 'Bordeaux', country: 'FR', lat: 44.8378, lng: -0.5792 },
  { name: 'Lille', country: 'FR', lat: 50.6292, lng: 3.0573 },
  { name: 'Rennes', country: 'FR', lat: 48.1173, lng: -1.6778 },
  { name: 'Reims', country: 'FR', lat: 49.2583, lng: 4.0317 },
  { name: 'Le Havre', country: 'FR', lat: 49.4944, lng: 0.1079 },
  { name: 'Rouen', country: 'FR', lat: 49.4432, lng: 1.0999 },
  { name: 'Saint-Étienne', country: 'FR', lat: 45.4397, lng: 4.3872 },
  { name: 'Toulon', country: 'FR', lat: 43.1242, lng: 5.928 },
  { name: 'Grenoble', country: 'FR', lat: 45.1885, lng: 5.7245 },
  { name: 'Dijon', country: 'FR', lat: 47.322, lng: 5.0415 },
  { name: 'Angers', country: 'FR', lat: 47.4784, lng: -0.5632 },
  { name: 'Nîmes', country: 'FR', lat: 43.8367, lng: 4.3601 },
  { name: 'Clermont-Ferrand', country: 'FR', lat: 45.7772, lng: 3.087 },
  { name: 'Le Mans', country: 'FR', lat: 48.0061, lng: 0.1996 },
  { name: 'Aix-en-Provence', country: 'FR', lat: 43.5297, lng: 5.4474 },
  { name: 'Brest', country: 'FR', lat: 48.3904, lng: -4.4861 },
  { name: 'Tours', country: 'FR', lat: 47.3941, lng: 0.6848 },
  { name: 'Amiens', country: 'FR', lat: 49.8941, lng: 2.2958 },
  { name: 'Limoges', country: 'FR', lat: 45.8336, lng: 1.2611 },
  { name: 'Metz', country: 'FR', lat: 49.1193, lng: 6.1757 },
  { name: 'Orléans', country: 'FR', lat: 47.9029, lng: 1.9093 },
  { name: 'Caen', country: 'FR', lat: 49.1829, lng: -0.3707 },
  { name: 'Nancy', country: 'FR', lat: 48.6921, lng: 6.1844 },
  { name: 'Mulhouse', country: 'FR', lat: 47.7508, lng: 7.3359 },
  { name: 'Saint-Denis', country: 'FR', lat: 48.9362, lng: 2.3574 },
  { name: 'Montreuil', country: 'FR', lat: 48.8638, lng: 2.4485 },
  { name: 'Créteil', country: 'FR', lat: 48.7904, lng: 2.4556 },
  { name: 'Nanterre', country: 'FR', lat: 48.8924, lng: 2.2071 },
  { name: 'Argenteuil', country: 'FR', lat: 48.9472, lng: 2.2467 },
  { name: 'Versailles', country: 'FR', lat: 48.8049, lng: 2.1204 },
  { name: 'Cergy', country: 'FR', lat: 49.0364, lng: 2.0761 },
  { name: 'Évry-Courcouronnes', country: 'FR', lat: 48.6295, lng: 2.4411 },
  { name: 'Boulogne-Billancourt', country: 'FR', lat: 48.8397, lng: 2.2399 },
  { name: 'Vitry-sur-Seine', country: 'FR', lat: 48.7875, lng: 2.3928 },
  { name: 'Aubervilliers', country: 'FR', lat: 48.9146, lng: 2.3821 },
  // Bénin
  { name: 'Cotonou', country: 'BJ', lat: 6.3703, lng: 2.3912 },
  { name: 'Abomey-Calavi', country: 'BJ', lat: 6.4485, lng: 2.3557 },
  { name: 'Porto-Novo', country: 'BJ', lat: 6.4969, lng: 2.6289 },
  { name: 'Parakou', country: 'BJ', lat: 9.3372, lng: 2.6303 },
  { name: 'Djougou', country: 'BJ', lat: 9.7085, lng: 1.6660 },
  { name: 'Bohicon', country: 'BJ', lat: 7.1782, lng: 2.0667 },
  { name: 'Abomey', country: 'BJ', lat: 7.1829, lng: 1.9912 },
  { name: 'Ouidah', country: 'BJ', lat: 6.3631, lng: 2.0851 },
  { name: 'Lokossa', country: 'BJ', lat: 6.6386, lng: 1.7167 },
  { name: 'Natitingou', country: 'BJ', lat: 10.3042, lng: 1.3796 },
  { name: 'Kandi', country: 'BJ', lat: 11.1342, lng: 2.9386 },
  { name: 'Grand-Popo', country: 'BJ', lat: 6.2833, lng: 1.8333 },
  // Afrique de l'Ouest et ailleurs
  { name: 'Lomé', country: 'TG', lat: 6.1319, lng: 1.2228 },
  { name: 'Lagos', country: 'NG', lat: 6.5244, lng: 3.3792 },
  { name: 'Abidjan', country: 'CI', lat: 5.36, lng: -4.0083 },
  { name: 'Dakar', country: 'SN', lat: 14.7167, lng: -17.4677 },
  { name: 'Ouagadougou', country: 'BF', lat: 12.3714, lng: -1.5197 },
  { name: 'Niamey', country: 'NE', lat: 13.5116, lng: 2.1254 },
  { name: 'Accra', country: 'GH', lat: 5.6037, lng: -0.187 },
  { name: 'Bamako', country: 'ML', lat: 12.6392, lng: -8.0029 },
  { name: 'Bruxelles', country: 'BE', lat: 50.8503, lng: 4.3517 },
  { name: 'Genève', country: 'CH', lat: 46.2044, lng: 6.1432 },
  { name: 'Montréal', country: 'CA', lat: 45.5019, lng: -73.5674 },
];

/** Pays reconnus : nom affiché et ville principale (position pour une ville hors liste). */
const COUNTRY_INFO: Record<string, { name: string; lat: number; lng: number; aliases?: string[] }> = {
  FR: { name: 'France', lat: 48.8566, lng: 2.3522 },
  BJ: { name: 'Bénin', lat: 6.3703, lng: 2.3912 },
  TG: { name: 'Togo', lat: 6.1319, lng: 1.2228 },
  NG: { name: 'Nigeria', lat: 6.5244, lng: 3.3792, aliases: ['Nigéria'] },
  CI: { name: "Côte d'Ivoire", lat: 5.36, lng: -4.0083 },
  SN: { name: 'Sénégal', lat: 14.7167, lng: -17.4677 },
  BF: { name: 'Burkina Faso', lat: 12.3714, lng: -1.5197 },
  NE: { name: 'Niger', lat: 13.5116, lng: 2.1254 },
  GH: { name: 'Ghana', lat: 5.6037, lng: -0.187 },
  ML: { name: 'Mali', lat: 12.6392, lng: -8.0029 },
  GN: { name: 'Guinée', lat: 9.6412, lng: -13.5784 },
  CM: { name: 'Cameroun', lat: 4.0511, lng: 9.7679 },
  GA: { name: 'Gabon', lat: 0.4162, lng: 9.4673 },
  CG: { name: 'Congo', lat: -4.2634, lng: 15.2429, aliases: ['Congo-Brazzaville'] },
  CD: { name: 'RD Congo', lat: -4.4419, lng: 15.2663, aliases: ['RDC', 'République démocratique du Congo', 'Congo-Kinshasa'] },
  TD: { name: 'Tchad', lat: 12.1348, lng: 15.0557 },
  MR: { name: 'Mauritanie', lat: 18.0735, lng: -15.9582 },
  MA: { name: 'Maroc', lat: 33.5731, lng: -7.5898 },
  TN: { name: 'Tunisie', lat: 36.8065, lng: 10.1815 },
  DZ: { name: 'Algérie', lat: 36.7538, lng: 3.0588 },
  BE: { name: 'Belgique', lat: 50.8503, lng: 4.3517 },
  CH: { name: 'Suisse', lat: 46.948, lng: 7.4474 },
  LU: { name: 'Luxembourg', lat: 49.6116, lng: 6.1319 },
  DE: { name: 'Allemagne', lat: 52.52, lng: 13.405 },
  ES: { name: 'Espagne', lat: 40.4168, lng: -3.7038 },
  IT: { name: 'Italie', lat: 41.9028, lng: 12.4964 },
  PT: { name: 'Portugal', lat: 38.7223, lng: -9.1393 },
  NL: { name: 'Pays-Bas', lat: 52.3676, lng: 4.9041, aliases: ['Hollande'] },
  GB: { name: 'Royaume-Uni', lat: 51.5074, lng: -0.1278, aliases: ['Angleterre', 'UK'] },
  CA: { name: 'Canada', lat: 45.5019, lng: -73.5674 },
  US: { name: 'États-Unis', lat: 40.7128, lng: -74.006, aliases: ['USA', 'Amérique'] },
  AE: { name: 'Émirats arabes unis', lat: 25.2048, lng: 55.2708, aliases: ['Dubaï', 'EAU'] },
};

const COUNTRIES: Record<string, string> = Object.fromEntries(Object.entries(COUNTRY_INFO).map(([code, c]) => [code, c.name]));

/** Comparaison souple : minuscules, sans accents, tirets ni apostrophes (« porto novo » = « Porto-Novo »). */
export const normalizePlace = (text: string) =>
  text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[-'’.]/g, ' ').replace(/\s+/g, ' ').trim();

/** Première lettre en majuscule, le reste tel que saisi. */
export const capitalizeFirst = (text: string) => text.replace(/^(\s*)(\S)/, (_, space: string, first: string) => space + first.toLocaleUpperCase('fr-FR'));

/** Code du pays saisi (nom, variante ou code à 2 lettres), null s'il n'est pas reconnu. */
export function countryCode(text: string | null | undefined): string | null {
  const key = normalizePlace(text ?? '');
  if (!key) return null;
  for (const [code, c] of Object.entries(COUNTRY_INFO)) {
    if (key === code.toLowerCase() || [c.name, ...(c.aliases ?? [])].some((n) => normalizePlace(n) === key)) return code;
  }
  return null;
}

/** Ville connue correspondant à la saisie (le pays la départage si fourni). */
export const matchCity = (name: string | null | undefined, country?: string | null) => {
  const key = normalizePlace(name ?? '');
  return key ? CITIES.find((c) => normalizePlace(c.name) === key && (!country || c.country === country)) : undefined;
};

/** Position d'une ville saisie : la ville si elle est connue, sinon la ville principale du pays. */
export function placeCoords(city: string, country: string | null): { lat: number; lng: number } | null {
  const known = matchCity(city, country);
  if (known) return { lat: known.lat, lng: known.lng };
  const info = country ? COUNTRY_INFO[country] : undefined;
  return info ? { lat: info.lat, lng: info.lng } : null;
}

/** Suggestions pendant la saisie. */
export function suggestCities(text: string, country?: string | null, limit = 4) {
  const key = normalizePlace(text);
  if (!key) return [];
  return CITIES.filter((c) => (!country || c.country === country) && normalizePlace(c.name).startsWith(key) && normalizePlace(c.name) !== key).slice(0, limit);
}

export function suggestCountries(text: string, limit = 4) {
  const key = normalizePlace(text);
  if (!key) return [];
  return Object.values(COUNTRY_INFO).map((c) => c.name).filter((n) => normalizePlace(n).startsWith(key) && normalizePlace(n) !== key).slice(0, limit);
}

export const cityLabel = (c: Pick<City, 'name' | 'country'>) => `${c.name} (${COUNTRIES[c.country] ?? c.country})`;

export const findCity = matchCity;

export const countryName = (code: string) => COUNTRIES[code] ?? code;

/** Pays de la liste, dans l'ordre d'apparition (France et Bénin en tête). */
export const COUNTRY_CODES = [...new Set(CITIES.map((c) => c.country))];
