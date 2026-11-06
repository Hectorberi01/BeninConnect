import * as ImagePicker from 'expo-image-picker';
import { Alert, Platform } from 'react-native';
import type { LocalPhoto } from '@/data/types';
import { info } from './dialog';

export type PhotoSource = 'library' | 'camera';
export type PhotoChoice = PhotoSource | 'remove' | 'demo';

/** Feuille de choix : galerie, appareil photo et options propres à l'écran. */
export function askPhotoSource(title: string, extra: { remove?: boolean; demo?: string } = {}): Promise<PhotoChoice | null> {
  // Pas d'appareil photo ni de feuille d'actions sur le web : directement la galerie.
  if (Platform.OS === 'web') return Promise.resolve('library');
  return new Promise((resolve) => {
    Alert.alert(title, undefined, [
      { text: 'Choisir dans la galerie', onPress: () => resolve('library') },
      { text: 'Prendre une photo', onPress: () => resolve('camera') },
      ...(extra.demo ? [{ text: extra.demo, onPress: () => resolve('demo' as const) }] : []),
      ...(extra.remove ? [{ text: 'Retirer la photo', style: 'destructive' as const, onPress: () => resolve('remove' as const) }] : []),
      { text: 'Annuler', style: 'cancel', onPress: () => resolve(null) },
    ], { cancelable: true, onDismiss: () => resolve(null) });
  });
}

/** Ouvre la galerie ou l'appareil photo ; null si annulé ou refusé. */
export async function pickPhoto(source: PhotoSource, options: { aspect?: [number, number] } = {}): Promise<LocalPhoto | null> {
  // Web : pas de permission à demander, et l'attendre ferait perdre le geste requis pour ouvrir le sélecteur.
  const permission = Platform.OS === 'web' ? { granted: true }
    : source === 'camera' ? await ImagePicker.requestCameraPermissionsAsync() : await ImagePicker.requestMediaLibraryPermissionsAsync();
  if (!permission.granted) {
    await info(source === 'camera' ? "Accès à l'appareil photo refusé" : 'Accès aux photos refusé',
      'Autorisez BéninConnect dans les Réglages de votre téléphone pour ajouter une photo.');
    return null;
  }

  const settings: ImagePicker.ImagePickerOptions = {
    mediaTypes: ['images'],
    allowsEditing: !!options.aspect,
    aspect: options.aspect,
    quality: 0.7, // JPEG compressé : envoi plus rapide (limite serveur 10 Mo)
    exif: false,
  };
  const result = source === 'camera' ? await ImagePicker.launchCameraAsync(settings) : await ImagePicker.launchImageLibraryAsync(settings);
  if (result.canceled || !result.assets[0]) return null;
  const asset = result.assets[0];
  return { uri: asset.uri, mimeType: asset.mimeType, fileName: asset.fileName };
}
