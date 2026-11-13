import * as Linking from 'expo-linking';
import { useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { askJobReport, CompanyLogo, contractText } from '@/components/jobs';
import { Button, Header, Icon, type IconName, IconButton, Muted, QueryView, Screen, T, Tag } from '@/components/ui';
import * as fmt from '@/core/format';
import { useColors } from '@/core/theme';
import { toast } from '@/core/toast';
import { countryName, type JobOffer } from '@/data/types';
import { useJob, useReportJob, useToggleJobFavorite } from '@/features/queries';

/** Nom court de la source pour le bouton (« ReliefWeb · ONG » → « ReliefWeb »). */
const sourceName = (job: JobOffer) => job.sourceLabel.split(' · ')[0];

export default function JobDetails() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useJob(id);
  return <Screen><QueryView query={query}>{(job) => <Details job={job} />}</QueryView></Screen>;
}

function Details({ job }: { job: JobOffer }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const favorite = useToggleJobFavorite();
  const report = useReportJob();
  const expired = job.status === 'Expired';

  const apply = async () => {
    try {
      await Linking.openURL(job.applyUrl);
    } catch {
      toast('Impossible d’ouvrir le site de la source.', 'error');
    }
  };

  const Line = ({ icon, text }: { icon: IconName; text: string | null | undefined }) => !text ? null : (
    <View style={styles.line}>
      <View style={[styles.lineIcon, { backgroundColor: c.primaryLight }]}><Icon name={icon} size={18} color={c.primary} /></View>
      <T style={{ flex: 1 }}>{text}</T>
    </View>
  );

  return (
    <View style={{ flex: 1 }}>
      <Header title="Offre d’emploi" right={
        <View style={{ flexDirection: 'row' }}>
          <IconButton name={job.isFavorite ? 'bookmark' : 'bookmark-outline'} color={job.isFavorite ? c.primary : undefined}
            label={job.isFavorite ? 'Retirer des favoris' : 'Ajouter aux favoris'} onPress={() => favorite.mutate({ id: job.id, favorite: !job.isFavorite })} />
          {!job.reportedByMe && (
            <IconButton name="flag-outline" label="Signaler cette offre" onPress={() => askJobReport((reason) => report.mutate({ id: job.id, reason }))} />
          )}
        </View>
      } />
      <ScrollView contentContainerStyle={{ padding: 16, paddingBottom: 24 }}>
        <View style={{ flexDirection: 'row', gap: 12, alignItems: 'center' }}>
          <CompanyLogo job={job} size={56} />
          <View style={{ flex: 1 }}>
            {!!job.companyName && <T v="label" style={{ fontSize: 15 }}>{job.companyName}</T>}
            <Muted>Publiée {fmt.relative(job.publishedAt)}</Muted>
          </View>
        </View>
        <T v="h1" style={{ marginTop: 14 }}>{job.title}</T>
        <View style={styles.tags}>
          <Tag label={contractText(job)} />
          {job.isRemote && <Tag label="Télétravail" tone="neutral" />}
          {expired && <Tag label="Offre expirée" tone="warning" />}
        </View>

        <View style={{ marginTop: 12 }}>
          <Line icon="map-marker-outline" text={job.locationLabel || (job.country ? countryName(job.country) : null)} />
          <Line icon="clock-outline" text={job.workingTime} />
          <Line icon="cash" text={job.salaryLabel} />
          <Line icon="domain" text={job.sectorLabel} />
          <Line icon="calendar-end" text={job.closingAt ? `Candidatures jusqu’au ${fmt.longDate(job.closingAt)}` : null} />
        </View>

        <T v="h3" style={{ marginTop: 18, marginBottom: 6 }}>Description</T>
        <T>{job.description || 'Consultez le détail de l’offre sur le site de la source.'}</T>

        <View style={[styles.source, { backgroundColor: c.surfaceVariant }]}>
          <Icon name="information-outline" size={18} color={c.textSecondary} />
          <Muted style={{ flex: 1 }}>
            Offre publiée par {job.sourceLabel}. BéninConnect l’affiche telle quelle : la candidature se fait sur le site de la source.
            Ne payez jamais pour obtenir un emploi.
          </Muted>
        </View>
      </ScrollView>
      <View style={[styles.bar, { backgroundColor: c.surface, borderColor: c.border, paddingBottom: insets.bottom + 12 }]}>
        <Button title={expired ? 'Offre expirée' : `Postuler sur ${sourceName(job)}`} icon="open-in-new" disabled={expired} onPress={apply} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  tags: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10 },
  line: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 5 },
  lineIcon: { width: 34, height: 34, borderRadius: 10, alignItems: 'center', justifyContent: 'center' },
  source: { flexDirection: 'row', gap: 8, padding: 12, borderRadius: 12, marginTop: 20 },
  bar: { padding: 16, paddingTop: 12, borderTopWidth: StyleSheet.hairlineWidth },
});
