/** API de production (Caddy + Let's Encrypt sur le VPS). */
export const PRODUCTION_API_URL = 'https://api.monbenin.com';

/** Site public (support, confidentialité, lien de partage). */
export const SITE_URL = 'https://monbenin.com';

/** Adresse par défaut. En développement : EXPO_PUBLIC_API_URL dans mobile-expo/.env.local, ou Paramètres › Adresse de l'API. */
export const DEFAULT_API_URL = process.env.EXPO_PUBLIC_API_URL?.replace(/\/+$/, '') || PRODUCTION_API_URL;

/** Outils de développement (source de données, adresse de l'API) : en dev, ou avec EXPO_PUBLIC_DEV_TOOLS=1. */
export const DEV_TOOLS = __DEV__ || process.env.EXPO_PUBLIC_DEV_TOOLS === '1';
