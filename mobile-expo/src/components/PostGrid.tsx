import { Image } from 'expo-image';
import { router } from 'expo-router';
import { Pressable, StyleSheet, View } from 'react-native';
import { imageSource } from '@/core/images';
import { useColors } from '@/core/theme';
import type { Post } from '@/data/types';
import { postImages } from '@/data/types';
import { Icon, T } from './ui';

/** Grille 3 colonnes du profil : photo (ou début du texte), icônes épingle, carrousel et vidéo. */
export function PostGrid({ posts }: { posts: Post[] }) {
  const c = useColors();
  return (
    <View style={styles.grid}>
      {posts.map((p) => {
        const images = postImages(p);
        const cover = images[0] ?? p.imagePath;
        return (
          <Pressable key={p.id} accessibilityRole="button" accessibilityLabel={`Publication : ${p.content.slice(0, 60)}`}
            onPress={() => router.push(`/post/${p.id}`)} style={({ pressed }) => [styles.cell, pressed && { opacity: 0.8 }]}>
            <View style={[styles.inner, { backgroundColor: c.surfaceVariant }]}>
            {cover
              ? <Image source={imageSource(cover)} style={StyleSheet.absoluteFill} contentFit="cover" transition={120} />
              : (
                <View style={[StyleSheet.absoluteFill, styles.text, { backgroundColor: c.primaryLight }]}>
                  <T lines={5} style={{ fontSize: 12, lineHeight: 16 }} color={c.primaryDark}>{p.content}</T>
                </View>
              )}
            <View style={styles.badges} pointerEvents="none">
              {p.pinnedAt && <Icon name="pin" size={16} color="#FFFFFF" />}
              {p.videoPath ? <Icon name="play" size={18} color="#FFFFFF" />
                : images.length > 1 && <Icon name="checkbox-multiple-blank" size={16} color="#FFFFFF" />}
            </View>
            </View>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  grid: { flexDirection: 'row', flexWrap: 'wrap', paddingHorizontal: 1 },
  cell: { width: '33.333%', aspectRatio: 1, padding: 1 },
  inner: { flex: 1, overflow: 'hidden' },
  text: { padding: 8 },
  badges: { position: 'absolute', top: 6, right: 6, flexDirection: 'row', gap: 4 },
});
