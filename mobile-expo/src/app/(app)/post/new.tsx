import { router, useLocalSearchParams, usePreventRemove } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, TextInput, View } from 'react-native';
import { Image } from 'expo-image';
import { ImageGrid } from '@/components/cards';
import { FormError, FormScroll } from '@/components/Form';
import { Avatar, Button, Header, Icon, Muted, Screen, Select, T } from '@/components/ui';
import { confirm } from '@/core/dialog';
import { imagePaths, imageSource } from '@/core/images';
import { askPhotoSource, pickPhoto } from '@/core/photos';
import { data } from '@/data';
import { useMe } from '@/core/session';
import { font, radius, useColors } from '@/core/theme';
import type { PostVisibility } from '@/data/types';
import { displayName, errorMessage, initials, type LocalPhoto } from '@/data/types';
import { useCreatePost, useMemberCommunities } from '@/features/queries';
import { MAX_POST_LENGTH, validatePost } from '@/features/rules';

const VISIBILITIES: { value: PostVisibility; label: string; description: string }[] = [
  { value: 'Public', label: 'Public', description: 'Visible par tous les membres de BéninConnect' },
  { value: 'CommunityMembers', label: 'Membres de la communauté', description: 'Visible uniquement dans la communauté choisie' },
  { value: 'OnlyMe', label: 'Moi uniquement', description: 'Brouillon visible seulement par vous' },
];

export default function CreatePost() {
  const c = useColors();
  const me = useMe();
  const params = useLocalSearchParams<{ communityId?: string; image?: string }>();
  const mine = useMemberCommunities(me.id).data ?? [];
  const create = useCreatePost();
  const [content, setContent] = useState('');
  const [communityId, setCommunityId] = useState<string | null>(params.communityId ?? null);
  const [visibility, setVisibility] = useState<PostVisibility>('Public');
  // Image embarquée (chemin) ou photo de l'appareil, envoyée au moment de publier.
  const [image, setImage] = useState<string | LocalPhoto | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [uploading, setUploading] = useState(false);
  const preview = typeof image === 'string' || image === null ? image : image.uri;

  const addPhoto = async () => {
    const choice = await askPhotoSource('Ajouter une photo', { demo: 'Images de démonstration' });
    if (choice === 'demo') setPickerOpen(true);
    else if (choice === 'library' || choice === 'camera') {
      const photo = await pickPhoto(choice);
      if (photo) {
        setImage(photo);
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
  const dirty = !!content.trim() || !!image;

  // Confirmation avant d'abandonner un brouillon (bouton retour, geste, Annuler).
  usePreventRemove(dirty && !leaving, ({ repeat }) => {
    confirm('Abandonner la publication ?', 'Votre texte ne sera pas conservé.', 'Abandonner', true).then((ok) => {
      if (!ok) return;
      setLeaving(true);
      repeat();
    });
  });

  // Fermeture après publication, une fois la protection levée.
  useEffect(() => {
    if (leaving && create.isSuccess) router.back();
  }, [leaving, create.isSuccess]);

  const chooseCommunity = (id: string | null) => {
    setCommunityId(id);
    if (!id && visibility === 'CommunityMembers') setVisibility('Public');
  };

  const publish = async () => {
    const e = validatePost(content, visibility, communityId);
    setError(e);
    if (e) return;
    let imagePath: string | null = null;
    if (image) {
      setUploading(true);
      try {
        imagePath = typeof image === 'string' ? image : await data().uploadImage(image);
      } catch (err) {
        setError(`Envoi de la photo impossible : ${errorMessage(err)}`);
        return;
      } finally {
        setUploading(false);
      }
    }
    create.mutate({ content: content.trim(), communityId, imagePath, visibility }, {
      onSuccess: () => setLeaving(true),
      onError: (err) => setError(errorMessage(err)),
    });
  };

  return (
    <Screen>
      <FormScroll header={
        <Header title="Nouvelle publication" right={
          <Button small title="Publier" onPress={publish} loading={create.isPending || uploading} disabled={!content.trim()} style={{ marginRight: 8 }} />
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

        {preview && (
          <View style={styles.preview}>
            <Image source={imageSource(preview)} style={StyleSheet.absoluteFill} contentFit="cover" />
            <Pressable accessibilityRole="button" accessibilityLabel="Retirer l'image" onPress={() => setImage(null)} style={styles.remove}>
              <Icon name="close" size={18} color="#FFFFFF" />
            </Pressable>
          </View>
        )}

        <Button title={image ? 'Changer la photo' : 'Ajouter une photo'} icon="image-plus" kind="outline" onPress={addPhoto} />
        {pickerOpen && (
          <View style={{ marginTop: 12 }}>
            <Muted style={{ marginBottom: 8 }}>Images de démonstration</Muted>
            <ImageGrid paths={imagePaths('Posts')} selected={typeof image === 'string' ? image : null} onSelect={(p) => { setImage(p); setPickerOpen(false); }} />
          </View>
        )}

        <View style={{ height: 16 }} />
        <Select label="Communauté" value={communityId} onChange={chooseCommunity}
          options={[{ value: null, label: 'Aucune (fil public)' }, ...mine.map((x) => ({ value: x.id as string | null, label: x.name }))]} />
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
  remove: { position: 'absolute', top: 8, right: 8, width: 32, height: 32, borderRadius: 16, backgroundColor: 'rgba(0,0,0,0.55)', alignItems: 'center', justifyContent: 'center' },
});
