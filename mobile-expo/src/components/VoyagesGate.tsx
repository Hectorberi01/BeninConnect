import type { ReactNode } from 'react';
import { EmptyState, Header, Loading, Screen } from '@/components/ui';
import { useVoyagesEnabled } from '@/features/queries';

/** Écrans Voyages : affichés seulement quand l'équipe a activé le module. */
export function VoyagesGate({ children }: { children: ReactNode }) {
  const { enabled, pending } = useVoyagesEnabled();
  if (enabled) return <>{children}</>;
  return (
    <Screen>
      <Header title="Voyages" />
      {pending ? <Loading /> : <EmptyState icon="airplane-clock" title="Bientôt disponible" message="Ce service n’est pas encore ouvert. Revenez prochainement." />}
    </Screen>
  );
}
