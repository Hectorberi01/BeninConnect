import { router, useLocalSearchParams } from 'expo-router';
import { Screen } from '@/components/ui';
import { TripForm } from '@/components/TripForm';
import { VoyagesGate } from '@/components/VoyagesGate';

/** Publier ou modifier un trajet (modification, liens depuis l'accueil). */
export default function PublishTripScreen() {
  const { id } = useLocalSearchParams<{ id?: string }>();
  return (
    <VoyagesGate>
      <Screen><TripForm id={id} onSaved={(tripId) => router.replace(`/voyages/trip/${tripId}`)} /></Screen>
    </VoyagesGate>
  );
}
