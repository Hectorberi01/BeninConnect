import { useAudioPlayer, useAudioPlayerStatus } from 'expo-audio';
import { Image } from 'expo-image';
import * as WebBrowser from 'expo-web-browser';
import { useState } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import * as fmt from '@/core/format';
import { toast } from '@/core/toast';
import { font, radius, type, useColors } from '@/core/theme';
import { data } from '@/data';
import type { Message } from '@/data/types';
import { Icon, IconButton } from './ui';

/** Pièce jointe d'un message : photo, message vocal ou document. */
export function AttachmentView({ message, mine }: { message: Message; mine: boolean }) {
  const attachment = message.attachment;
  const url = data().attachmentUrl(message);
  if (!attachment || !url) return null;
  switch (attachment.kind) {
    case 'Image': return <PhotoAttachment url={url} />;
    case 'Audio': return <VoiceAttachment url={url} durationMs={attachment.durationMs ?? 0} mine={mine} />;
    default: return <FileAttachment url={url} name={attachment.fileName} size={attachment.size} mine={mine} />;
  }
}

function PhotoAttachment({ url }: { url: string }) {
  const insets = useSafeAreaInsets();
  const [open, setOpen] = useState(false);
  return (
    <>
      <Pressable accessibilityRole="imagebutton" accessibilityLabel="Photo, toucher pour agrandir" onPress={() => setOpen(true)}>
        <Image source={{ uri: url }} style={styles.photo} contentFit="cover" transition={150} />
      </Pressable>
      <Modal visible={open} animationType="fade" onRequestClose={() => setOpen(false)} statusBarTranslucent>
        <View style={styles.viewer}>
          <Image source={{ uri: url }} style={StyleSheet.absoluteFill} contentFit="contain" />
          <View style={[styles.viewerClose, { top: insets.top + 8 }]}>
            <IconButton name="close" label="Fermer" color="#FFFFFF" onPress={() => setOpen(false)} />
          </View>
        </View>
      </Modal>
    </>
  );
}

function VoiceAttachment({ url, durationMs, mine }: { url: string; durationMs: number; mine: boolean }) {
  const c = useColors();
  const player = useAudioPlayer({ uri: url }, { updateInterval: 250 });
  const status = useAudioPlayerStatus(player);
  const total = status.duration > 0 ? status.duration * 1000 : durationMs;
  const progress = total > 0 ? Math.min(1, (status.currentTime * 1000) / total) : 0;
  const color = mine ? '#FFFFFF' : c.primary;

  const toggle = () => {
    if (status.playing) return player.pause();
    if (status.didJustFinish || (total > 0 && status.currentTime * 1000 >= total - 200)) player.seekTo(0);
    player.play();
  };

  return (
    <View style={styles.voice}>
      <Pressable accessibilityRole="button" accessibilityLabel={status.playing ? 'Pause' : 'Écouter le message vocal'} onPress={toggle} hitSlop={8}
        style={[styles.play, { backgroundColor: mine ? 'rgba(255,255,255,0.22)' : c.primaryLight }]}>
        <Icon name={status.playing ? 'pause' : 'play'} size={22} color={color} />
      </Pressable>
      <View style={{ flex: 1, gap: 6 }}>
        <View style={[styles.track, { backgroundColor: mine ? 'rgba(255,255,255,0.3)' : c.border }]}>
          <View style={[styles.trackFill, { width: `${progress * 100}%`, backgroundColor: color }]} />
        </View>
        <Text style={[type.caption, { color: mine ? '#D1FAE5' : c.textSecondary, fontSize: 12 }]}>
          {fmt.duration(status.playing || status.currentTime > 0 ? status.currentTime * 1000 : total)}
        </Text>
      </View>
    </View>
  );
}

function FileAttachment({ url, name, size, mine }: { url: string; name: string; size: number; mine: boolean }) {
  const c = useColors();
  const open = () => WebBrowser.openBrowserAsync(url).catch(() => toast("Impossible d'ouvrir le document.", 'error'));
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={`Ouvrir ${name}`} onPress={open}
      style={({ pressed }) => [styles.file, { backgroundColor: mine ? 'rgba(255,255,255,0.16)' : c.surfaceVariant }, pressed && { opacity: 0.7 }]}>
      <Icon name="file-pdf-box" size={34} color={mine ? '#FFFFFF' : c.error} />
      <View style={{ flex: 1 }}>
        <Text numberOfLines={2} style={[styles.fileName, { color: mine ? '#FFFFFF' : c.text }]}>{name}</Text>
        {size > 0 && <Text style={[type.caption, { color: mine ? '#D1FAE5' : c.textSecondary }]}>{`PDF · ${fmt.fileSize(size)}`}</Text>}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  photo: { width: 230, height: 230, borderRadius: radius.control, marginBottom: 4 },
  viewer: { flex: 1, backgroundColor: '#000' },
  viewerClose: { position: 'absolute', right: 12, borderRadius: 20, backgroundColor: 'rgba(0,0,0,0.5)' },
  voice: { flexDirection: 'row', alignItems: 'center', gap: 10, width: 220, paddingVertical: 2 },
  play: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center' },
  track: { height: 4, borderRadius: 2, overflow: 'hidden' },
  trackFill: { height: 4, borderRadius: 2 },
  file: { flexDirection: 'row', alignItems: 'center', gap: 10, padding: 10, borderRadius: radius.control, width: 240, marginBottom: 4 },
  fileName: { fontSize: 15, fontFamily: font.semibold },
});
