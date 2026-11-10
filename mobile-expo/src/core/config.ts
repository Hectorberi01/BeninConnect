import { Platform } from 'react-native';

/** API de production (Caddy + Let's Encrypt sur le VPS). */
export const PRODUCTION_API_URL = 'https://api.monbenin.com';

/** Site public (support, confidentialité, lien de partage). */
export const SITE_URL = 'https://monbenin.com';

/**
 * Adresse par défaut. En développement : EXPO_PUBLIC_API_URL dans mobile-expo/.env.local, ou Paramètres › Adresse de l'API.
 * Navigateur : EXPO_PUBLIC_WEB_API_URL si présente (ex. http://localhost:5080, car ngrok gratuit bloque le contrôle CORS).
 */
const envUrl = (Platform.OS === 'web' && process.env.EXPO_PUBLIC_WEB_API_URL) || process.env.EXPO_PUBLIC_API_URL;
export const DEFAULT_API_URL = envUrl?.replace(/\/+$/, '') || PRODUCTION_API_URL;

/** Outils de développement (source de données, adresse de l'API) : en dev, ou avec EXPO_PUBLIC_DEV_TOOLS=1. */
export const DEV_TOOLS = __DEV__ || process.env.EXPO_PUBLIC_DEV_TOOLS === '1';
