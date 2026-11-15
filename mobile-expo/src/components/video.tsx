import { Image } from 'expo-image';
import { useFocusEffect } from 'expo-router';
import { useVideoPlayer, type VideoPlayer, VideoView } from 'expo-video';
import { type ComponentRef, useCallback, useEffect, useRef, useState } from 'react';
import { Pressable, StyleSheet, useWindowDimensions, View } from 'react-native';
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

/** Vidéo en cours de lecture : une seule à la fois dans toute l'application. */
let current: VideoPlayer | null = null;

// Le lecteur peut être déjà libéré (publication démontée) : pause sans erreur
const pause = (player: VideoPlayer) => {
  try {
    player.pause();
  } catch {
    // lecteur libéré
  }
};

/**
 * Lecteur : se met en pause quand il sort de l'écran (moins de 40 % visible), quand on change d'écran
 * ou quand une autre vidéo démarre.
 */
function Player({ uri }: { uri: string }) {
  const frame = useRef<ComponentRef<typeof View>>(null);
  const { height: screen } = useWindowDimensions();
  const player = useVideoPlayer({ uri }, (p) => {
    p.loop = false;
    p.play();
  });

  useEffect(() => {
    const take = () => {
      if (current && current !== player) pause(current);
      current = player;
    };
    take();
    const sub = player.addListener('playingChange', ({ isPlaying }) => { if (isPlaying) take(); });
    return () => {
      sub.remove();
      if (current === player) current = null;
    };
  }, [player]);

  // Position vérifiée pendant la lecture seulement (fonctionne dans toutes les listes)
  useEffect(() => {
    const timer = setInterval(() => {
      if (!player.playing) return;
      frame.current?.measureInWindow((_x, y, _w, h) => {
        if (!h) return;
        const visible = Math.min(y + h, screen) - Math.max(y, 0);
        if (visible < h * 0.4) pause(player);
      });
    }, 300);
    return () => clearInterval(timer);
  }, [player, screen]);

  useFocusEffect(useCallback(() => () => pause(player), [player]));

  return (
    <View ref={frame} collapsable={false} style={StyleSheet.absoluteFill}>
      <VideoView player={player} style={StyleSheet.absoluteFill} contentFit="contain" nativeControls />
    </View>
  );
}

const styles = StyleSheet.create({
  frame: { width: '100%', aspectRatio: 4 / 5, backgroundColor: '#000', marginTop: 4 },
  center: { position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, alignItems: 'center', justifyContent: 'center' },
  play: { width: 68, height: 68, borderRadius: 34, backgroundColor: 'rgba(0,0,0,0.55)', alignItems: 'center', justifyContent: 'center', paddingLeft: 4 },
});
