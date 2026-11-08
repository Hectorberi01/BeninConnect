import { Image } from 'expo-image';
import { useVideoPlayer, VideoView } from 'expo-video';
import { useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { imageSource, mediaUri } from '@/core/images';
import { Icon } from './ui';

/**
 * Vidéo d'une publication : miniature et bouton lecture ; le lecteur n'est créé qu'au toucher
 * (pas de téléchargement tant que le membre ne lance pas la vidéo).
 */
export function PostVideo({ videoPath, posterPath }: { videoPath: string; posterPath: string | null }) {
  const [playing, setPlaying] = useState(false);
  const uri = mediaUri(videoPath);
  if (!uri) return null;
  return (
    <View style={styles.frame}>
      {playing ? <Player uri={uri} /> : (
        <Pressable accessibilityRole="button" accessibilityLabel="Lire la vidéo" onPress={() => setPlaying(true)} style={StyleSheet.absoluteFill}>
          {posterPath && <Image source={imageSource(posterPath)} style={StyleSheet.absoluteFill} contentFit="cover" transition={150} />}
          <View style={styles.center}>
            <View style={styles.play}><Icon name="play" size={34} color="#FFFFFF" /></View>
          </View>
        </Pressable>
      )}
    </View>
  );
}

function Player({ uri }: { uri: string }) {
  const player = useVideoPlayer({ uri }, (p) => {
    p.loop = false;
    p.play();
  });
  return <VideoView player={player} style={StyleSheet.absoluteFill} contentFit="contain" nativeControls />;
}

const styles = StyleSheet.create({
  frame: { width: '100%', aspectRatio: 4 / 5, backgroundColor: '#000', marginTop: 4 },
  center: { position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, alignItems: 'center', justifyContent: 'center' },
  play: { width: 68, height: 68, borderRadius: 34, backgroundColor: 'rgba(0,0,0,0.55)', alignItems: 'center', justifyContent: 'center', paddingLeft: 4 },
});
