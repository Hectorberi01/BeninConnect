import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';
import { ImageGrid } from '@/components/cards';
import { FormError, FormScroll } from '@/components/Form';
import { Button, Header, Screen, Select, T, TextField } from '@/components/ui';
import { imagePaths } from '@/core/images';
import { useMe } from '@/core/session';
import { errorMessage } from '@/data/types';
import { useCreateEvent, useEventCategories, useMemberCommunities } from '@/features/queries';
import { type EventForm, formatDateInput, parseLocalDateTime, validateEvent } from '@/features/rules';

export default function CreateEvent() {
  const me = useMe();
  const params = useLocalSearchParams<{ communityId?: string }>();
  const categories = useEventCategories().data ?? [];
  const mine = useMemberCommunities(me.id).data ?? [];
  const create = useCreateEvent();
  const images = imagePaths('Events');
  const inAWeek = new Date();
  inAWeek.setDate(inAWeek.getDate() + 7);

  const [form, setForm] = useState<EventForm>({
    title: '', description: '', category: null, city: me.city, address: '',
    date: formatDateInput(inAWeek), time: '18:00', duration: '2', capacity: '',
  });
  const [country, setCountry] = useState(me.country);
  const [communityId, setCommunityId] = useState<string | null>(params.communityId ?? null);
  const [image, setImage] = useState<string | null>(images[0] ?? null);
  const [errors, setErrors] = useState<Partial<Record<keyof EventForm, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const set = (k: keyof EventForm) => (value: string) => setForm((f) => ({ ...f, [k]: value }));

  const submit = () => {
    const e = validateEvent(form);
    setErrors(e);
    if (Object.keys(e).length) {
      setFormError('Corrigez les champs signalés.');
      return;
    }
    setFormError(null);
    const start = parseLocalDateTime(form.date, form.time)!;
    const end = new Date(start.getTime() + +form.duration * 3_600_000);
    create.mutate({
      title: form.title.trim(), description: form.description.trim(), category: form.category!, city: form.city.trim(), country: country.trim(),
      address: form.address.trim(), startsAt: start.toISOString(), endsAt: end.toISOString(),
      capacity: form.capacity.trim() ? +form.capacity : null, communityId, imagePath: image,
    }, {
      onSuccess: (created) => router.replace(`/events/${created.id}`),
      onError: (err) => setFormError(errorMessage(err)),
    });
  };

  return (
    <Screen>
      <FormScroll header={<Header title="Créer un événement" />}>
        <FormError message={formError} />
        <TextField label="Titre" value={form.title} onChangeText={set('title')} error={errors.title} maxLength={80} placeholder="Afterwork de la diaspora" />
        <TextField label="Description" value={form.description} onChangeText={set('description')} error={errors.description}
          multiline maxLength={1000} counter={`${form.description.length} / 1000`} placeholder="Programme, public, informations pratiques…" />
        <Select label="Catégorie" value={form.category} onChange={(x) => setForm((f) => ({ ...f, category: x }))} error={errors.category}
          options={categories.map((x) => ({ value: x as string | null, label: x }))} />
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <TextField style={{ flex: 1 }} label="Ville" value={form.city} onChangeText={set('city')} error={errors.city} />
          <TextField style={{ flex: 1 }} label="Pays" value={country} onChangeText={setCountry} />
        </View>
        <TextField label="Adresse" value={form.address} onChangeText={set('address')} error={errors.address} placeholder="12 rue de la République" />
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <TextField style={{ flex: 3 }} label="Date" value={form.date} onChangeText={set('date')} error={errors.date}
            placeholder="JJ/MM/AAAA" keyboardType="numbers-and-punctuation" />
          <TextField style={{ flex: 2 }} label="Heure" value={form.time} onChangeText={set('time')} placeholder="HH:MM" keyboardType="numbers-and-punctuation" />
        </View>
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <TextField style={{ flex: 1 }} label="Durée (heures)" value={form.duration} onChangeText={set('duration')} error={errors.duration} keyboardType="number-pad" />
          <TextField style={{ flex: 1 }} label="Capacité" value={form.capacity} onChangeText={set('capacity')} error={errors.capacity}
            keyboardType="number-pad" placeholder="Illimitée" />
        </View>
        <Select label="Communauté organisatrice" value={communityId} onChange={setCommunityId}
          options={[{ value: null, label: 'Aucune' }, ...mine.map((x) => ({ value: x.id as string | null, label: x.name }))]} />
        <T v="label" style={{ marginBottom: 8, marginTop: 8 }}>Image</T>
        <ImageGrid paths={images} selected={image} onSelect={setImage} columns={5} />
        <Button title="Publier l'événement" onPress={submit} loading={create.isPending} style={{ marginTop: 20 }} />
      </FormScroll>
    </Screen>
  );
}
