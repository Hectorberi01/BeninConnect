import { Image } from 'expo-image';
import { useState } from 'react';
import { Animated, FlatList, Modal, Pressable, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { create } from 'zustand';
import { Icon } from '@/components/ui';
import { imageSource } from '@/core/images';
import { useZoomPan } from '@/core/zoom';

const useViewer = create<{ paths: string[]; index: number }>(() => ({ paths: [], index: 0 }));

/** Affiche des photos en plein écran (zoom, défilement entre elles). */
export function openImages(paths: (string | null | undefined)[], index = 0) {
  const list = paths.filter((p): p is string => !!p);
  if (list.length) useViewer.setState({ paths: list, index: Math.min(index, list.length - 1) });
}
const close = () => useViewer.setState({ paths: [], index: 0 });

/** Monté une fois dans la zone connectée. */
export function ImageViewerHost() {
  const { paths, index } = useViewer();
  return (
    <Modal visible={paths.length > 0} transparent animationType="fade" onRequestClose={close} statusBarTranslucent>
      {paths.length > 0 && <Viewer paths={paths} start={index} />}
    </Modal>
  );
}

function Viewer({ paths, start }: { paths: string[]; start: number }) {
  const { width, height } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const [index, setIndex] = useState(start);
  const [zoomed, setZoomed] = useState(false);
  return (
    <View style={styles.root}>
      <FlatList data={paths} horizontal pagingEnabled scrollEnabled={!zoomed} initialScrollIndex={start} showsHorizontalScrollIndicator={false}
        keyExtractor={(p, i) => `${i}:${p}`} getItemLayout={(_, i) => ({ length: width, offset: width * i, index: i })}
        onMomentumScrollEnd={(e) => setIndex(Math.round(e.nativeEvent.contentOffset.x / width))}
        renderItem={({ item }) => <Page path={item} width={width} height={height} onZoom={setZoomed} />} />
      <View style={[styles.top, { top: insets.top + 8 }]} pointerEvents="box-none">
        {paths.length > 1 ? <Text style={styles.counter}>{`${index + 1} / ${paths.length}`}</Text> : <View />}
        <Pressable accessibilityRole="button" accessibilityLabel="Fermer" onPress={close} hitSlop={12} style={styles.close}>
          <Icon name="close" size={24} color="#FFFFFF" />
        </Pressable>
      </View>
    </View>
  );
}

function Page({ path, width, height, onZoom }: { path: string; width: number; height: number; onZoom: (z: boolean) => void }) {
  const [ratio, setRatio] = useState(1);
  // Image entière à l'écran au départ
  const w = width / height > ratio ? height * ratio : width;
  const h = width / height > ratio ? height : width / ratio;
  const zoom = useZoomPan({
    bounds: (s) => ({ x: Math.max(0, (w * s - width) / 2), y: Math.max(0, (h * s - height) / 2) }),
    onZoomChange: onZoom, onSwipeDown: close,
  });
  return (
    <View {...zoom.handlers} style={[styles.page, { width, height }]}>
      <Animated.View style={[{ width: w, height: h }, zoom.style]}>
        <Image source={imageSource(path)} style={StyleSheet.absoluteFill} contentFit="contain" transition={120}
          onLoad={(e) => e.source.width && e.source.height && setRatio(e.source.width / e.source.height)} accessibilityLabel="Photo" />
      </Animated.View>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#000000' },
  page: { alignItems: 'center', justifyContent: 'center', overflow: 'hidden' },
  top: { position: 'absolute', left: 16, right: 16, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  counter: { color: '#FFFFFF', fontSize: 15, fontWeight: '600' },
  close: { width: 40, height: 40, borderRadius: 20, backgroundColor: 'rgba(255,255,255,0.15)', alignItems: 'center', justifyContent: 'center' },
});
