import { router } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { ImageGrid } from '@/components/cards';
import { FormError, FormScroll } from '@/components/Form';
import { Avatar, Button, Chip, Cover, Header, Icon, Muted, Screen, T, TextField } from '@/components/ui';
import { imagePaths } from '@/core/images';
import { askPhotoSource, pickPhoto } from '@/core/photos';
import { useMe } from '@/core/session';
import { depth, useColors } from '@/core/theme';
import { data } from '@/data';
import { toast } from '@/core/toast';
import * as v from '@/core/validation';
import { INTERESTS } from '@/data/repositories';
import { errorMessage, initials, type LocalPhoto } from '@/data/types';
import { useUpdateProfile } from '@/features/queries';
import { MAX_BIO, MAX_INTERESTS } from '@/features/rules';

export default function EditProfile() {
  const c = useColors();
  const me = useMe();
  const update = useUpdateProfile();
  const [form, setForm] = useState({
    firstName: me.firstName, lastName: me.lastName, profession: me.profession, country: me.country, city: me.city, bio: me.bio,
  });
  const [interests, setInterests] = useState<string[]>(me.interests);
  const [errors, setErrors] = useState<Record<string, string | undefined>>({});
  const [formError, setFormError] = useState<string | null>(null);
  // Chemin enregistré, fichier local à envoyer, ou null (retirée).
  const [avatar, setAvatar] = useState<string | LocalPhoto | null>(me.avatarImage ?? null);
  const [cover, setCover] = useState<string | LocalPhoto | null>(me.coverImage);
  const [coverGrid, setCoverGrid] = useState(false);
  const [uploading, setUploading] = useState(false);
  const shown = (x: string | LocalPhoto | null) => (typeof x === 'string' || x === null ? x : x.uri);

  const changeAvatar = async () => {
    const choice = await askPhotoSource('Photo de profil', { remove: !!avatar });
    if (choice === 'remove') setAvatar(null);
    else if (choice === 'library' || choice === 'camera') {
      const photo = await pickPhoto(choice, { aspect: [1, 1] });
      if (photo) setAvatar(photo);
    }
  };

  const changeCover = async () => {
    const choice = await askPhotoSource('Photo de couverture', { remove: !!cover, demo: 'Couvertures BéninConnect' });
    if (choice === 'remove') setCover(null);
    else if (choice === 'demo') setCoverGrid(true);
    else if (choice === 'library' || choice === 'camera') {
      const photo = await pickPhoto(choice, { aspect: [16, 9] });
      if (photo) setCover(photo);
    }
  };

  // Envoie la photo si elle vient de l'appareil ; '' = retirée.
  const resolve = async (x: string | LocalPhoto | null) => (x === null ? '' : typeof x === 'string' ? x : data().uploadImage(x));
  const set = (k: keyof typeof form) => (value: string) => setForm((f) => ({ ...f, [k]: value }));

  const toggle = (interest: string) => {
    if (interests.includes(interest)) {
      setInterests(interests.filter((x) => x !== interest));
      setErrors((e) => ({ ...e, interests: undefined }));
    } else if (interests.length >= MAX_INTERESTS) {
      setErrors((e) => ({ ...e, interests: `Maximum ${MAX_INTERESTS} centres d'intérêt.` }));
    } else {
      setInterests([...interests, interest]);
    }
  };

  const save = async () => {
    setFormError(null);
    const e = v.collect({
      firstName: v.length(form.firstName, 'Le prénom', 2, 40),
      lastName: v.length(form.lastName, 'Le nom', 2, 40),
      country: v.required(form.country, 'Le pays'),
      city: v.required(form.city, 'La ville'),
      bio: form.bio.length > MAX_BIO ? `La biographie ne doit pas dépasser ${MAX_BIO} caractères.` : null,
    });
    setErrors(e);
    if (Object.keys(e).length) return;
    let avatarImage: string;
    let coverImage: string;
    setUploading(true);
    try {
      [avatarImage, coverImage] = await Promise.all([resolve(avatar), resolve(cover)]);
    } catch (err) {
      setFormError(`Envoi de la photo impossible : ${errorMessage(err)}`);
      return;
    } finally {
      setUploading(false);
    }
    update.mutate({ ...form, interests, avatarImage, coverImage }, {
      onSuccess: () => {
        toast('Profil mis à jour.', 'success');
        router.back();
      },
      onError: (err) => setFormError(errorMessage(err)),
    });
  };

  return (
    <Screen>
      <FormScroll header={<Header title="Modifier le profil" right={<Button small title="Enregistrer" onPress={save} loading={update.isPending || uploading} style={{ marginRight: 8 }} />} />}>
        <View style={[styles.coverShell, depth(c, 2)]}>
          <Cover path={shown(cover)} height={140} style={styles.cover}>
            <Pressable accessibilityRole="button" accessibilityLabel="Changer la photo de couverture" onPress={changeCover} style={styles.coverButton}>
              <Icon name="camera-outline" size={18} color="#FFFFFF" />
              <T v="label" color="#FFFFFF">Couverture</T>
            </Pressable>
          </Cover>
        </View>
        {coverGrid && (
          <View style={{ marginTop: 10 }}>
            <ImageGrid paths={imagePaths('Profile')} selected={typeof cover === 'string' ? cover : null} columns={3}
              onSelect={(p) => { setCover(p); setCoverGrid(false); }} />
          </View>
        )}
        <View pointerEvents="box-none" style={{ alignItems: 'center', marginTop: -46, marginBottom: 16 }}>
          <Pressable accessibilityRole="button" accessibilityLabel="Changer la photo de profil" onPress={changeAvatar}>
            <View style={[styles.avatarRing, { backgroundColor: c.surface }, depth(c, 2)]}>
              <Avatar initials={initials({ firstName: form.firstName || me.firstName, lastName: form.lastName || me.lastName })}
                color={me.avatarColor} size={92} image={shown(avatar)} />
            </View>
            <View style={[styles.camera, { backgroundColor: c.primary, borderColor: c.surface }]}>
              <Icon name="camera" size={16} color="#FFFFFF" />
            </View>
          </Pressable>
          <Muted style={{ marginTop: 8 }}>{avatar ? 'Touchez la photo pour la changer' : 'Ajoutez une photo (sinon vos initiales)'}</Muted>
        </View>
        <FormError message={formError} />
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <TextField style={{ flex: 1 }} label="Prénom" value={form.firstName} onChangeText={set('firstName')} error={errors.firstName} />
          <TextField style={{ flex: 1 }} label="Nom" value={form.lastName} onChangeText={set('lastName')} error={errors.lastName} />
        </View>
        <TextField label="Profession" value={form.profession} onChangeText={set('profession')} placeholder="Ingénieure logiciel" />
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <TextField style={{ flex: 1 }} label="Pays" value={form.country} onChangeText={set('country')} error={errors.country} />
          <TextField style={{ flex: 1 }} label="Ville" value={form.city} onChangeText={set('city')} error={errors.city} />
        </View>
        <TextField label="Biographie" value={form.bio} onChangeText={set('bio')} error={errors.bio} multiline
          counter={`${form.bio.length} / ${MAX_BIO}`} placeholder="Parlez de vous, de votre parcours…" />
        <T v="label" style={{ marginBottom: 4 }}>{`Centres d'intérêt (${interests.length}/${MAX_INTERESTS})`}</T>
        {errors.interests ? <T v="caption" color={c.error}>{errors.interests}</T> : <Muted>Choisissez jusqu'à 6 thèmes.</Muted>}
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 10 }}>
          {INTERESTS.map((i) => <Chip key={i} label={i} selected={interests.includes(i)} onPress={() => toggle(i)} />)}
        </View>
        <Button title="Enregistrer" onPress={save} loading={update.isPending || uploading} style={{ marginTop: 24 }} />
      </FormScroll>
    </Screen>
  );
}

const styles = StyleSheet.create({
  coverShell: { borderRadius: 20 },
  cover: { borderRadius: 20 },
  coverButton: {
    position: 'absolute', right: 10, top: 10, flexDirection: 'row', alignItems: 'center', gap: 6,
    paddingHorizontal: 12, height: 34, borderRadius: 17, backgroundColor: 'rgba(0,0,0,0.45)',
  },
  avatarRing: { padding: 4, borderRadius: 50 },
  camera: {
    position: 'absolute', right: 0, bottom: 2, width: 32, height: 32, borderRadius: 16, borderWidth: 2,
    alignItems: 'center', justifyContent: 'center',
  },
});
