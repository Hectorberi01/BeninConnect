import { Image } from 'expo-image';
import { router } from 'expo-router';
import { Alert, Platform, StyleSheet, View } from 'react-native';
import * as fmt from '@/core/format';
import { useColors } from '@/core/theme';
import { CONTRACT_LABELS, type JobOfferSummary } from '@/data/types';
import { useToggleJobFavorite } from '@/features/queries';
import { Card, Icon, IconButton, Muted, T, Tag } from './ui';

/** Libellé du contrat : celui de la source s'il est court, sinon le type normalisé. */
export const contractText = (job: Pick<JobOfferSummary, 'contractType' | 'contractLabel'>) =>
  job.contractType === 'Autre' && job.contractLabel && job.contractLabel.length <= 24 ? job.contractLabel : CONTRACT_LABELS[job.contractType];

export function CompanyLogo({ job, size = 44 }: { job: Pick<JobOfferSummary, 'companyLogoUrl' | 'companyName'>; size?: number }) {
  const c = useColors();
  return (
    <View style={[styles.logo, { width: size, height: size, borderRadius: size / 4, backgroundColor: c.primaryLight }]}>
      {job.companyLogoUrl
        ? <Image source={{ uri: job.companyLogoUrl }} style={StyleSheet.absoluteFill} contentFit="contain" accessibilityIgnoresInvertColors />
        : <Icon name="briefcase-outline" size={size / 2} color={c.primary} />}
    </View>
  );
}

export function JobCard({ job }: { job: JobOfferSummary }) {
  const c = useColors();
  const favorite = useToggleJobFavorite();
  return (
    <Card style={styles.card} onPress={() => router.push(`/jobs/${job.id}`)}>
      <View style={styles.row}>
        <CompanyLogo job={job} />
        <View style={{ flex: 1 }}>
          <T v="label" style={{ fontSize: 16 }} lines={2}>{job.title}</T>
          {!!job.companyName && <T lines={1} style={{ marginTop: 2 }}>{job.companyName}</T>}
          <Muted lines={1} style={{ marginTop: 2 }}>{job.locationLabel}</Muted>
        </View>
        <IconButton name={job.isFavorite ? 'bookmark' : 'bookmark-outline'} color={job.isFavorite ? c.primary : c.textSecondary}
          label={job.isFavorite ? 'Retirer des favoris' : 'Ajouter aux favoris'}
          onPress={() => favorite.mutate({ id: job.id, favorite: !job.isFavorite })} />
      </View>
      <View style={styles.tags}>
        <Tag label={contractText(job)} />
        {job.isRemote && <Tag label="Télétravail" tone="neutral" />}
        {job.status === 'Expired' && <Tag label="Expirée" tone="warning" />}
      </View>
      <View style={[styles.footer, { borderColor: c.border }]}>
        <Muted style={{ flex: 1 }} lines={1}>{job.sourceLabel}</Muted>
        <Muted>{fmt.relative(job.publishedAt)}</Muted>
      </View>
    </Card>
  );
}

/** Vignette horizontale (accueil). */
export function JobTile({ job }: { job: JobOfferSummary }) {
  return (
    <Card style={styles.tile} onPress={() => router.push(`/jobs/${job.id}`)}>
      <CompanyLogo job={job} size={36} />
      <T v="label" lines={2} style={{ marginTop: 8, minHeight: 40 }}>{job.title}</T>
      <Muted lines={1}>{job.companyName ?? job.locationLabel}</Muted>
      <View style={[styles.tags, { marginTop: 8 }]}><Tag label={contractText(job)} /></View>
    </Card>
  );
}

const REASONS = [
  'Arnaque ou demande d’argent',
  'Offre trompeuse ou fausse',
  'Contenu discriminatoire',
  'Lien cassé ou offre pourvue',
];

/** Choix du motif puis envoi (un signalement par membre et par offre). */
export function askJobReport(onReport: (reason: string) => void) {
  if (Platform.OS === 'web') {
    if (typeof window !== 'undefined' && window.confirm('Signaler cette offre ?')) onReport(REASONS[0]);
    return;
  }
  Alert.alert('Signaler cette offre', 'Que se passe-t-il avec cette offre ?', [
    ...REASONS.map((reason) => ({ text: reason, onPress: () => onReport(reason) })),
    { text: 'Annuler', style: 'cancel' as const },
  ]);
}

const styles = StyleSheet.create({
  card: { marginHorizontal: 16, marginBottom: 12, padding: 14 },
  row: { flexDirection: 'row', alignItems: 'flex-start', gap: 12 },
  logo: { alignItems: 'center', justifyContent: 'center', overflow: 'hidden' },
  tags: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10 },
  footer: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 10, paddingTop: 10, borderTopWidth: StyleSheet.hairlineWidth },
  tile: { width: 200, marginRight: 12, padding: 12 },
});
