import { Image } from 'expo-image';
import { useLocalSearchParams } from 'expo-router';
import qrcode from 'qrcode-generator';
import { useMemo } from 'react';
import { Share, StyleSheet, View } from 'react-native';
import { Avatar, Button, Header, Muted, QueryView, Screen, T } from '@/components/ui';
import { SITE_URL } from '@/core/config';
import { depth, radius, useColors } from '@/core/theme';
import { displayName, initials, location } from '@/data/types';
import { useUser } from '@/features/queries';

const SIZE = 260;

/** Code QR du profil : https://monbenin.com/user/<id> (ouvre l'application, ou la page web du profil). */
export default function ProfileQr() {
  const c = useColors();
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useUser(id);
  const url = `${SITE_URL}/user/${id}`;
  // Image GIF générée sur place ; cellules de 16 px puis réduite à l'affichage : bords nets
  const qr = useMemo(() => {
    const code = qrcode(0, 'M');
    code.addData(url);
    code.make();
    return code.createDataURL(16, 0);
  }, [url]);

  return (
    <Screen>
      <Header title="Code QR du profil" />
      <QueryView query={query}>
        {(user) => (
          <View style={styles.body}>
            <View style={[styles.card, { backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 3)]}>
              <Avatar initials={initials(user)} color={user.avatarColor} image={user.avatarImage} size={64} />
              <T v="h3" style={{ marginTop: 10 }}>{displayName(user)}</T>
              {!!location(user) && <Muted>{location(user)}</Muted>}
              <View style={styles.qr}>
                <Image source={{ uri: qr }} style={{ width: SIZE, height: SIZE }} contentFit="contain" accessibilityLabel={`Code QR du profil de ${displayName(user)}`} />
              </View>
              <Muted style={{ textAlign: 'center' }}>Scannez ce code avec l'appareil photo pour ouvrir le profil sur BéninConnect.</Muted>
            </View>
            <Button title="Partager le lien du profil" icon="share-variant-outline" style={{ alignSelf: 'stretch' }}
              onPress={() => Share.share({ message: `${displayName(user)} sur BéninConnect : ${url}` })} />
          </View>
        )}
      </QueryView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  body: { flex: 1, padding: 24, alignItems: 'center', gap: 20 },
  card: { alignItems: 'center', padding: 24, borderRadius: radius.card, borderWidth: 1, alignSelf: 'stretch', gap: 2 },
  qr: { backgroundColor: '#FFFFFF', padding: 14, borderRadius: 16, marginVertical: 16 },
});
