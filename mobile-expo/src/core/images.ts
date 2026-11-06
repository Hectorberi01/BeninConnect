import { usePreferences } from './preferences';
// Images embarquées (hors ligne). Les chemins sont ceux renvoyés par l'API et le mock (« Posts/post_01.jpg »).
const IMAGES: Record<string, number> = {
  'Covers/community_01.jpg': require('../../assets/images/Covers/community_01.jpg'),
  'Covers/community_02.jpg': require('../../assets/images/Covers/community_02.jpg'),
  'Covers/community_03.jpg': require('../../assets/images/Covers/community_03.jpg'),
  'Covers/community_04.jpg': require('../../assets/images/Covers/community_04.jpg'),
  'Covers/community_05.jpg': require('../../assets/images/Covers/community_05.jpg'),
  'Covers/community_06.jpg': require('../../assets/images/Covers/community_06.jpg'),
  'Covers/community_07.jpg': require('../../assets/images/Covers/community_07.jpg'),
  'Covers/community_08.jpg': require('../../assets/images/Covers/community_08.jpg'),
  'Covers/community_09.jpg': require('../../assets/images/Covers/community_09.jpg'),
  'Covers/community_10.jpg': require('../../assets/images/Covers/community_10.jpg'),
  'Covers/community_11.jpg': require('../../assets/images/Covers/community_11.jpg'),
  'Covers/community_12.jpg': require('../../assets/images/Covers/community_12.jpg'),
  'Covers/community_13.jpg': require('../../assets/images/Covers/community_13.jpg'),
  'Covers/community_14.jpg': require('../../assets/images/Covers/community_14.jpg'),
  'Covers/community_15.jpg': require('../../assets/images/Covers/community_15.jpg'),
  'Events/event_01.jpg': require('../../assets/images/Events/event_01.jpg'),
  'Events/event_02.jpg': require('../../assets/images/Events/event_02.jpg'),
  'Events/event_03.jpg': require('../../assets/images/Events/event_03.jpg'),
  'Events/event_04.jpg': require('../../assets/images/Events/event_04.jpg'),
  'Events/event_05.jpg': require('../../assets/images/Events/event_05.jpg'),
  'Events/event_06.jpg': require('../../assets/images/Events/event_06.jpg'),
  'Events/event_07.jpg': require('../../assets/images/Events/event_07.jpg'),
  'Events/event_08.jpg': require('../../assets/images/Events/event_08.jpg'),
  'Events/event_09.jpg': require('../../assets/images/Events/event_09.jpg'),
  'Events/event_10.jpg': require('../../assets/images/Events/event_10.jpg'),
  'Onboarding/onboarding_1.png': require('../../assets/images/Onboarding/onboarding_1.png'),
  'Onboarding/onboarding_2.png': require('../../assets/images/Onboarding/onboarding_2.png'),
  'Onboarding/onboarding_3.png': require('../../assets/images/Onboarding/onboarding_3.png'),
  'Posts/post_01.jpg': require('../../assets/images/Posts/post_01.jpg'),
  'Posts/post_02.jpg': require('../../assets/images/Posts/post_02.jpg'),
  'Posts/post_03.jpg': require('../../assets/images/Posts/post_03.jpg'),
  'Posts/post_04.jpg': require('../../assets/images/Posts/post_04.jpg'),
  'Posts/post_05.jpg': require('../../assets/images/Posts/post_05.jpg'),
  'Posts/post_06.jpg': require('../../assets/images/Posts/post_06.jpg'),
  'Posts/post_07.jpg': require('../../assets/images/Posts/post_07.jpg'),
  'Posts/post_08.jpg': require('../../assets/images/Posts/post_08.jpg'),
  'Posts/post_09.jpg': require('../../assets/images/Posts/post_09.jpg'),
  'Posts/post_10.jpg': require('../../assets/images/Posts/post_10.jpg'),
  'Posts/post_11.jpg': require('../../assets/images/Posts/post_11.jpg'),
  'Posts/post_12.jpg': require('../../assets/images/Posts/post_12.jpg'),
  'Posts/post_13.jpg': require('../../assets/images/Posts/post_13.jpg'),
  'Posts/post_14.jpg': require('../../assets/images/Posts/post_14.jpg'),
  'Posts/post_15.jpg': require('../../assets/images/Posts/post_15.jpg'),
  'Posts/post_16.jpg': require('../../assets/images/Posts/post_16.jpg'),
  'Profile/cover_01.jpg': require('../../assets/images/Profile/cover_01.jpg'),
  'Profile/cover_02.jpg': require('../../assets/images/Profile/cover_02.jpg'),
  'Profile/cover_03.jpg': require('../../assets/images/Profile/cover_03.jpg'),
  'Profile/cover_04.jpg': require('../../assets/images/Profile/cover_04.jpg'),
  'Profile/cover_05.jpg': require('../../assets/images/Profile/cover_05.jpg'),
  'Profile/cover_06.jpg': require('../../assets/images/Profile/cover_06.jpg'),
};

/** Image embarquée, photo envoyée à l'API (uploads/…) ou fichier local (démo, aperçu avant envoi). */
export function imageSource(path: string | null | undefined): number | { uri: string; headers?: Record<string, string> } | undefined {
  if (!path) return undefined;
  if (IMAGES[path]) return IMAGES[path];
  if (path.startsWith('uploads/')) {
    return { uri: `${usePreferences.getState().apiUrl.replace(/\/+$/, '')}/${path}`, headers: { 'ngrok-skip-browser-warning': '1' } };
  }
  if (/^(file|https?|blob|data|ph|content):/.test(path)) return { uri: path };
  return undefined;
}

export const imagePaths = (prefix: 'Posts' | 'Covers' | 'Events' | 'Profile') =>
  Object.keys(IMAGES).filter((k) => k.startsWith(`${prefix}/`)).sort();
