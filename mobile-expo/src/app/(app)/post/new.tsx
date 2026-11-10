import { router, useLocalSearchParams, usePreventRemove } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, TextInput, View } from 'react-native';
import { Image } from 'expo-image';
import { ImageGrid } from '@/components/cards';
import { MentionSuggestions } from '@/components/social';
import { FormError, FormScroll } from '@/components/Form';
import { Avatar, Button, Header, Icon, Muted, Screen, Select, T } from '@/components/ui';
import { DEV_TOOLS } from '@/core/config';
import { confirm } from '@/core/dialog';
import { imagePaths, imageSource } from '@/core/images';
import * as fmt from '@/core/format';
import { askPhotoSource, MAX_VIDEO_SECONDS, pickPhoto, pickPhotos, pickVideo } from '@/core/photos';
import { data } from '@/data';
import { useMe } from '@/core/session';
import { font, radius, useColors } from '@/core/theme';
import { insertMention, keptMentions } from '@/data/social';
import type { PostVisibility, User } from '@/data/types';
import { displayName, errorMessage, initials, type LocalPhoto, type LocalVideo, postImages } from '@/data/types';
import { useCreatePost, useMemberCommunities, usePost, useUpdatePost, useUsersMap } from '@/features/queries';

const MAX_IMAGES = 10;
type Photo = string | LocalPhoto;
const photoKey = (p: Photo) => (typeof p === 'string' ? p : p.uri);
import { MAX_POST_LENGTH, validatePost } from '@/features/rules';

const VISIBILITIES: { value: PostVisibility; label: string; description: string }[] = [
  { value: 'Public', label: 'Public', description: 'Visible par tous les membres de BéninConnect' },
  { value: 'CommunityMembers', label: 'Membres de la communauté', description: 'Visible uniquement dans la communauté choisie' },
  { value: 'OnlyMe', label: 'Moi uniquement', description: 'Brouillon visible seulement par vous' },
];

/** Nouvelle publication, ou modification d'une publication existante (paramètre edit). */
export default function CreatePost() {
  const c = useColors();
  const me = useMe();
  const params = useLocalSearchParams<{ communityId?: string; image?: string; edit?: string }>();
  const editing = !!params.edit;
  const original = usePost(params.edit ?? '', editing).data;
  const mine = useMemberCommunities(me.id).data ?? [];
  const create = useCreatePost();
  const update = useUpdatePost();
  const saving = editing ? update : create;
  const [content, setContent] = useState('');
  const [communityId, setCommunityId] = useState<string | null>(params.communityId ?? null);
  const [visibility, setVisibility] = useState<PostVisibility>('Public');
  // Photos du carrousel : chemin déjà envoyé ou photo de l'appareil, envoyée au moment de publier.
  const [images, setImages] = useState<Photo[]>([]);
  const [cursor, setCursor] = useState(0);
  const [chosen, setChosen] = useState<User[]>([]);
  const mentioned = useUsersMap(original?.mentionIds ?? []);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [uploading, setUploading] = useState(false);
  // Vidéo : chemin déjà envoyé (modification) ou vidéo de l'appareil ; image = sa miniature.
  const [video, setVideo] = useState<string | LocalVideo | null>(null);
  const [progress, setProgress] = useState<number | null>(null);
  const preview = typeof video === 'object' && video ? video.thumbnailUri : typeof video === 'string' ? original?.imagePath ?? null : null;
  const [uploaded, setUploaded] = useState<string | null>(null);

  const addVideo = async () => {
    const choice = await askPhotoSource('Ajouter une vidéo');
    if (choice !== 'library' && choice !== 'camera') return;
    const picked = await pickVideo(choice);
    if (picked) {
      setVideo(picked);
      setImages([]);
      setPickerOpen(false);
    }
  };
  const removeVideo = () => setVideo(null);
  const removePhoto = (p: Photo) => setImages((list) => list.filter((x) => x !== p));
  const addPhotos = (list: Photo[]) => {
    setImages((old) => [...old, ...list].slice(0, MAX_IMAGES));
    setVideo(null);
    setPickerOpen(false);
  };
  const pickMention = (u: User) => {
    const next = insertMention(content, cursor, u);
    setContent(next.text);
    setCursor(next.cursor);
    setChosen((list) => (list.some((x) => x.id === u.id) ? list : [...list, u]));
  };

  const addPhoto = async () => {
    // Images de démonstration : outils de développement uniquement
    const choice = await askPhotoSource('Ajouter une photo', { demo: DEV_TOOLS ? 'Images de démonstration' : undefined });
    if (choice === 'demo') setPickerOpen(true);
    else if (choice === 'library') addPhotos(await pickPhotos(MAX_IMAGES - images.length));
    else if (choice === 'camera') {
      const photo = await pickPhoto(choice);
      if (photo) addPhotos([photo]);
    }
  };

  // « Publier une photo » depuis l'accueil : ouvre directement le choix.
  useEffect(() => {
    if (params.image === '1') addPhoto();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  const [error, setError] = useState<string | null>(null);
  const [leaving, setLeaving] = useState(false);

  // Modification : formulaire pré-rempli une fois la publication chargée.
  const [loaded, setLoaded] = useState(false);
  useEffect(() => {
    if (!original || loaded) return;
    setContent(original.content);
    setImages(postImages(original));
    setVideo(original.videoPath ?? null);
    setVisibility(original.visibility);
    setCommunityId(original.communityId);
    setLoaded(true);
  }, [original, loaded]);

  const dirty = editing
    ? loaded && (content.trim() !== original?.content || images.map(photoKey).join('|') !== (original ? postImages(original).join('|') : '')
      || (typeof video === 'string' ? video : video?.uri ?? null) !== (original?.videoPath ?? null) || visibility !== original?.visibility)
    : !!content.trim() || images.length > 0 || !!video;

  // Confirmation avant d'abandonner un brouillon (bouton retour, geste, Annuler).
  usePreventRemove(dirty && !leaving, ({ repeat }) => {
    const [title, message] = editing
      ? ['Abandonner les modifications ?', 'Vos changements ne seront pas enregistrés.']
      : ['Abandonner la publication ?', 'Votre texte ne sera pas conservé.'];
    confirm(title, message, 'Abandonner', true).then((ok) => {
      if (!ok) return;
      setLeaving(true);
      repeat();
    });
  });

  // Fermeture après publication, une fois la protection levée.
  useEffect(() => {
    if (leaving && saving.isSuccess) router.back();
  }, [leaving, saving.isSuccess]);

  const chooseCommunity = (id: string | null) => {
    setCommunityId(id);
    if (!id && visibility === 'CommunityMembers') setVisibility('Public');
  };

  const publish = async () => {
    const e = validatePost(content, visibility, communityId);
    setError(e);
    if (e) return;
    let imagePath: string | null = null;
    let videoPath: string | null = null;
    const paths: string[] = [];
    setUploading(true);
    try {
      if (video && typeof video === 'object') {
        // Miniature puis vidéo (progression affichée)
        if (video.thumbnailUri) imagePath = await data().uploadImage({ uri: video.thumbnailUri, mimeType: 'image/jpeg', fileName: 'miniature.jpg' });
        setProgress(0);
        videoPath = await data().uploadVideo(video, setProgress);
      } else if (video) {
        videoPath = video;
        imagePath = original?.imagePath ?? null;
      } else {
        // Photos envoyées une à une, dans l'ordre du carrousel
        for (const [i, photo] of images.entries()) {
          setUploaded(images.length > 1 ? `${i + 1}/${images.length}` : null);
          paths.push(typeof photo === 'string' ? photo : await data().uploadImage(photo));
        }
        imagePath = paths[0] ?? null;
      }
    } catch (err) {
      setError(`${video ? 'Envoi de la vidéo' : 'Envoi de la photo'} impossible : ${errorMessage(err)}`);
      return;
    } finally {
      setUploading(false);
      setProgress(null);
      setUploaded(null);
    }
    const mentions = keptMentions(content, [...chosen, ...mentioned.values()]);
    const callbacks = { onSuccess: () => setLeaving(true), onError: (err: unknown) => setError(errorMessage(err)) };
    if (editing) update.mutate({ id: params.edit!, request: { content: content.trim(), imagePath, visibility, videoPath, images: paths, mentions } }, callbacks);
    else create.mutate({ content: content.trim(), communityId, imagePath, visibility, videoPath, images: paths, mentions }, callbacks);
  };

  return (
    <Screen>
      <FormScroll header={
        <Header title={editing ? 'Modifier la publication' : 'Nouvelle publication'} right={
          <Button small title={editing ? 'Enregistrer' : 'Publier'} onPress={publish} loading={saving.isPending || uploading}
            disabled={!content.trim() || (editing && !dirty)} style={{ marginRight: 8 }} />
        } />
      }>
        <View style={styles.author}>
          <Avatar initials={initials(me)} color={me.avatarColor} image={me.avatarImage} />
          <View>
            <T v="label" style={{ fontSize: 15 }}>{displayName(me)}</T>
            <Muted>{VISIBILITIES.find((x) => x.value === visibility)?.label}</Muted>
          </View>
        </View>
        <FormError message={error} />
        <TextInput value={content} onChangeText={setContent} multiline autoFocus placeholder={`Quoi de neuf, ${me.firstName} ? (@ pour mentionner, # pour un sujet)`}
          placeholderTextColor={c.textSecondary} accessibilityLabel="Contenu de la publication"
          onSelectionChange={(e) => setCursor(e.nativeEvent.selection.end)}
          style={[styles.editor, { color: c.text, borderColor: c.border, backgroundColor: c.surface }]} />
        <View style={{ marginTop: 6, marginHorizontal: -12 }}><MentionSuggestions text={content} cursor={cursor} onPick={pickMention} /></View>
        <Muted style={{ alignSelf: 'flex-end', marginTop: 4, marginBottom: 12 }} color={content.length > MAX_POST_LENGTH ? c.error : undefined}>
          {`${content.length} / ${MAX_POST_LENGTH}`}
        </Muted>

        {images.length > 0 && (
          <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ marginBottom: 12 }} contentContainerStyle={{ gap: 8 }}>
            {images.map((p, i) => (
              <View key={photoKey(p)} style={styles.thumb}>
                <Image source={imageSource(photoKey(p))} style={StyleSheet.absoluteFill} contentFit="cover" />
                {i === 0 && images.length > 1 && <View style={styles.first}><T color="#FFFFFF" style={{ fontSize: 11 }}>Couverture</T></View>}
                <Pressable accessibilityRole="button" accessibilityLabel={`Retirer la photo ${i + 1}`} onPress={() => removePhoto(p)} style={styles.remove}>
                  <Icon name="close" size={16} color="#FFFFFF" />
                </Pressable>
              </View>
            ))}
          </ScrollView>
        )}
        {images.length > 0 && <Muted style={{ marginTop: -6, marginBottom: 10 }}>{`${images.length} / ${MAX_IMAGES} photos`}</Muted>}

        {video && (
          <View style={[styles.preview, { backgroundColor: '#000' }]}>
            {preview && <Image source={imageSource(preview)} style={StyleSheet.absoluteFill} contentFit="cover" />}
            {video && (
              <View style={styles.videoBadge}>
                <Icon name="play" size={18} color="#FFFFFF" />
                {typeof video === 'object' && video.durationMs > 0 && <T color="#FFFFFF" style={{ fontSize: 13 }}>{fmt.duration(video.durationMs)}</T>}
              </View>
            )}
            <Pressable accessibilityRole="button" accessibilityLabel="Retirer la vidéo" onPress={removeVideo} style={styles.remove}>
              <Icon name="close" size={18} color="#FFFFFF" />
            </Pressable>
          </View>
        )}

        {uploaded && <Muted style={{ marginBottom: 8 }} color={c.primary}>{`Envoi des photos… ${uploaded}`}</Muted>}
        {progress !== null && (
          <Muted style={{ marginBottom: 8 }} color={c.primary}>{`Envoi de la vidéo… ${Math.round(progress * 100)} %`}</Muted>
        )}
        <View style={{ flexDirection: 'row', gap: 10 }}>
          <Button title={images.length ? 'Ajouter des photos' : 'Photos'} icon="image-plus" kind="outline" onPress={addPhoto}
            disabled={images.length >= MAX_IMAGES} style={{ flex: 1 }} />
          <Button title={video ? 'Changer la vidéo' : 'Vidéo'} icon="video-plus-outline" kind="outline" onPress={addVideo} style={{ flex: 1 }} />
        </View>
        <Muted style={{ marginTop: 6 }}>{`Vidéo : ${MAX_VIDEO_SECONDS} secondes au plus.`}</Muted>
        {pickerOpen && (
          <View style={{ marginTop: 12 }}>
            <Muted style={{ marginBottom: 8 }}>Images de démonstration</Muted>
            <ImageGrid paths={imagePaths('Posts')} selected={null} onSelect={(p) => addPhotos([p])} />
          </View>
        )}

        <View style={{ height: 16 }} />
        {/* La communauté d'une publication ne change pas après coup */}
        {!editing && (
          <Select label="Communauté" value={communityId} onChange={chooseCommunity}
            options={[{ value: null, label: 'Aucune (fil public)' }, ...mine.map((x) => ({ value: x.id as string | null, label: x.name }))]} />
        )}
        <Select label="Visibilité" value={visibility} onChange={setVisibility}
          options={VISIBILITIES.filter((x) => x.value !== 'CommunityMembers' || communityId)} />
      </FormScroll>
    </Screen>
  );
}

const styles = StyleSheet.create({
  author: { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: 14 },
  editor: { minHeight: 160, borderWidth: 1, borderRadius: radius.control, padding: 14, fontSize: 16, fontFamily: font.regular, textAlignVertical: 'top' },
  thumb: { width: 110, height: 110, borderRadius: 12, overflow: 'hidden', backgroundColor: '#000' },
  first: { position: 'absolute', left: 6, bottom: 6, paddingHorizontal: 6, paddingVertical: 2, borderRadius: 8, backgroundColor: 'rgba(0,0,0,0.6)' },
  preview: { width: '100%', aspectRatio: 4 / 3, borderRadius: radius.card, overflow: 'hidden', marginBottom: 12 },
  videoBadge: {
    position: 'absolute', left: 10, bottom: 10, flexDirection: 'row', alignItems: 'center', gap: 4,
    paddingHorizontal: 10, height: 30, borderRadius: 15, backgroundColor: 'rgba(0,0,0,0.6)',
  },
  remove: { position: 'absolute', top: 8, right: 8, width: 32, height: 32, borderRadius: 16, backgroundColor: 'rgba(0,0,0,0.55)', alignItems: 'center', justifyContent: 'center' },
});
