import { Redirect } from 'expo-router';

// Onglet factice : l'appui ouvre /post/new (voir le layout des onglets).
export default function Publish() {
  return <Redirect href="/post/new" />;
}
