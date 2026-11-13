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

const COUNTRIES: Record<string, string> = {
  FR: 'France', BJ: 'Bénin', TG: 'Togo', NG: 'Nigeria', CI: "Côte d'Ivoire", SN: 'Sénégal', BF: 'Burkina Faso', NE: 'Niger', GH: 'Ghana',
  ML: 'Mali', BE: 'Belgique', CH: 'Suisse', CA: 'Canada',
};

export const cityLabel = (c: Pick<City, 'name' | 'country'>) => `${c.name} (${COUNTRIES[c.country] ?? c.country})`;

export const findCity = (name: string | null | undefined, country?: string | null) =>
  name ? CITIES.find((c) => c.name.toLowerCase() === name.trim().toLowerCase() && (!country || c.country === country)) : undefined;
