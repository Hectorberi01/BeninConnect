import { Alert, Platform } from 'react-native';
import { toast } from '@/core/toast';
import { data } from '@/data';
import { errorMessage, type ReportReason, type ReportTarget } from '@/data/types';

const REASONS: { reason: ReportReason; label: string }[] = [
  { reason: 'Inappropriate', label: 'Contenu inapproprié ou choquant' },
  { reason: 'Harassment', label: 'Harcèlement ou propos haineux' },
  { reason: 'Spam', label: 'Spam ou arnaque' },
  { reason: 'FalseInformation', label: 'Fausse information' },
  { reason: 'Other', label: 'Autre raison' },
];

const TITLES: Record<ReportTarget, string> = {
  Post: 'Signaler la publication',
  Comment: 'Signaler le commentaire',
  User: 'Signaler ce membre',
  Community: 'Signaler la communauté',
};

async function send(target: ReportTarget, id: string, reason: ReportReason) {
  try {
    await data().report(target, id, reason);
    toast('Merci. Notre équipe de modération va examiner ce signalement.', 'success');
  } catch (e) {
    toast(errorMessage(e), 'error');
  }
}

/** Choix du motif puis envoi à la modération. */
export function reportContent(target: ReportTarget, id: string) {
  if (Platform.OS === 'web') {
    if (typeof window !== 'undefined' && window.confirm(`${TITLES[target]} ?`)) void send(target, id, 'Inappropriate');
    return;
  }
  Alert.alert(TITLES[target], 'Pourquoi signalez-vous ce contenu ?', [
    ...REASONS.map(({ reason, label }) => ({ text: label, onPress: () => void send(target, id, reason) })),
    { text: 'Annuler', style: 'cancel' as const },
  ]);
}
