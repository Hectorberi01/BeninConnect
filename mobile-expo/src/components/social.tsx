import { Image } from 'expo-image';
import { router } from 'expo-router';
import { useRef, useState } from 'react';
import { Animated, FlatList, Pressable, type StyleProp, StyleSheet, Text, type TextStyle, View } from 'react-native';
import { imageSource } from '@/core/images';
import { depth, radius, type, useColors } from '@/core/theme';
import { parseSocialText, mentionQuery } from '@/data/social';
import type { Post, User } from '@/data/types';
import { displayName, initials, postImages } from '@/data/types';
import { useQuery } from '@tanstack/react-query';
import { data } from '@/data';
import { usePost, useUsersMap } from '@/features/queries';
import { PostVideo } from './video';
import { Avatar, Icon, Muted, T } from './ui';

// --- Texte avec @mentions et #hashtags cliquables ---------------------------------------------

export function RichText({ text, mentionIds, lines, color, style }: {
  text: string; mentionIds?: string[]; lines?: number; color?: string; style?: StyleProp<TextStyle>;
}) {
  const c = useColors();
  const users = useUsersMap(mentionIds ?? []);
  const parts = parseSocialText(text, [...users.values()]);
  return (
    <T lines={lines} color={color} style={style}>
      {parts.map((p, i) => {
        if (p.kind === 'text') return p.text;
        const link = { color: color ? undefined : c.primary, fontWeight: '600' as const, textDecorationLine: color ? 'underline' as const : 'none' as const };
        return (
          <Text key={i} style={link} suppressHighlighting
            onPress={() => (p.kind === 'tag'
              ? router.push({ pathname: '/tag/[tag]', params: { tag: p.tag } })
              : router.push(`/user/${p.userId}`))}>
            {p.text}
          </Text>
        );
      })}
    </T>
  );
}

// --- Photos et vidéo d'une publication : carrousel, double appui pour aimer --------------------

const DOUBLE_TAP_MS = 280;

export function PostMedia({ post, onSingleTap, onDoubleTap }: { post: Post; onSingleTap?: () => void; onDoubleTap: () => void }) {
  const c = useColors();
  const images = postImages(post);
  const [width, setWidth] = useState(0);
  const [index, setIndex] = useState(0);
  const heart = useRef(new Animated.Value(0)).current;
  const lastTap = useRef(0);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const burst = () => {
    heart.setValue(0);
    Animated.sequence([
      Animated.spring(heart, { toValue: 1, friction: 4, tension: 140, useNativeDriver: true }),
      Animated.timing(heart, { toValue: 0, duration: 260, delay: 280, useNativeDriver: true }),
    ]).start();
  };

  const tap = () => {
    const now = Date.now();
    if (now - lastTap.current < DOUBLE_TAP_MS) {
      lastTap.current = 0;
      if (timer.current) clearTimeout(timer.current);
      burst();
      onDoubleTap();
      return;
    }
    lastTap.current = now;
    if (onSingleTap) timer.current = setTimeout(onSingleTap, DOUBLE_TAP_MS);
  };

  if (post.videoPath) return <PostVideo videoPath={post.videoPath} posterPath={post.imagePath} />;
  if (images.length === 0) return null;

  return (
    <View onLayout={(e) => setWidth(e.nativeEvent.layout.width)}>
      {width > 0 && (
        <FlatList
          data={images}
          keyExtractor={(p, i) => `${i}:${p}`}
          horizontal
          pagingEnabled
          scrollEnabled={images.length > 1}
          showsHorizontalScrollIndicator={false}
          onMomentumScrollEnd={(e) => setIndex(Math.round(e.nativeEvent.contentOffset.x / width))}
          renderItem={({ item, index: i }) => (
            <Pressable onPress={tap} accessibilityLabel={`Photo ${i + 1} sur ${images.length}`} accessibilityHint="Double appui pour aimer">
              <Image source={imageSource(item)} style={{ width, aspectRatio: images.length > 1 ? 1 : 4 / 3 }} contentFit="cover" transition={150} />
            </Pressable>
          )}
        />
      )}
      {width === 0 && <View style={{ width: '100%', aspectRatio: images.length > 1 ? 1 : 4 / 3 }} />}

      <Animated.View pointerEvents="none" style={[StyleSheet.absoluteFill, styles.center,
        { opacity: heart, transform: [{ scale: heart.interpolate({ inputRange: [0, 1], outputRange: [0.4, 1] }) }] }]}>
        <Icon name="heart" size={96} color="#FFFFFF" />
      </Animated.View>

      {images.length > 1 && (
        <>
          <View style={[styles.counter, { backgroundColor: 'rgba(0,0,0,0.55)' }]}>
            <Text style={[type.caption, { color: '#FFFFFF', fontSize: 12 }]}>{`${index + 1}/${images.length}`}</Text>
          </View>
          <View style={styles.dots}>
            {images.map((p, i) => (
              <View key={`${i}:${p}`} style={[styles.dot, { backgroundColor: i === index ? c.primary : c.border }]} />
            ))}
          </View>
        </>
      )}
    </View>
  );
}

// --- Suggestions de mentions pendant la saisie ------------------------------------------------

export function MentionSuggestions({ text, cursor, onPick }: { text: string; cursor: number; onPick: (u: User) => void }) {
  const c = useColors();
  const q = mentionQuery(text, cursor);
  const search = q?.query ?? '';
  const users = useQuery({ queryKey: ['users', search], queryFn: () => data().searchUsers(search), enabled: search.length > 0, staleTime: 30_000 });
  if (!q || q.query.length < 1) return null;
  const list = (users.data ?? []).slice(0, 5);
  if (list.length === 0) return null;
  return (
    <View style={[styles.suggestions, { backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 2)]}>
      {list.map((u) => (
        <Pressable key={u.id} accessibilityRole="button" accessibilityLabel={`Mentionner ${displayName(u)}`} onPress={() => onPick(u)}
          style={({ pressed }) => [styles.suggestion, pressed && { opacity: 0.6 }]}>
          <Avatar initials={initials(u)} color={u.avatarColor} image={u.avatarImage} size={30} />
          <T v="label" lines={1} style={{ flex: 1, fontSize: 14 }}>{displayName(u)}</T>
          {!!u.city && <Muted lines={1}>{u.city}</Muted>}
        </Pressable>
      ))}
    </View>
  );
}

// --- Publication envoyée dans une discussion --------------------------------------------------

export function SharedPostCard({ postId, mine }: { postId: string; mine: boolean }) {
  const c = useColors();
  const q = usePost(postId);
  const users = useUsersMap(q.data ? [q.data.authorId] : []);
  const textColor = mine ? '#FFFFFF' : c.text;
  if (q.isError) {
    return (
      <View style={[styles.shared, { borderColor: mine ? 'rgba(255,255,255,0.35)' : c.border }]}>
        <Muted color={mine ? '#D1FAE5' : undefined} style={{ fontStyle: 'italic' }}>Publication indisponible</Muted>
      </View>
    );
  }
  const post = q.data;
  const author = post ? users.get(post.authorId) : undefined;
  const cover = post ? postImages(post)[0] ?? post.imagePath : null;
  return (
    <Pressable accessibilityRole="button" accessibilityLabel="Ouvrir la publication" onPress={() => router.push(`/post/${postId}`)}
      style={({ pressed }) => [styles.shared, { borderColor: mine ? 'rgba(255,255,255,0.35)' : c.border }, pressed && { opacity: 0.8 }]}>
      {cover && <Image source={imageSource(cover)} style={styles.sharedImage} contentFit="cover" />}
      <View style={{ padding: 10, gap: 4 }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
          {author && <Avatar initials={initials(author)} color={author.avatarColor} image={author.avatarImage} size={22} />}
          <T v="label" lines={1} color={textColor} style={{ fontSize: 13, flex: 1 }}>{author ? displayName(author) : 'Publication'}</T>
        </View>
        <T lines={3} color={textColor} style={{ fontSize: 14 }}>{post ? post.content : 'Chargement…'}</T>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  center: { alignItems: 'center', justifyContent: 'center' },
  counter: { position: 'absolute', top: 10, right: 10, borderRadius: 12, paddingHorizontal: 8, paddingVertical: 2 },
  dots: { flexDirection: 'row', justifyContent: 'center', gap: 5, paddingTop: 8 },
  dot: { width: 6, height: 6, borderRadius: 3 },
  suggestions: { borderRadius: radius.card, borderWidth: 1, marginHorizontal: 12, marginBottom: 6, overflow: 'hidden' },
  suggestion: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 12, paddingVertical: 8 },
  shared: { borderWidth: 1, borderRadius: 14, overflow: 'hidden', width: 230, marginBottom: 4 },
  sharedImage: { width: '100%', height: 120 },
});
