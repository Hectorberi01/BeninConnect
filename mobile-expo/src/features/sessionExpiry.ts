import { useEffect } from 'react';
import { useSession } from '@/core/session';
import { toast } from '@/core/toast';
import { onSessionExpired } from '@/data/sessionEvents';
import { signOut } from '@/features/ProfileView';

let expiring = false;

/** Session expirée : déconnexion et retour à la connexion, une seule fois même si plusieurs écrans échouent. */
export function useSessionExpiry() {
  useEffect(() => {
    onSessionExpired(() => {
      if (expiring || !useSession.getState().user) return;
      expiring = true;
      signOut()
        .then(() => toast('Votre session a expiré. Reconnectez-vous.', 'info'))
        .finally(() => { expiring = false; });
    });
    return () => onSessionExpired(null);
  }, []);
}
