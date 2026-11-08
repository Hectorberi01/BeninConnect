import { router, useLocalSearchParams, usePreventRemove } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, TextInput, View } from 'react-native';
import { Image } from 'expo-image';
import { ImageGrid } from '@/components/cards';
import { FormError, FormScroll } from '@/components/Form';
import { Avatar, Button, Header, Icon, Muted, Screen, Select, T } from '@/components/ui';
import { DEV_TOOLS } from '@/core/config';
import { confirm } from '@/core/dialog';
import { imagePaths, imageSource } from '@/core/images';
import * as fmt from '@/core/format';
import { askPhotoSource, MAX_VIDEO_SECONDS, pickPhoto, pickVideo } from '@/core/photos';
import { data } from '@/data';
import { useMe } from '@/core/session';
import { font, radius, useColors } from '@/core/theme';
import type { PostVisibility } from '@/data/types';
import { displayName, errorMessage, initials, type LocalPhoto, type LocalVideo } from '@/data/types';
import { useCreatePost, useMemberCommunities, usePost, useUpdatePost } from '@/features/queries';
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
  // Image embarquée (chemin) ou photo de l'appareil, envoyée au moment de publier.
  const [image, setImage] = useState<string | LocalPhoto | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [uploading, setUploading] = useState(false);
  // Vidéo : chemin déjà envoyé (modification) ou vidéo de l'appareil ; image = sa miniature.
  const [video, setVideo] = useState<string | LocalVideo | null>(null);
  const [progress, setProgress] = useState<number | null>(null);
  const preview = typeof video === 'object' && video ? video.thumbnailUri
    : typeof image === 'string' || image === null ? image : image.uri;

  const addVideo = async () => {
    const choice = await askPhotoSource('Ajouter une vidéo');
    if (choice !== 'library' && choice !== 'camera') return;
    const picked = await pickVideo(choice);
    if (picked) {
      setVideo(picked);
      setImage(null);
      setPickerOpen(false);
    }
  };
  const removeMedia = () => {
    setImage(null);
    setVideo(null);
  };

  const addPhoto = async () => {
    // Images de démonstration : outils de développement uniquement
    const choice = await askPhotoSource('Ajouter une photo', { demo: DEV_TOOLS ? 'Images de démonstration' : undefined });
    if (choice === 'demo') setPickerOpen(true);
    else if (choice === 'library' || choice === 'camera') {
      const photo = await pickPhoto(choice);
      if (photo) {
        setImage(photo);
        setVideo(null);
        setPickerOpen(false);
      }
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
    setImage(original.imagePath);
    setVideo(original.videoPath ?? null);
    setVisibility(original.visibility);
    setCommunityId(original.communityId);
    setLoaded(true);
  }, [original, loaded]);

  const dirty = editing
    ? loaded && (content.trim() !== original?.content || (typeof image === 'string' ? image : image?.uri ?? null) !== original?.imagePath
      || (typeof video === 'string' ? video : video?.uri ?? null) !== (original?.videoPath ?? null) || visibility !== original?.visibility)
    : !!content.trim() || !!image || !!video;

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
    setUploading(true);
    try {
      if (video && typeof video === 'object') {
        // Miniature puis vidéo (progression affichée)
        if (video.thumbnailUri) imagePath = await data().uploadImage({ uri: video.thumbnailUri, mimeType: 'image/jpeg', fileName: 'miniature.jpg' });
        setProgress(0);
        videoPath = await data().uploadVideo(video, setProgress);
      } else {
        videoPath = video;
        if (image) imagePath = typeof image === 'string' ? image : await data().uploadImage(image);
      }
    } catch (err) {
      setError(`${video ? 'Envoi de la vidéo' : 'Envoi de la photo'} impossible : ${errorMessage(err)}`);
      return;
    } finally {
      setUploading(false);
      setProgress(null);
    }
    const callbacks = { onSuccess: () => setLeaving(true), onError: (err: unknown) => setError(errorMessage(err)) };
    if (editing) update.mutate({ id: params.edit!, request: { content: content.trim(), imagePath, visibility, videoPath } }, callbacks);
    else create.mutate({ content: content.trim(), communityId, imagePath, visibility, videoPath }, callbacks);
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
        <TextInput value={content} onChangeText={setContent} multiline autoFocus placeholder={`Quoi de neuf, ${me.firstName} ?`}
          placeholderTextColor={c.textSecondary} accessibilityLabel="Contenu de la publication"
          style={[styles.editor, { color: c.text, borderColor: c.border, backgroundColor: c.surface }]} />
        <Muted style={{ alignSelf: 'flex-end', marginTop: 4, marginBottom: 12 }} color={content.length > MAX_POST_LENGTH ? c.error : undefined}>
          {`${content.length} / ${MAX_POST_LENGTH}`}
        </Muted>

        {(preview || video) && (
          <View style={[styles.preview, { backgroundColor: '#000' }]}>
            {preview && <Image source={imageSource(preview)} style={StyleSheet.absoluteFill} contentFit="cover" />}
            {video && (
              <View style={styles.videoBadge}>
                <Icon name="play" size={18} color="#FFFFFF" />
                {typeof video === 'object' && video.durationMs > 0 && <T color="#FFFFFF" style={{ fontSize: 13 }}>{fmt.duration(video.durationMs)}</T>}
              </View>
            )}
            <Pressable accessibilityRole="button" accessibilityLabel={video ? 'Retirer la vidéo' : "Retirer l'image"} onPress={removeMedia} style={styles.remove}>
              <Icon name="close" size={18} color="#FFFFFF" />
            </Pressable>
          </View>
        )}

        {progress !== null && (
          <Muted style={{ marginBottom: 8 }} color={c.primary}>{`Envoi de la vidéo… ${Math.round(progress * 100)} %`}</Muted>
        )}
        <View style={{ flexDirection: 'row', gap: 10 }}>
          <Button title={image && !video ? 'Changer la photo' : 'Photo'} icon="image-plus" kind="outline" onPress={addPhoto} style={{ flex: 1 }} />
          <Button title={video ? 'Changer la vidéo' : 'Vidéo'} icon="video-plus-outline" kind="outline" onPress={addVideo} style={{ flex: 1 }} />
        </View>
        <Muted style={{ marginTop: 6 }}>{`Vidéo : ${MAX_VIDEO_SECONDS} secondes au plus.`}</Muted>
        {pickerOpen && (
          <View style={{ marginTop: 12 }}>
            <Muted style={{ marginBottom: 8 }}>Images de démonstration</Muted>
            <ImageGrid paths={imagePaths('Posts')} selected={typeof image === 'string' ? image : null} onSelect={(p) => { setImage(p); setPickerOpen(false); }} />
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
  preview: { width: '100%', aspectRatio: 4 / 3, borderRadius: radius.card, overflow: 'hidden', marginBottom: 12 },
  videoBadge: {
    position: 'absolute', left: 10, bottom: 10, flexDirection: 'row', alignItems: 'center', gap: 4,
    paddingHorizontal: 10, height: 30, borderRadius: 15, backgroundColor: 'rgba(0,0,0,0.6)',
  },
  remove: { position: 'absolute', top: 8, right: 8, width: 32, height: 32, borderRadius: 16, backgroundColor: 'rgba(0,0,0,0.55)', alignItems: 'center', justifyContent: 'center' },
});
