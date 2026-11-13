import * as DocumentPicker from 'expo-document-picker';
import * as Linking from 'expo-linking';
import { router } from 'expo-router';
import { Alert, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { initialsOf, Notice, Rating, VerificationTag } from '@/components/services';
import { Avatar, Button, Card, Header, Icon, IconButton, ListRow, Loading, Muted, Screen, SectionTitle, T, Tag } from '@/components/ui';
import { confirm } from '@/core/dialog';
import * as fmt from '@/core/format';
import { pickPhoto } from '@/core/photos';
import { useColors } from '@/core/theme';
import { DOCUMENT_KIND_LABELS, servicePrice, type DocumentKind, type MyProvider } from '@/data/types';
import {
  useDeleteOffering, useDeleteProviderDocument, useMyProvider, useServicesInfo, useSubmitMyProvider, useUploadProviderDocument,
} from '@/features/queries';

const STATUS: Record<MyProvider['status'], { label: string; tone: 'primary' | 'warning' | 'neutral'; text: string }> = {
  Draft: { label: 'Brouillon', tone: 'neutral', text: 'Complétez votre profil et ajoutez une offre, puis envoyez-le en vérification.' },
  PendingReview: { label: 'En vérification', tone: 'warning', text: 'Notre équipe vérifie votre profil. Vous serez prévenu dès sa mise en ligne.' },
  Active: { label: 'En ligne', tone: 'primary', text: 'Les membres peuvent vous trouver et vous envoyer des demandes.' },
  Suspended: { label: 'Suspendu', tone: 'warning', text: 'Votre profil n’est plus visible. Contactez l’équipe BéninConnect.' },
};

/** Espace prestataire : statut, profil, offres, justificatifs, demandes reçues. */
export default function ProviderSpace() {
  const query = useMyProvider();
  return (
    <Screen>
      <Header title="Mon espace prestataire" />
      {query.isPending ? <Loading /> : query.data ? <Space p={query.data} /> : <Intro />}
    </Screen>
  );
}

function TaxNotice() {
  const info = useServicesInfo().data;
  if (!info) return null;
  return (
    <Notice icon="file-document-outline">
      {info.taxNotice}{' '}
      {info.taxLinks.map((l, i) => (
        <Muted key={l.url} onPress={() => void Linking.openURL(l.url)}>{i ? ' · ' : ''}<Muted style={{ textDecorationLine: 'underline' }}>{l.label}</Muted></Muted>
      ))}
    </Notice>
  );
}

function Intro() {
  const c = useColors();
  const steps = ['Votre présentation et votre photo', 'Votre zone d’intervention', 'Votre statut (particulier ou professionnel)', 'Votre première offre', 'Vérification par l’équipe'];
  return (
    <ScrollView contentContainerStyle={{ padding: 16 }}>
      <T v="h2">Proposez vos services</T>
      <Muted style={{ marginTop: 4 }}>Coiffure, ménage, cours, traiteur, déménagement… Les membres vous trouvent et vous contactent. Pas besoin d’entreprise pour commencer.</Muted>
      <Card style={{ padding: 16, marginTop: 16, gap: 10 }}>
        {steps.map((s, i) => (
          <View key={s} style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
            <View style={[styles.step, { backgroundColor: c.primaryLight }]}><T v="label" color={c.primary}>{i + 1}</T></View>
            <T style={{ flex: 1 }}>{s}</T>
          </View>
        ))}
      </Card>
      <TaxNotice />
      <Notice icon="handshake-outline">BéninConnect met en relation : vous fixez vos prix et êtes payé directement par vos clients.</Notice>
      <Button title="Commencer" icon="arrow-right" style={{ marginTop: 16 }} onPress={() => router.push('/services/profile')} />
    </ScrollView>
  );
}

function askDocumentKind(): Promise<DocumentKind | null> {
  const kinds = Object.keys(DOCUMENT_KIND_LABELS) as DocumentKind[];
  if (Platform.OS === 'web') return Promise.resolve('Identity');
  return new Promise((resolve) => Alert.alert('Quel justificatif ?', undefined, [
    ...kinds.map((k) => ({ text: DOCUMENT_KIND_LABELS[k], onPress: () => resolve(k) })),
    { text: 'Annuler', style: 'cancel' as const, onPress: () => resolve(null) },
  ]));
}

function askFileSource(): Promise<'photo' | 'pdf' | null> {
  if (Platform.OS === 'web') return Promise.resolve('pdf');
  return new Promise((resolve) => Alert.alert('Envoyer', undefined, [
    { text: 'Photo du document', onPress: () => resolve('photo') },
    { text: 'Fichier PDF', onPress: () => resolve('pdf') },
    { text: 'Annuler', style: 'cancel', onPress: () => resolve(null) },
  ]));
}

function Space({ p }: { p: MyProvider }) {
  const c = useColors();
  const submit = useSubmitMyProvider();
  const removeOffering = useDeleteOffering();
  const upload = useUploadProviderDocument();
  const removeDocument = useDeleteProviderDocument();
  const status = STATUS[p.status];

  const addDocument = async () => {
    const kind = await askDocumentKind();
    if (!kind) return;
    const source = await askFileSource();
    if (source === 'photo') {
      const photo = await pickPhoto('library');
      if (photo) upload.mutate({ kind, file: { uri: photo.uri, name: photo.fileName ?? 'justificatif.jpg', mimeType: photo.mimeType ?? 'image/jpeg' } });
    } else if (source === 'pdf') {
      const result = await DocumentPicker.getDocumentAsync({ type: 'application/pdf', copyToCacheDirectory: true });
      const asset = result.canceled ? null : result.assets[0];
      if (asset) upload.mutate({ kind, file: { uri: asset.uri, name: asset.name, mimeType: asset.mimeType ?? 'application/pdf' } });
    }
  };

  return (
    <ScrollView contentContainerStyle={{ paddingBottom: 32 }}>
      <Card style={{ margin: 16, padding: 16 }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
          <Avatar initials={initialsOf(p.displayName)} color={c.primary} image={p.photoPath} size={56} />
          <View style={{ flex: 1 }}>
            <T v="label" style={{ fontSize: 17 }}>{p.displayName}</T>
            <Muted>{p.city} · rayon {p.serviceRadiusKm} km</Muted>
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 6 }}>
              <Tag label={status.label} tone={status.tone} />
              <VerificationTag level={p.verificationLevel} />
              {p.hiddenReason !== 'None' && <Tag label="Masqué, en revue" tone="warning" />}
            </View>
          </View>
          <IconButton name="pencil-outline" label="Modifier mon profil" color={c.primary} onPress={() => router.push('/services/profile')} />
        </View>
        <Muted style={{ marginTop: 10 }}>{status.text}</Muted>
        {!!p.reviewNote && (
          <View style={[styles.note, { backgroundColor: c.warningLight }]}>
            <Icon name="message-alert-outline" size={18} color={c.warning} />
            <T style={{ flex: 1 }}>Message de l’équipe : {p.reviewNote}</T>
          </View>
        )}
        {p.status === 'Active' && <View style={{ marginTop: 10 }}><Rating average={p.ratingAverage} count={p.ratingCount} /></View>}
        {p.status === 'Draft' && (
          <Button title="Envoyer en vérification" icon="send-check-outline" style={{ marginTop: 12 }} loading={submit.isPending}
            disabled={p.offerings.length === 0} onPress={() => submit.mutate()} />
        )}
      </Card>

      <Card style={{ marginHorizontal: 16, paddingHorizontal: 16, paddingVertical: 2 }}>
        <ListRow icon="inbox-arrow-down-outline" label="Demandes reçues" last={p.status !== 'Active'}
          onPress={() => router.push({ pathname: '/services/requests', params: { role: 'Provider' } })} />
        {p.status === 'Active' && <ListRow icon="eye-outline" label="Voir ma fiche publique" last onPress={() => router.push(`/services/provider/${p.id}`)} />}
      </Card>

      <SectionTitle title={`Mes offres (${p.offerings.length})`} action="Ajouter" onAction={() => router.push('/services/offering')} />
      {p.offerings.length === 0 && <Muted style={{ paddingHorizontal: 16 }}>Ajoutez au moins une offre pour être visible.</Muted>}
      {p.offerings.map((o) => (
        <Card key={o.id} style={styles.offering} onPress={() => router.push({ pathname: '/services/offering', params: { id: o.id } })}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
            <View style={{ flex: 1 }}>
              <T v="label">{o.title}</T>
              <Muted>{o.categoryName} · {servicePrice(o)} · {o.images.length} photo(s)</Muted>
            </View>
            {!o.isActive && <Tag label="Désactivée" tone="neutral" />}
            {o.hiddenReason !== 'None' && <Tag label="En vérification" tone="warning" />}
            <IconButton name="trash-can-outline" label="Supprimer l’offre" onPress={async () => {
              if (await confirm('Supprimer cette offre ?', o.title, 'Supprimer', true)) removeOffering.mutate(o.id);
            }} />
          </View>
        </Card>
      ))}

      <SectionTitle title="Vérification" action="Envoyer" onAction={addDocument} />
      <View style={{ paddingHorizontal: 16 }}>
        <Muted>
          Pièce d’identité : badge « Identité vérifiée ». Diplôme ou inscription au registre, avec votre SIRET ou IFU : badge « Pro vérifié ».
          Vos documents ne sont vus que par l’équipe.
        </Muted>
        {p.documents.map((d) => (
          <View key={d.id} style={[styles.document, { borderColor: c.border }]}>
            <Icon name="file-document-outline" color={c.textSecondary} />
            <View style={{ flex: 1 }}>
              <T lines={1}>{DOCUMENT_KIND_LABELS[d.kind]}</T>
              <Muted lines={1}>{d.originalName} · {fmt.relative(d.uploadedAt)}{d.reviewNote ? ` · ${d.reviewNote}` : ''}</Muted>
            </View>
            <Tag label={{ Pending: 'À vérifier', Approved: 'Validé', Rejected: 'Refusé' }[d.status]}
              tone={d.status === 'Approved' ? 'primary' : d.status === 'Pending' ? 'warning' : 'neutral'} />
            {d.status !== 'Approved' && <IconButton name="close" size={18} label="Retirer" onPress={() => removeDocument.mutate(d.id)} />}
          </View>
        ))}
        {upload.isPending && <Loading label="Envoi…" />}
        <TaxNotice />
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  step: { width: 28, height: 28, borderRadius: 14, alignItems: 'center', justifyContent: 'center' },
  note: { flexDirection: 'row', gap: 8, padding: 10, borderRadius: 10, marginTop: 10 },
  offering: { marginHorizontal: 16, marginBottom: 10, padding: 12 },
  document: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingVertical: 10, borderBottomWidth: StyleSheet.hairlineWidth },
});
