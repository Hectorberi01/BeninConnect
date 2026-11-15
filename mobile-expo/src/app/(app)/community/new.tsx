import { router } from 'expo-router';
import { useState } from 'react';
import { Image } from 'expo-image';
import { ActivityIndicator, Pressable, StyleSheet, View } from 'react-native';
import { ImageGrid } from '@/components/cards';
import { FormError, FormScroll } from '@/components/Form';
import { Button, Header, Icon, Muted, Screen, Segmented, Select, T, TextField } from '@/components/ui';
import { CityField, CountryField } from '@/components/places';
import { imagePaths, imageSource } from '@/core/images';
import { askPhotoSource, pickPhoto } from '@/core/photos';
import { toast } from '@/core/toast';
import { data } from '@/data';
import { useMe } from '@/core/session';
import * as v from '@/core/validation';
import type { CommunityVisibility } from '@/data/types';
import { errorMessage } from '@/data/types';
import { useCommunityCategories, useCreateCommunity } from '@/features/queries';

export default function CreateCommunity() {
  const me = useMe();
  const categories = useCommunityCategories().data ?? [];
  const create = useCreateCommunity();
  const covers = imagePaths('Covers');
  const [form, setForm] = useState({ name: '', description: '', country: me.country, city: me.city });
  const [category, setCategory] = useState<string | null>(null);
  const [visibility, setVisibility] = useState<CommunityVisibility>('Public');
  const [cover, setCover] = useState<string | null>(covers[0] ?? null);
  const [uploading, setUploading] = useState(false);
  const [errors, setErrors] = useState<Record<string, string | undefined>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const set = (k: keyof typeof form) => (value: string) => setForm((f) => ({ ...f, [k]: value }));

  // Photo du téléphone, envoyée tout de suite (format 16:9 de la couverture)
  const importCover = async () => {
    const choice = await askPhotoSource('Image de couverture');
    if (choice !== 'library' && choice !== 'camera') return;
    const photo = await pickPhoto(choice, { aspect: [16, 9] });
    if (!photo) return;
    setUploading(true);
    try {
      setCover(await data().uploadImage(photo));
    } catch (e) {
      toast(`Envoi de la photo impossible : ${errorMessage(e)}`, 'error');
    } finally {
      setUploading(false);
    }
  };

  const submit = () => {
    setFormError(null);
    const e = v.collect({
      name: v.length(form.name, 'Le nom', 3, 60),
      description: v.length(form.description, 'La description', 10, 500),
      category: category ? null : 'Choisissez une catégorie.',
      country: v.required(form.country, 'Le pays'),
      city: v.required(form.city, 'La ville'),
    });
    setErrors(e);
    if (Object.keys(e).length) return;
    create.mutate({ ...form, name: form.name.trim(), description: form.description.trim(), category: category!, visibility, coverImage: cover }, {
      onSuccess: (created) => router.replace(`/community/${created.id}`),
      onError: (err) => setFormError(errorMessage(err)),
    });
  };

  return (
    <Screen>
      <FormScroll header={<Header title="Créer une communauté" />}>
        <FormError message={formError} />
        <TextField label="Nom de la communauté" value={form.name} onChangeText={set('name')} error={errors.name} placeholder="Béninois de Bordeaux" maxLength={60} />
        <TextField label="Description" value={form.description} onChangeText={set('description')} error={errors.description}
          multiline counter={`${form.description.length} / 500`} maxLength={500} placeholder="Présentez l'objectif de la communauté…" />
        <Select label="Catégorie" value={category} onChange={setCategory} error={errors.category}
          options={categories.map((x) => ({ value: x as string | null, label: x }))} />
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <CountryField style={{ flex: 1 }} label="Pays" value={form.country} onChangeText={set('country')} error={errors.country} />
          <CityField style={{ flex: 1 }} label="Ville" value={form.city} onChangeText={set('city')} error={errors.city} />
        </View>
        <T v="label" style={{ marginBottom: 6 }}>Confidentialité</T>
        <View style={{ marginHorizontal: -16 }}>
          <Segmented<CommunityVisibility> value={visibility} onChange={setVisibility}
            items={[{ key: 'Public', label: 'Publique' }, { key: 'Private', label: 'Privée' }]} />
        </View>
        <Muted style={{ marginTop: 6, marginBottom: 16 }}>
          {visibility === 'Public'
            ? 'Tout le monde peut voir et rejoindre la communauté.'
            : "Les membres doivent être acceptés ; le contenu n'est visible que par eux."}
        </Muted>
        <T v="label" style={{ marginBottom: 8 }}>Image de couverture</T>
        <Pressable accessibilityRole="button" accessibilityLabel="Importer une photo de couverture" onPress={importCover} disabled={uploading} style={styles.preview}>
          {cover && <Image source={imageSource(cover)} style={StyleSheet.absoluteFill} contentFit="cover" transition={150} />}
          <View style={styles.import}>
            {uploading ? <ActivityIndicator color="#FFFFFF" size="small" /> : <Icon name="image-plus" size={18} color="#FFFFFF" />}
            <T v="label" color="#FFFFFF">{uploading ? 'Envoi…' : 'Importer une photo'}</T>
          </View>
        </Pressable>
        <Muted style={{ marginTop: 10, marginBottom: 6 }}>Ou choisissez une image proposée :</Muted>
        <ImageGrid paths={covers} selected={cover} onSelect={setCover} columns={3} />
        <Button title="Créer la communauté" onPress={submit} loading={create.isPending} disabled={uploading} style={{ marginTop: 20 }} />
      </FormScroll>
    </Screen>
  );
}

const styles = StyleSheet.create({
  preview: { width: '100%', aspectRatio: 16 / 9, borderRadius: 16, overflow: 'hidden', backgroundColor: '#1F2937', alignItems: 'center', justifyContent: 'center' },
  import: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 14, paddingVertical: 9, borderRadius: 20, backgroundColor: 'rgba(0,0,0,0.55)' },
});
