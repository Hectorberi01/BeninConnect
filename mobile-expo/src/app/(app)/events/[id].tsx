import { router, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { participantsText, ParticipationButton } from '@/components/cards';
import { Avatar, Card, Cover, Icon, type IconName, IconButton, ListRow, Muted, QueryView, Screen, T, Tag } from '@/components/ui';
import * as fmt from '@/core/format';
import { useColors } from '@/core/theme';
import type { CommunityEvent } from '@/data/types';
import { displayName, initials, location } from '@/data/types';
import { useCommunity, useEvent, useUser } from '@/features/queries';

export default function EventDetails() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useEvent(id);
  return <Screen><QueryView query={query}>{(event) => <Details event={event} />}</QueryView></Screen>;
}

function Details({ event }: { event: CommunityEvent }) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const organizer = useUser(event.organizerId).data;
  const community = useCommunity(event.communityId ?? '').data;
  const hasCommunity = !!event.communityId && !!community;

  const Line = ({ icon, title, subtitle }: { icon: IconName; title: string; subtitle?: string }) => (
    <View style={styles.line}>
      <View style={[styles.lineIcon, { backgroundColor: c.primaryLight }]}><Icon name={icon} size={20} color={c.primary} /></View>
      <View style={{ flex: 1 }}>
        <T>{title}</T>
        {!!subtitle && <Muted>{subtitle}</Muted>}
      </View>
    </View>
  );

  return (
    <View style={{ flex: 1 }}>
      <ScrollView contentContainerStyle={{ paddingBottom: 24 }}>
        <Cover path={event.imagePath} height={220 + insets.top}>
          <View style={[styles.back, { top: insets.top + 4 }]}>
            <IconButton name="arrow-left" label="Retour" color="#FFFFFF" onPress={() => router.back()} />
          </View>
        </Cover>
        <View style={{ padding: 16, gap: 4 }}>
          <Tag label={event.category} />
          <T v="h1" style={{ marginTop: 8, marginBottom: 12 }}>{event.title}</T>
          <Line icon="calendar-clock" title={fmt.eventSchedule(event.startsAt, event.endsAt)} />
          <Line icon="map-marker-outline" title={event.address} subtitle={location(event)} />
          <Line icon="account-multiple-outline" title={participantsText(event)}
            subtitle={event.capacity != null ? `Capacité : ${event.capacity} places` : 'Entrée libre, sans limite de places'} />

          <T v="h3" style={{ marginTop: 16, marginBottom: 6 }}>À propos</T>
          <T>{event.description}</T>

          <T v="h3" style={{ marginTop: 20, marginBottom: 8 }}>Organisateur</T>
          {organizer && (
            <Card style={{ padding: 12 }} onPress={() => router.push(`/user/${organizer.id}`)}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
                <Avatar initials={initials(organizer)} color={organizer.avatarColor} image={organizer.avatarImage} />
                <View style={{ flex: 1 }}>
                  <T v="label" style={{ fontSize: 15 }}>{displayName(organizer)}</T>
                  <Muted>{organizer.profession || location(organizer)}</Muted>
                </View>
                <Icon name="chevron-right" color={c.textSecondary} />
              </View>
            </Card>
          )}
          {hasCommunity && (
            <ListRow icon="account-group-outline" label={`Organisé dans « ${community.name} »`} onPress={() => router.push(`/community/${community.id}`)} />
          )}
        </View>
      </ScrollView>
      <View style={[styles.bar, { backgroundColor: c.surface, borderColor: c.border, paddingBottom: insets.bottom + 12 }]}>
        <ParticipationButton event={event} full />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  back: { position: 'absolute', left: 8, borderRadius: 20, backgroundColor: 'rgba(0,0,0,0.35)' },
  line: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 6 },
  lineIcon: { width: 40, height: 40, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  bar: { padding: 16, paddingTop: 12, borderTopWidth: StyleSheet.hairlineWidth },
});
