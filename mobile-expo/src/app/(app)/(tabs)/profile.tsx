import { Screen } from '@/components/ui';
import { useMe } from '@/core/session';
import { ProfileView } from '@/features/ProfileView';

export default function MyProfile() {
  const me = useMe();
  return <Screen><ProfileView userId={me.id} /></Screen>;
}
