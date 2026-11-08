import { RecordingPresets, requestRecordingPermissionsAsync, setAudioModeAsync, useAudioRecorder, useAudioRecorderState } from 'expo-audio';
import { useEffect, useRef } from 'react';
import { info } from '@/core/dialog';
import type { OutgoingFile } from '@/data/types';

/** Durée maximale d'un message vocal. */
export const MAX_VOICE_MS = 5 * 60_000;

/** Enregistrement d'un message vocal (AAC .m4a) ; arrêt automatique à 5 minutes. */
export function useVoiceRecorder(onAutoStop: (file: OutgoingFile) => void) {
  const recorder = useAudioRecorder(RecordingPresets.HIGH_QUALITY);
  const state = useAudioRecorderState(recorder, 250);
  const stopping = useRef(false);

  const start = async () => {
    const permission = await requestRecordingPermissionsAsync();
    if (!permission.granted) {
      info('Accès au micro refusé', 'Autorisez BéninConnect à utiliser le micro dans les Réglages de votre téléphone pour envoyer un message vocal.');
      return false;
    }
    await setAudioModeAsync({ allowsRecording: true, playsInSilentMode: true });
    await recorder.prepareToRecordAsync();
    recorder.record();
    stopping.current = false;
    return true;
  };

  /** Arrête et retourne le fichier (null : trop court ou annulé). */
  const stop = async (): Promise<OutgoingFile | null> => {
    if (stopping.current) return null;
    stopping.current = true;
    const durationMs = recorder.currentTime * 1000 || state.durationMillis;
    await recorder.stop();
    await setAudioModeAsync({ allowsRecording: false, playsInSilentMode: true }); // lecture sur le haut-parleur
    const uri = recorder.uri;
    if (!uri || durationMs < 800) return null;
    return { uri, name: `message-vocal-${Date.now()}.m4a`, mimeType: 'audio/mp4', durationMs };
  };

  useEffect(() => {
    if (state.isRecording && state.durationMillis >= MAX_VOICE_MS) {
      stop().then((file) => file && onAutoStop(file)).catch(() => undefined);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state.isRecording, state.durationMillis]);

  return { recording: state.isRecording, durationMs: state.durationMillis, start, stop };
}
