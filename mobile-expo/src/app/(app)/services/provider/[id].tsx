import { Image } from 'expo-image';
import * as Linking from 'expo-linking';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { askServiceReport, durationText, initialsOf, Notice, Rating, Stars, VerificationTag } from '@/components/services';
import { Avatar, Button, Card, Header, Icon, type IconName, IconButton, Muted, QueryView, Screen, SectionTitle, T, Tag, TextField } from '@/components/ui';
import * as fmt from '@/core/format';
import { imageSource } from '@/core/images';
import { useColors } from '@/core/theme';
import { data } from '@/data';
import { LEGAL_STATUS_LABELS, servicePrice, type ProviderDetail, type ServiceReview } from '@/data/types';
import { useProvider, useReplyServiceReview, useReportService, useServicesInfo, useToggleProviderFavorite } from '@/features/queries';

export default function ProviderScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useProvider(id);
  return <Screen><QueryView query={query}>{(p) => <Details p={p} />}</QueryView></Screen>;
}

const responseText = (minutes: number | null) =>
  minutes == null ? null : minutes < 60 ? 'Répond en moins d’une heure' : minutes < 24 * 60 ? `Répond en ${Math.round(minutes / 60)} h environ` : 'Répond en quelques jours';

function Details({ p }: { p: ProviderDetail }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const favorite = useToggleProviderFavorite();
  const report = useReportService();
  const info = useServicesInfo().data;
  const [reviews, setReviews] = useState<ServiceReview[]>(p.reviews.items);
  const [cursor, setCursor] = useState(p.reviews.nextCursor);
  const first = p.offerings[0];

  const ask = (offeringId: string, title: string) =>
    router.push({ pathname: '/services/request', params: { offeringId, title, provider: p.displayName } });
  const more = async () => {
    const page = await data().getProviderReviews(p.id, cursor);
    setReviews((old) => [...old, ...page.items]);
    setCursor(page.nextCursor);
  };

  const Line = ({ icon, text }: { icon: IconName; text: string | null }) => !text ? null : (
    <View style={styles.line}><Icon name={icon} size={18} color={c.primary} /><T style={{ flex: 1 }}>{text}</T></View>
  );

  return (
    <View style={{ flex: 1 }}>
      <Header title="Prestataire" right={
        <View style={{ flexDirection: 'row' }}>
          <IconButton name={p.isFavorite ? 'heart' : 'heart-outline'} color={p.isFavorite ? c.error : undefined}
            label={p.isFavorite ? 'Retirer des favoris' : 'Ajouter aux favoris'} onPress={() => favorite.mutate({ id: p.id, favorite: !p.isFavorite })} />
          {!p.isMine && <IconButton name="flag-outline" label="Signaler ce profil"
            onPress={() => askServiceReport('Signaler ce profil', (reason) => report.mutate({ target: 'Profile', id: p.id, reason }))} />}
        </View>
      } />
      <ScrollView contentContainerStyle={{ paddingBottom: 24 }}>
        <View style={{ alignItems: 'center', padding: 16, gap: 6 }}>
          <Avatar initials={initialsOf(p.displayName)} color={c.primary} image={p.photoPath} size={88} />
          <T v="h2" style={{ textAlign: 'center' }}>{p.displayName}</T>
          <Rating average={p.ratingAverage} count={p.ratingCount} />
          <View style={styles.tags}>
            <VerificationTag level={p.verificationLevel} />
            <Tag label={LEGAL_STATUS_LABELS[p.legalStatus]} tone="neutral" />
          </View>
        </View>

        <View style={{ paddingHorizontal: 16 }}>
          <Line icon="map-marker-outline" text={`${p.city}${p.servesAtHome ? ` · se déplace jusqu’à ${p.serviceRadiusKm} km` : ''}${p.hasPremises ? ' · reçoit sur place' : ''}`} />
          <Line icon="message-reply-text-outline" text={responseText(p.medianResponseMinutes)} />
          <Line icon="phone-outline" text={p.phone} />
          <Line icon="calendar-check-outline" text={p.memberSince ? `Sur BéninConnect depuis ${fmt.monthYear(p.memberSince)}` : null} />
          {!!p.bio && <T style={{ marginTop: 10 }}>{p.bio}</T>}
          {p.hasRegulatedServices && info && <Notice icon="shield-alert-outline">{info.qualifications}</Notice>}
        </View>

        <SectionTitle title="Services proposés" />
        {p.offerings.map((o) => (
          <Card key={o.id} style={styles.offering}>
            <View style={{ flexDirection: 'row', alignItems: 'flex-start', gap: 8 }}>
              <View style={{ flex: 1 }}>
                <T v="label" style={{ fontSize: 16 }}>{o.title}</T>
                <Muted>{o.categoryName}{o.durationMinutes ? ` · ${durationText(o.durationMinutes)}` : ''}</Muted>
              </View>
              <T v="label" color={c.primary}>{servicePrice(o)}</T>
            </View>
            {!!o.description && <T style={{ marginTop: 6 }}>{o.description}</T>}
            {o.images.length > 0 && (
              <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8, marginTop: 10 }}>
                {o.images.map((path) => <Image key={path} source={imageSource(path)} style={styles.photo} contentFit="cover" accessibilityLabel="Réalisation" />)}
              </ScrollView>
            )}
            <View style={{ flexDirection: 'row', gap: 8, marginTop: 10 }}>
              {!p.isMine && <Button title="Demander un devis" small icon="email-fast-outline" onPress={() => ask(o.id, o.title)} style={{ flex: 1 }} />}
              {!p.isMine && <IconButton name="flag-outline" label="Signaler cette offre"
                onPress={() => askServiceReport('Signaler cette offre', (reason) => report.mutate({ target: 'Offering', id: o.id, reason }))} />}
            </View>
          </Card>
        ))}

        <SectionTitle title={`Avis vérifiés (${p.reviews.total})`} />
        <View style={{ paddingHorizontal: 16 }}>
          {info && <Muted style={{ marginBottom: 8 }}>{info.reviews}</Muted>}
          {reviews.length === 0 && <Muted>Pas encore d’avis.</Muted>}
          {reviews.map((r) => <ReviewItem key={r.id} r={r} canReply={p.isMine && !r.providerReply} />)}
          {cursor && <Button title="Voir plus d’avis" kind="secondary" small onPress={more} />}
        </View>
      </ScrollView>

      <View style={[styles.bar, { backgroundColor: c.surface, borderColor: c.border, paddingBottom: insets.bottom + 12 }]}>
        {p.isMine ? (
          <Button title="Gérer mon espace prestataire" icon="cog-outline" onPress={() => router.push('/services/me')} />
        ) : (
          <View style={{ flexDirection: 'row', gap: 10 }}>
            {p.phone && <Button title="Appeler" kind="secondary" icon="phone" onPress={() => void Linking.openURL(`tel:${p.phone!.replace(/[^+\d]/g, '')}`)} />}
            {first && <Button title="Demander un devis" icon="email-fast-outline" onPress={() => ask(first.id, first.title)} style={{ flex: 1 }} />}
          </View>
        )}
      </View>
    </View>
  );
}

function ReviewItem({ r, canReply }: { r: ServiceReview; canReply: boolean }) {
  const c = useColors();
  const reply = useReplyServiceReview();
  const report = useReportService();
  const [text, setText] = useState('');
  const [open, setOpen] = useState(false);
  return (
    <View style={[styles.review, { borderColor: c.border }]}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
        <Stars value={r.rating} />
        <T v="label" style={{ flex: 1 }}>{r.authorName}</T>
        <Tag label="Avis vérifié" tone="neutral" />
      </View>
      <Muted style={{ marginTop: 2 }}>{fmt.longDate(r.createdAt)}{r.updatedAt ? ' · modifié' : ''}</Muted>
      {!!r.comment && <T style={{ marginTop: 6 }}>{r.comment}</T>}
      {!!r.providerReply && (
        <View style={[styles.reply, { borderColor: c.primary }]}>
          <Muted>Réponse du prestataire</Muted>
          <T>{r.providerReply}</T>
        </View>
      )}
      <View style={{ flexDirection: 'row', justifyContent: 'flex-end', gap: 4 }}>
        {canReply && !open && <Button title="Répondre" kind="secondary" small onPress={() => setOpen(true)} />}
        <IconButton name="flag-outline" size={18} label="Signaler cet avis"
          onPress={() => askServiceReport('Signaler cet avis', (reason) => report.mutate({ target: 'Review', id: r.id, reason }))} />
      </View>
      {open && (
        <View style={{ marginTop: 6 }}>
          <TextField label="Votre réponse (une seule fois)" value={text} onChangeText={setText} multiline maxLength={1000} />
          <Button title="Publier la réponse" small loading={reply.isPending} disabled={text.trim().length < 2}
            onPress={() => reply.mutate({ id: r.id, reply: text.trim() }, { onSuccess: () => setOpen(false) })} />
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  tags: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'center', gap: 6 },
  line: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingVertical: 4 },
  offering: { marginHorizontal: 16, marginBottom: 12, padding: 14 },
  photo: { width: 110, height: 110, borderRadius: 10 },
  review: { paddingVertical: 12, borderBottomWidth: StyleSheet.hairlineWidth },
  reply: { marginTop: 8, paddingLeft: 10, borderLeftWidth: 2, gap: 2 },
  bar: { padding: 16, paddingTop: 12, borderTopWidth: StyleSheet.hairlineWidth },
});
