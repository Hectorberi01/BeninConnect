import { usePathname } from 'expo-router';
import { useEffect, useRef } from 'react';
import { Animated, Dimensions, Modal, PanResponder, Platform, Pressable, StyleSheet, View } from 'react-native';
import { create } from 'zustand';
import { Muted, QueryView, T } from '@/components/ui';
import { useColors } from '@/core/theme';
import { usePost } from '@/features/queries';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CommentsPanel, useKeyboardHeight } from './CommentsPanel';

const useSheet = create<{ postId: string | null }>(() => ({ postId: null }));

/** Ouvre les commentaires d'une publication dans la fenêtre du bas. */
export const openComments = (postId: string) => useSheet.setState({ postId });
const closeComments = () => useSheet.setState({ postId: null });

/** Monté une fois dans la zone connectée. */
export function CommentsSheetHost() {
  const postId = useSheet((s) => s.postId);
  const pathname = usePathname();
  // Navigation depuis la fenêtre (profil, mention) : on la ferme pour laisser voir l'écran ouvert
  useEffect(() => closeComments(), [pathname]);
  return (
    <Modal visible={!!postId} transparent animationType="slide" onRequestClose={closeComments} statusBarTranslucent>
      {postId && <Sheet postId={postId} />}
    </Modal>
  );
}

function Sheet({ postId }: { postId: string }) {
  const c = useColors();
  const query = usePost(postId);
  const keyboard = useKeyboardHeight();
  const insets = useSafeAreaInsets();
  const drag = useRef(new Animated.Value(0)).current;
  // Glisser la poignée ou le titre vers le bas pour fermer
  const pan = useRef(PanResponder.create({
    onMoveShouldSetPanResponder: (_, g) => g.dy > 6 && Math.abs(g.dy) > Math.abs(g.dx),
    onPanResponderMove: (_, g) => drag.setValue(Math.max(0, g.dy)),
    onPanResponderRelease: (_, g) => {
      if (g.dy > 120 || g.vy > 1) closeComments();
      else Animated.spring(drag, { toValue: 0, useNativeDriver: true }).start();
    },
  })).current;
  // Clavier ouvert : la fenêtre remonte au-dessus et occupe l'espace restant
  const screen = Dimensions.get(Platform.OS === 'android' ? 'screen' : 'window').height;
  const height = keyboard ? screen - keyboard - insets.top - 8 : screen * 0.75;
  const count = query.data?.commentCount ?? 0;

  return (
    <View style={styles.root}>
      <Pressable style={styles.backdrop} accessibilityRole="button" accessibilityLabel="Fermer les commentaires" onPress={closeComments} />
      <Animated.View style={[styles.sheet, { height, marginBottom: keyboard, backgroundColor: c.surface, transform: [{ translateY: drag }] }]}>
        <View {...pan.panHandlers} style={[styles.head, { borderColor: c.border }]}>
          <View style={[styles.grabber, { backgroundColor: c.border }]} />
          <View style={styles.titleRow}>
            <T v="label" style={{ fontSize: 16 }}>Commentaires</T>
            {count > 0 && <Muted style={{ fontSize: 14 }}>{count}</Muted>}
          </View>
        </View>
        <QueryView query={query}>{(post) => <CommentsPanel post={post} sheet />}</QueryView>
      </Animated.View>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, justifyContent: 'flex-end' },
  backdrop: { ...StyleSheet.absoluteFill, backgroundColor: 'rgba(0,0,0,0.45)' },
  sheet: { borderTopLeftRadius: 24, borderTopRightRadius: 24, overflow: 'hidden' },
  head: { alignItems: 'center', paddingTop: 8, paddingBottom: 12, borderBottomWidth: StyleSheet.hairlineWidth },
  grabber: { width: 40, height: 5, borderRadius: 3, marginBottom: 10 },
  titleRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
});
