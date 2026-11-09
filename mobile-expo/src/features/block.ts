import { useQuery } from '@tanstack/react-query';
import { confirm } from '@/core/dialog';
import { queryClient } from '@/core/queryClient';
import { toast } from '@/core/toast';
import { data } from '@/data';
import { displayName, errorMessage, type User } from '@/data/types';

type Member = Pick<User, 'id' | 'firstName' | 'lastName'>;

/** Caches dont le contenu change après un blocage (publications, commentaires, discussions). */
const AFFECTED = ['blockedUsers', 'feed', 'posts', 'post', 'comments', 'conversations', 'conversation', 'notifications'];

function refresh() {
  for (const key of AFFECTED) queryClient.invalidateQueries({ queryKey: [key] }).catch(() => undefined);
}

/** Membres que j'ai bloqués. */
export const useBlockedUsers = () => useQuery({ queryKey: ['blockedUsers'], queryFn: () => data().getBlockedUsers() });

/** Vrai si j'ai bloqué ce membre. */
export function useIsBlocked(userId: string | undefined) {
  const blocked = useBlockedUsers();
  return !!userId && !!blocked.data?.some((u) => u.id === userId);
}

/** Confirmation puis blocage ; onDone appelé après succès (ex. quitter l'écran). */
export async function blockUser(user: Member, onDone?: () => void) {
  const name = displayName(user);
  const ok = await confirm(`Bloquer ${name} ?`,
    'Vous ne verrez plus ses publications ni ses commentaires, et vous ne pourrez plus échanger de messages. '
    + 'Cette personne ne sera pas prévenue. Vous pourrez la débloquer dans Paramètres › Utilisateurs bloqués.',
    'Bloquer', true);
  if (!ok) return;
  try {
    await data().blockUser(user.id);
    refresh();
    toast(`${name} est bloqué(e).`, 'success');
    onDone?.();
  } catch (e) {
    toast(errorMessage(e), 'error');
  }
}

export async function unblockUser(user: Member) {
  try {
    await data().unblockUser(user.id);
    refresh();
    toast(`${displayName(user)} est débloqué(e).`, 'success');
  } catch (e) {
    toast(errorMessage(e), 'error');
  }
}
