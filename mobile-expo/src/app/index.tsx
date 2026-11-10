import Constants from 'expo-constants';
import { router } from 'expo-router';
import { useEffect } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { usePreferences } from '@/core/preferences';
import { useSession } from '@/core/session';
import { data } from '@/data';
import { benin, font } from '@/core/theme';

/** Écran de démarrage : onboarding, session mémorisée ou connexion. */
export default function Splash() {
  useEffect(() => {
    let cancelled = false;
    (async () => {
      await new Promise((r) => setTimeout(r, 900));
      const prefs = usePreferences.getState();
      if (!prefs.onboardingCompleted) {
        if (!cancelled) router.replace('/onboarding');
        return;
      }
      const user = prefs.rememberMe ? await useSession.getState().restore() : null;
      if (!cancelled) router.replace(user ? '/home' : '/login');
    })();
    return () => { cancelled = true; };
  }, []);

  return (
    <View style={styles.root}>
      <View style={styles.logo}><Text style={styles.logoText}>B</Text></View>
      <Text style={styles.title}>BéninConnect</Text>
      <Text style={styles.subtitle}>La communauté béninoise, partout dans le monde</Text>
      <View style={styles.flag}>
        <View style={[styles.band, { backgroundColor: benin.green }]} />
        <View style={[styles.band, { backgroundColor: benin.yellow }]} />
        <View style={[styles.band, { backgroundColor: benin.red }]} />
      </View>
      <Text style={styles.version}>{`Version ${Constants.expoConfig?.version ?? '1.0'}${data().kind === 'api' ? '' : ' · démonstration'}`}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#087F5B', alignItems: 'center', justifyContent: 'center', padding: 24 },
  logo: { width: 88, height: 88, borderRadius: 24, backgroundColor: '#FFFFFF', alignItems: 'center', justifyContent: 'center', marginBottom: 20 },
  logoText: { fontFamily: font.bold, fontSize: 44, color: '#087F5B' },
  title: { fontFamily: font.bold, fontSize: 30, color: '#FFFFFF' },
  subtitle: { fontFamily: font.regular, fontSize: 15, color: '#D1FAE5', marginTop: 8, textAlign: 'center' },
  flag: { flexDirection: 'row', width: 72, height: 6, borderRadius: 3, overflow: 'hidden', marginTop: 24 },
  band: { flex: 1 },
  version: { position: 'absolute', bottom: 48, fontFamily: font.regular, fontSize: 13, color: '#A7F3D0' },
});
