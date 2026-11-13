import { Image } from 'expo-image';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { Notice } from '@/components/services';
import { Button, Header, Icon, Loading, Muted, Screen, Segmented, Select, SwitchRow, T, TextField } from '@/components/ui';
import { confirm } from '@/core/dialog';
import { imageSource } from '@/core/images';
import { pickPhotos } from '@/core/photos';
import { useColors } from '@/core/theme';
import { toast } from '@/core/toast';
import { data } from '@/data';
import { errorMessage, type LocalPhoto, type MyProvider, type PriceType, type ServiceCategory, type ServiceOffering } from '@/data/types';
import { useMyProvider, useSaveOffering, useServiceCategories, useServicesInfo, useSubmitMyProvider } from '@/features/queries';

const MAX_IMAGES = 8;

/** Création ou modification d'une offre (portfolio de 8 photos au plus). */
export default function OfferingForm() {
  const { id, first } = useLocalSearchParams<{ id?: string; first?: string }>();
  const mine = useMyProvider();
  const categories = useServiceCategories();
  if (mine.isPending || categories.isPending) return <Screen><Loading /></Screen>;
  if (!mine.data) return <Screen><Header title="Offre" /><Muted style={{ padding: 16 }}>Créez d’abord votre profil prestataire.</Muted></Screen>;
  const existing = id ? mine.data.offerings.find((o) => o.id === id) ?? null : null;
  return <Screen><Form provider={mine.data} existing={existing} tree={categories.data ?? []} first={first === '1'} /></Screen>;
}

function Form({ provider, existing, tree, first }: { provider: MyProvider; existing: ServiceOffering | null; tree: ServiceCategory[]; first: boolean }) {
  const c = useColors();
  const info = useServicesInfo().data;
  const save = useSaveOffering();
  const submit = useSubmitMyProvider();
  const [categoryId, setCategoryId] = useState<string | null>(existing?.categoryId ?? null);
  const [title, setTitle] = useState(existing?.title ?? '');
  const [description, setDescription] = useState(existing?.description ?? '');
  const [priceType, setPriceType] = useState<PriceType>(existing?.priceType ?? 'From');
  const [price, setPrice] = useState(existing?.priceAmount != null ? String(existing.priceAmount) : '');
  const [duration, setDuration] = useState(existing?.durationMinutes != null ? String(existing.durationMinutes) : '');
  const [images, setImages] = useState<string[]>(existing?.images ?? []);
  const [active, setActive] = useState(existing?.isActive ?? true);
  const [uploading, setUploading] = useState(0);
  const [preparing, setPreparing] = useState(false);

  const leaves = tree.flatMap((p) => p.children.map((x) => ({ ...x, parent: p.name })));
  const category = leaves.find((x) => x.id === categoryId);
  const amount = Number(price.replace(',', '.'));
  const invalid = !categoryId || title.trim().length < 3 || (priceType !== 'Quote' && !(amount > 0)) || uploading > 0 || preparing;

  // Le sélecteur rend la main après avoir préparé toutes les photos (iCloud, compression) : indicateur affiché dès son ouverture
  const addPhotos = async () => {
    setPreparing(true);
    let photos: LocalPhoto[] = [];
    try {
      photos = await pickPhotos(MAX_IMAGES - images.length - uploading);
    } finally {
      setPreparing(false);
    }
    setUploading((n) => n + photos.length);
    const queue = [...photos];
    const worker = async () => {
      for (let photo = queue.shift(); photo; photo = queue.shift()) {
        try {
          const path = await data().uploadServiceImage(photo);
          setImages((old) => (old.length < MAX_IMAGES ? [...old, path] : old));
        } catch (e) {
          toast(errorMessage(e), 'error');
        } finally {
          setUploading((n) => n - 1);
        }
      }
    };
    await Promise.all([worker(), worker(), worker()]); // 3 envois à la fois
  };

  const onSave = () => save.mutate({
    id: existing?.id ?? null,
    input: {
      categoryId: categoryId!, title: title.trim(), description: description.trim() || null, priceType,
      priceAmount: priceType === 'Quote' ? null : amount, durationMinutes: duration ? Number(duration) : null, images, isActive: active,
    },
  }, {
    onSuccess: async () => {
      // Première offre d'un profil en brouillon : proposer l'envoi en vérification
      if (first && provider.status === 'Draft' && await confirm('Envoyer votre profil en vérification ?',
        'Notre équipe le vérifie avant sa mise en ligne. Vous pourrez encore le modifier.', 'Envoyer')) {
        submit.mutate(undefined, { onSettled: () => router.replace('/services/me') });
      } else {
        router.replace('/services/me');
      }
    },
  });

  return (
    <>
      <Header title={existing ? 'Modifier l’offre' : first ? 'Votre première offre' : 'Nouvelle offre'} />
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
          <Select label="Catégorie" value={categoryId} onChange={setCategoryId} placeholder="Choisir une catégorie"
            options={leaves.map((x) => ({ value: x.id as string | null, label: `${x.parent} · ${x.name}`, description: x.requiresQualification ? 'Métier réglementé' : undefined }))} />
          {category?.requiresQualification && info && (
            <Notice icon="shield-alert-outline">
              Métier réglementé : cochez la déclaration sur l’honneur dans votre profil. {info.qualifications}
            </Notice>
          )}
          <TextField label="Titre" value={title} onChangeText={setTitle} maxLength={100} placeholder="Ex. : Tresses africaines à domicile" style={{ marginTop: 12 }} />
          <TextField label="Description" value={description} onChangeText={setDescription} multiline maxLength={3000} counter={`${description.length}/3000`}
            placeholder="Ce qui est compris, le matériel, les délais…" />

          <T v="label" style={{ marginBottom: 6 }}>Prix</T>
          <Segmented value={priceType} onChange={setPriceType} items={[
            { key: 'From', label: 'À partir de' }, { key: 'Fixed', label: 'Fixe' }, { key: 'Hourly', label: 'À l’heure' }, { key: 'Quote', label: 'Sur devis' },
          ]} />
          {priceType !== 'Quote' && (
            <TextField label={`Montant (${provider.currency === 'XOF' ? 'FCFA' : '€'})`} value={price} onChangeText={setPrice} keyboardType="decimal-pad"
              style={{ marginTop: 12 }} />
          )}
          <TextField label="Durée habituelle en minutes (facultatif)" value={duration} onChangeText={(v) => setDuration(v.replace(/\D/g, ''))}
            keyboardType="number-pad" style={{ marginTop: priceType === 'Quote' ? 12 : 0 }} />

          <T v="label" style={{ marginBottom: 6 }}>Photos de vos réalisations ({images.length}/{MAX_IMAGES})</T>
          <View style={styles.photos}>
            {images.map((path) => (
              <View key={path} style={styles.photo}>
                <Image source={imageSource(path)} style={StyleSheet.absoluteFill} contentFit="cover" />
                <Pressable accessibilityRole="button" accessibilityLabel="Retirer la photo" onPress={() => setImages((old) => old.filter((p) => p !== path))}
                  style={styles.remove}><Icon name="close" size={16} color="#FFFFFF" /></Pressable>
              </View>
            ))}
            {Array.from({ length: uploading }, (_, i) => (
              <View key={`u${i}`} style={[styles.photo, styles.add, { backgroundColor: c.surfaceVariant }]}>
                <ActivityIndicator color={c.primary} />
                <Muted>Envoi…</Muted>
              </View>
            ))}
            {preparing && (
              <View style={[styles.photo, styles.add, { backgroundColor: c.surfaceVariant }]}>
                <ActivityIndicator color={c.primary} />
                <Muted style={{ textAlign: 'center' }}>Préparation…</Muted>
              </View>
            )}
            {!preparing && images.length + uploading < MAX_IMAGES && (
              <Pressable accessibilityRole="button" accessibilityLabel="Ajouter des photos" onPress={addPhotos}
                style={[styles.photo, styles.add, { borderColor: c.border }]}>
                <Icon name="image-plus" color={c.primary} />
                <Muted>Ajouter</Muted>
              </Pressable>
            )}
          </View>
          <Muted style={{ marginTop: 6 }}>
            5 Mo au plus par photo, localisation retirée automatiquement. Les photos stockées sur iCloud sont d’abord téléchargées : patientez quelques secondes.
          </Muted>

          {existing && <SwitchRow icon="eye-outline" label="Offre visible" description="Désactivez-la pour la mettre en pause." value={active} onChange={setActive} />}
          <View style={{ height: 16 }} />
          <Button title="Enregistrer l’offre" icon="check" loading={save.isPending || submit.isPending} disabled={invalid} onPress={onSave} />
        </ScrollView>
      </KeyboardAvoidingView>
    </>
  );
}

const styles = StyleSheet.create({
  photos: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  photo: { width: 96, height: 96, borderRadius: 12, overflow: 'hidden' },
  add: { borderWidth: 1, borderStyle: 'dashed', alignItems: 'center', justifyContent: 'center', gap: 4 },
  remove: { position: 'absolute', top: 4, right: 4, width: 24, height: 24, borderRadius: 12, backgroundColor: 'rgba(0,0,0,0.55)', alignItems: 'center', justifyContent: 'center' },
});
