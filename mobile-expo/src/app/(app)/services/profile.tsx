import { router } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native';
import { CityPicker, initialsOf, Notice } from '@/components/services';
import { Avatar, Button, Checkbox, Header, Loading, Muted, Screen, Segmented, Select, SwitchRow, T, TextField } from '@/components/ui';
import { askPhotoSource, pickPhoto } from '@/core/photos';
import { useMe } from '@/core/session';
import { useColors } from '@/core/theme';
import { toast } from '@/core/toast';
import { data } from '@/data';
import { CITIES, findCity, type City } from '@/data/cities';
import { displayName, errorMessage, LEGAL_STATUS_LABELS, type ContactPreference, type LegalStatus, type MyProvider } from '@/data/types';
import { useMyProvider, useSaveMyProvider, useServicesInfo } from '@/features/queries';

const STEPS = ['Présentation', 'Zone', 'Contact et statut'];
const RADII = [5, 10, 15, 25, 50, 100];

/** Création (assistant en étapes) ou modification du profil prestataire. */
export default function ProviderProfileForm() {
  const query = useMyProvider();
  return (
    <Screen>
      {query.isPending ? <Loading /> : <Form existing={query.data ?? null} />}
    </Screen>
  );
}

function Form({ existing }: { existing: MyProvider | null }) {
  const c = useColors();
  const me = useMe();
  const info = useServicesInfo().data;
  const save = useSaveMyProvider();
  const [step, setStep] = useState(0);
  const [name, setName] = useState(existing?.displayName ?? displayName(me));
  const [bio, setBio] = useState(existing?.bio ?? '');
  const [photo, setPhoto] = useState<string | null>(existing?.photoPath ?? null);
  const [uploading, setUploading] = useState(false);
  const [city, setCity] = useState<City | null>(() => (existing ? findCity(existing.city, existing.country) : findCity(me.city)) ?? null);
  const [postalCode, setPostalCode] = useState(existing?.postalCode ?? '');
  const [radius, setRadius] = useState(existing?.serviceRadiusKm ?? 15);
  const [atHome, setAtHome] = useState(existing?.servesAtHome ?? true);
  const [premises, setPremises] = useState(existing?.hasPremises ?? false);
  const [contact, setContact] = useState<ContactPreference>(existing?.contactPreference ?? 'InApp');
  const [phone, setPhone] = useState(existing?.phone ?? '');
  const [phonePublic, setPhonePublic] = useState(existing?.phoneIsPublic ?? false);
  const [legal, setLegal] = useState<LegalStatus>(existing?.legalStatus ?? 'Individual');
  const [siret, setSiret] = useState(existing?.siret ?? '');
  const [ifu, setIfu] = useState(existing?.ifu ?? '');
  const [declared, setDeclared] = useState(existing?.qualificationDeclared ?? false);

  const choosePhoto = async () => {
    const source = await askPhotoSource('Photo de profil', { remove: !!photo });
    if (source === 'remove') return setPhoto(null);
    if (source !== 'library' && source !== 'camera') return;
    const picked = await pickPhoto(source, { aspect: [1, 1] });
    if (!picked) return;
    setUploading(true);
    try {
      setPhoto(await data().uploadServiceImage(picked));
    } catch (e) {
      toast(errorMessage(e), 'error');
    } finally {
      setUploading(false);
    }
  };

  const valid = [name.trim().length >= 2, !!city && (atHome || premises), contact === 'InApp' || phone.trim().length >= 8];
  const last = step === STEPS.length - 1;

  const submit = () => {
    if (!city) return;
    save.mutate({
      input: {
        displayName: name.trim(), bio: bio.trim() || null, photoPath: photo, phone: phone.trim() || null, phoneIsPublic: phonePublic,
        contactPreference: contact, legalStatus: legal, siret: siret.trim() || null, ifu: ifu.trim() || null, country: city.country, city: city.name,
        postalCode: postalCode.trim() || null, latitude: city.lat, longitude: city.lng, serviceRadiusKm: radius, servesAtHome: atHome, hasPremises: premises,
      },
      declared,
    }, {
      // Première fois : enchaîner sur la première offre
      onSuccess: (p) => (!existing && p.offerings.length === 0 ? router.replace({ pathname: '/services/offering', params: { first: '1' } }) : router.back()),
    });
  };

  return (
    <>
      <Header title={existing ? 'Mon profil prestataire' : 'Devenir prestataire'} subtitle={`Étape ${step + 1} sur ${STEPS.length} · ${STEPS[step]}`} />
      <View style={{ flexDirection: 'row', gap: 6, paddingHorizontal: 16, paddingTop: 8 }}>
        {STEPS.map((s, i) => <View key={s} style={{ flex: 1, height: 4, borderRadius: 2, backgroundColor: i <= step ? c.primary : c.border }} />)}
      </View>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
          {step === 0 && <>
            <View style={{ alignItems: 'center', marginBottom: 16, gap: 8 }}>
              <Avatar initials={initialsOf(name || '?')} color={c.primary} image={photo} size={96} onPress={choosePhoto} />
              <Button title={uploading ? 'Envoi…' : photo ? 'Changer la photo' : 'Ajouter une photo'} kind="secondary" small loading={uploading} onPress={choosePhoto} />
              <Muted style={{ textAlign: 'center' }}>Une photo de vous ou de votre travail. Sa localisation est retirée automatiquement.</Muted>
            </View>
            <TextField label="Nom affiché" value={name} onChangeText={setName} maxLength={80} hint="Votre nom ou celui de votre activité." />
            <TextField label="Présentation" value={bio} onChangeText={setBio} multiline maxLength={1500} counter={`${bio.length}/1500`}
              placeholder="Votre expérience, votre façon de travailler…" />
          </>}

          {step === 1 && <>
            <CityPicker value={city} onChange={setCity} label="Votre ville" />
            <Muted style={{ marginTop: -6, marginBottom: 12 }}>
              Seule la ville est affichée aux membres. {CITIES.length} villes disponibles : choisissez la plus proche.
            </Muted>
            <TextField label="Code postal (facultatif)" value={postalCode} onChangeText={setPostalCode} keyboardType="number-pad" maxLength={10} />
            <SwitchRow icon="car-outline" label="Je me déplace chez le client" value={atHome} onChange={setAtHome} />
            {atHome && <Select label="Distance maximale" value={String(radius)} onChange={(v) => setRadius(Number(v))}
              options={RADII.map((r) => ({ value: String(r), label: `${r} km` }))} />}
            <SwitchRow icon="storefront-outline" label="Je reçois chez moi ou dans mon local" value={premises} onChange={setPremises} />
          </>}

          {step === 2 && <>
            <T v="label" style={{ marginBottom: 6 }}>Comment préférez-vous être contacté ?</T>
            <Segmented value={contact} onChange={setContact}
              items={[{ key: 'InApp', label: 'Messagerie' }, { key: 'Phone', label: 'Téléphone' }, { key: 'WhatsApp', label: 'WhatsApp' }]} />
            <TextField label={contact === 'InApp' ? 'Téléphone (facultatif)' : 'Téléphone'} value={phone} onChangeText={setPhone} keyboardType="phone-pad"
              placeholder="+33 6… ou +229…" style={{ marginTop: 12 }} />
            {!!phone.trim() && <SwitchRow icon="eye-outline" label="Afficher mon numéro à tous"
              description="Sinon, il n’est donné qu’après acceptation d’une demande." value={phonePublic} onChange={setPhonePublic} />}

            <Select label="Statut" value={legal} onChange={setLegal}
              options={(Object.keys(LEGAL_STATUS_LABELS) as LegalStatus[]).map((k) => ({ value: k, label: LEGAL_STATUS_LABELS[k] }))} />
            {legal !== 'Individual' && city?.country !== 'BJ' && (
              <TextField label="SIRET (facultatif)" value={siret} onChangeText={setSiret} keyboardType="number-pad" maxLength={17}
                hint="Requis pour le badge « Pro vérifié »." />
            )}
            {legal !== 'Individual' && city?.country === 'BJ' && (
              <TextField label="IFU (facultatif)" value={ifu} onChangeText={setIfu} maxLength={20} hint="Requis pour le badge « Pro vérifié »." />
            )}
            <Checkbox checked={declared} onChange={setDeclared}
              label="Je déclare sur l’honneur disposer des diplômes ou autorisations exigés pour les métiers réglementés que je propose." />
            {info && <Notice icon="file-document-outline">{info.taxNotice} ({info.taxLinks.map((l) => l.label).join(', ')})</Notice>}
          </>}

          <View style={{ flexDirection: 'row', gap: 10, marginTop: 20 }}>
            {step > 0 && <Button title="Retour" kind="secondary" onPress={() => setStep(step - 1)} style={{ flex: 1 }} />}
            <Button title={last ? (existing ? 'Enregistrer' : 'Continuer vers mon offre') : 'Suivant'} icon={last ? 'check' : 'arrow-right'}
              disabled={!valid[step] || uploading} loading={save.isPending} style={{ flex: 1 }}
              onPress={() => (last ? submit() : setStep(step + 1))} />
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </>
  );
}
