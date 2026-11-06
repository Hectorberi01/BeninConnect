import { Image } from 'expo-image';
import { router } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Button, Muted, Screen, T } from '@/components/ui';
import { imageSource } from '@/core/images';
import { usePreferences } from '@/core/preferences';
import { useColors } from '@/core/theme';

const PAGES = [
  {
    title: 'Connectez-vous à votre communauté',
    description: "Retrouvez les Béninoises et Béninois de votre ville, de votre pays d'accueil et du monde entier.",
    image: 'Onboarding/onboarding_1.png',
  },
  {
    title: 'Partagez vos expériences et découvrez les événements',
    description: 'Publiez, commentez, échangez des conseils et participez aux rencontres près de chez vous.',
    image: 'Onboarding/onboarding_2.png',
  },
  {
    title: 'Développez votre réseau partout dans le monde',
    description: 'Études, emploi, entrepreneuriat, retour au pays : avancez avec le soutien de la diaspora.',
    image: 'Onboarding/onboarding_3.png',
  },
];

export default function Onboarding() {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const [index, setIndex] = useState(0);
  const page = PAGES[index];
  const last = index === PAGES.length - 1;

  const finish = () => {
    usePreferences.getState().set({ onboardingCompleted: true });
    router.replace('/login');
  };

  return (
    <Screen style={{ paddingTop: insets.top, paddingBottom: insets.bottom + 16 }}>
      <View style={styles.top}>
        {!last && <Button title="Passer" kind="ghost" small onPress={finish} />}
      </View>
      <View style={styles.body}>
        <Image source={imageSource(page.image)} style={styles.image} contentFit="contain" accessibilityLabel={page.title} />
        <T v="h1" style={styles.center}>{page.title}</T>
        <Muted style={[styles.center, { fontSize: 15, lineHeight: 22, marginTop: 12 }]}>{page.description}</Muted>
      </View>
      <View style={styles.dots} accessibilityLabel={`Étape ${index + 1} sur ${PAGES.length}`}>
        {PAGES.map((_, i) => (
          <View key={i} style={[styles.dot, { backgroundColor: i === index ? c.primary : c.border, width: i === index ? 24 : 8 }]} />
        ))}
      </View>
      <View style={styles.footer}>
        {index > 0 && <Button title="Précédent" kind="outline" onPress={() => setIndex(index - 1)} style={{ flex: 1 }} />}
        <Button title={last ? 'Commencer' : 'Suivant'} onPress={() => (last ? finish() : setIndex(index + 1))} style={{ flex: 1 }} />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  top: { height: 48, alignItems: 'flex-end', justifyContent: 'center', paddingHorizontal: 12 },
  body: { flex: 1, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 28 },
  image: { width: '100%', maxWidth: 360, aspectRatio: 1, marginBottom: 24 },
  center: { textAlign: 'center' },
  dots: { flexDirection: 'row', justifyContent: 'center', gap: 6, marginVertical: 20 },
  dot: { height: 8, borderRadius: 4 },
  footer: { flexDirection: 'row', gap: 12, paddingHorizontal: 24 },
});
