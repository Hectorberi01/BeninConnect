import { Share } from 'react-native';
import { SITE_URL } from '@/core/config';
import * as fmt from '@/core/format';
import { toast } from '@/core/toast';
import type { Community, CommunityEvent } from '@/data/types';
import { location } from '@/data/types';

// Liens publics (monbenin.com) : aperçu pour les non-inscrits, ouverture directe dans l'app pour les membres
async function share(message: string) {
  try {
    await Share.share({ message });
  } catch {
    toast('Partage impossible.', 'error');
  }
}

export const shareEvent = (event: CommunityEvent) => share(
  `« ${event.title} » sur BéninConnect\n📅 ${fmt.eventSchedule(event.startsAt, event.endsAt)}\n📍 ${location(event)}\n\n${SITE_URL}/events/${event.id}`);

export const shareCommunity = (community: Community) => {
  const n = community.memberIds.length;
  return share(`Rejoins la communauté « ${community.name} » sur BéninConnect (${n} membre${n > 1 ? 's' : ''}).\n\n${SITE_URL}/community/${community.id}`);
};
