import { useLocalSearchParams } from 'expo-router';
import { Screen } from '@/components/ui';
import { ProfileView } from '@/features/ProfileView';

export default function UserProfile() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <Screen><ProfileView userId={id} showBack /></Screen>;
}
