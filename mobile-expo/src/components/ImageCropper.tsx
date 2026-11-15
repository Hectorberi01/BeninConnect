import { Image } from 'expo-image';
import { ImageManipulator, SaveFormat } from 'expo-image-manipulator';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Animated, Modal, Pressable, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { info } from '@/core/dialog';
import { useZoomPan } from '@/core/zoom';
import type { LocalPhoto } from '@/data/types';

const FORMATS = [
  { key: 'square', label: 'Carré', ratio: 1 },
  { key: 'landscape', label: '4:3', ratio: 4 / 3 },
  { key: 'portrait', label: '3:4', ratio: 3 / 4 },
  { key: 'original', label: 'Original', ratio: null },
] as const;
type Format = (typeof FORMATS)[number]['key'];

/** Recadrage d'une photo : choisir un format, pincer et déplacer l'image dans le cadre. */
export function ImageCropper({ photo, onCancel, onDone }: { photo: LocalPhoto | null; onCancel: () => void; onDone: (p: LocalPhoto) => void }) {
  return (
    <Modal visible={!!photo} animationType="slide" onRequestClose={onCancel} statusBarTranslucent>
      {photo && <Cropper photo={photo} onCancel={onCancel} onDone={onDone} />}
    </Modal>
  );
}

function Cropper({ photo, onCancel, onDone }: { photo: LocalPhoto; onCancel: () => void; onDone: (p: LocalPhoto) => void }) {
  const insets = useSafeAreaInsets();
  const { width, height } = useWindowDimensions();
  const [size, setSize] = useState<{ w: number; h: number } | null>(null);
  const [format, setFormat] = useState<Format>('square');
  const [saving, setSaving] = useState(false);

  // Dimensions réelles (orientation appliquée)
  useEffect(() => {
    let alive = true;
    ImageManipulator.manipulate(photo.uri).renderAsync()
      .then((ref) => { if (alive) setSize({ w: ref.width, h: ref.height }); })
      .catch(() => { if (alive) onCancel(); });
    return () => { alive = false; };
  }, [photo.uri, onCancel]);

  const ratio = FORMATS.find((f) => f.key === format)!.ratio ?? (size ? size.w / size.h : 1);
  const maxW = width - 32;
  const maxH = height - insets.top - insets.bottom - 200;
  const frame = maxW / ratio <= maxH ? { w: maxW, h: maxW / ratio } : { w: maxH * ratio, h: maxH };

  const crop = async (state: { scale: number; x: number; y: number }, base: number) => {
    if (!size) return;
    const k = base * state.scale;
    const iw = size.w * k;
    const ih = size.h * k;
    const w = Math.min(size.w, Math.round(frame.w / k));
    const h = Math.min(size.h, Math.round(frame.h / k));
    const originX = Math.max(0, Math.min(size.w - w, Math.round(((iw - frame.w) / 2 - state.x) / k)));
    const originY = Math.max(0, Math.min(size.h - h, Math.round(((ih - frame.h) / 2 - state.y) / k)));
    if (w === size.w && h === size.h) return onDone(photo);
    setSaving(true);
    try {
      const image = await ImageManipulator.manipulate(photo.uri).crop({ originX, originY, width: w, height: h }).renderAsync();
      const saved = await image.saveAsync({ format: SaveFormat.JPEG, compress: 0.8 });
      onDone({ uri: saved.uri, mimeType: 'image/jpeg', fileName: 'photo.jpg' });
    } catch {
      await info('Recadrage impossible', 'La photo n’a pas pu être recadrée. Réessayez.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <View style={[styles.root, { paddingTop: insets.top, paddingBottom: insets.bottom + 12 }]}>
      <View style={styles.bar}>
        <Pressable accessibilityRole="button" onPress={onCancel} hitSlop={10}><Text style={styles.action}>Annuler</Text></Pressable>
        <Text style={styles.title}>Recadrer</Text>
        <View style={{ width: 70 }} />
      </View>
      <View style={styles.area}>
        {size ? <Frame key={`${format}:${frame.w}`} uri={photo.uri} size={size} frame={frame} saving={saving} onCrop={crop} />
          : <ActivityIndicator color="#FFFFFF" />}
      </View>
      <View style={styles.formats}>
        {FORMATS.map((f) => (
          <Pressable key={f.key} accessibilityRole="button" accessibilityState={{ selected: f.key === format }} onPress={() => setFormat(f.key)}
            style={[styles.format, f.key === format && styles.formatOn]}>
            <Text style={[styles.formatText, f.key === format && { color: '#000000' }]}>{f.label}</Text>
          </Pressable>
        ))}
      </View>
      <Text style={styles.hint}>Pincez pour zoomer, faites glisser pour cadrer.</Text>
    </View>
  );
}

/** Cadre de recadrage ; la photo le couvre toujours entièrement. */
function Frame({ uri, size, frame, saving, onCrop }: {
  uri: string; size: { w: number; h: number }; frame: { w: number; h: number }; saving: boolean;
  onCrop: (state: { scale: number; x: number; y: number }, base: number) => void;
}) {
  const base = Math.max(frame.w / size.w, frame.h / size.h);
  const w = size.w * base;
  const h = size.h * base;
  const zoom = useZoomPan({ max: 5, bounds: (s) => ({ x: Math.max(0, (w * s - frame.w) / 2), y: Math.max(0, (h * s - frame.h) / 2) }) });
  return (
    <>
      <View {...zoom.handlers} style={[styles.frame, { width: frame.w, height: frame.h }]}>
        <Animated.View style={[{ width: w, height: h }, zoom.style]}>
          <Image source={{ uri }} style={StyleSheet.absoluteFill} contentFit="cover" />
        </Animated.View>
        <View pointerEvents="none" style={styles.grid}>
          <View style={[styles.line, { left: '33.3%', top: 0, bottom: 0, width: StyleSheet.hairlineWidth }]} />
          <View style={[styles.line, { left: '66.6%', top: 0, bottom: 0, width: StyleSheet.hairlineWidth }]} />
          <View style={[styles.line, { top: '33.3%', left: 0, right: 0, height: StyleSheet.hairlineWidth }]} />
          <View style={[styles.line, { top: '66.6%', left: 0, right: 0, height: StyleSheet.hairlineWidth }]} />
        </View>
      </View>
      <Pressable accessibilityRole="button" disabled={saving} onPress={() => onCrop(zoom.current(), base)} style={styles.done}>
        {saving ? <ActivityIndicator color="#000000" /> : <Text style={styles.doneText}>Valider</Text>}
      </Pressable>
    </>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#000000' },
  bar: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 16, height: 52 },
  title: { color: '#FFFFFF', fontSize: 17, fontWeight: '600' },
  action: { color: '#FFFFFF', fontSize: 16, width: 70 },
  area: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 20 },
  frame: { overflow: 'hidden', alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: 'rgba(255,255,255,0.9)' },
  grid: StyleSheet.absoluteFill,
  line: { position: 'absolute', backgroundColor: 'rgba(255,255,255,0.45)' },
  formats: { flexDirection: 'row', justifyContent: 'center', gap: 8, marginTop: 12 },
  format: { paddingHorizontal: 14, paddingVertical: 8, borderRadius: 18, backgroundColor: 'rgba(255,255,255,0.15)' },
  formatOn: { backgroundColor: '#FFFFFF' },
  formatText: { color: '#FFFFFF', fontSize: 14, fontWeight: '600' },
  hint: { color: 'rgba(255,255,255,0.6)', textAlign: 'center', marginTop: 10, fontSize: 13 },
  done: { backgroundColor: '#FFFFFF', borderRadius: 22, paddingHorizontal: 28, height: 44, alignItems: 'center', justifyContent: 'center', minWidth: 120 },
  doneText: { color: '#000000', fontSize: 16, fontWeight: '600' },
});
