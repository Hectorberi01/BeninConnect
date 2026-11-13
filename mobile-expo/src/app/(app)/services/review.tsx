import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { Notice } from '@/components/services';
import { Button, Header, Icon, Muted, Screen, T, TextField } from '@/components/ui';
import { useColors } from '@/core/theme';
import { useReviewService, useServicesInfo } from '@/features/queries';

const LABELS = ['', 'Très décevant', 'Décevant', 'Correct', 'Bien', 'Excellent'];

/** Avis vérifié : seulement après une prestation terminée, modifiable 7 jours. */
export default function ReviewService() {
  const c = useColors();
  const { requestId, reviewId, provider } = useLocalSearchParams<{ requestId?: string; reviewId?: string; provider?: string }>();
  const info = useServicesInfo().data;
  const save = useReviewService();
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState('');

  return (
    <Screen>
      <Header title={reviewId ? 'Modifier mon avis' : 'Laisser un avis'} />
      <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
        {!!provider && <T v="h3" style={{ marginBottom: 12 }}>{provider}</T>}
        <View style={{ flexDirection: 'row', justifyContent: 'center', gap: 6 }} accessibilityRole="adjustable" accessibilityLabel={`Note : ${rating} sur 5`}>
          {[1, 2, 3, 4, 5].map((i) => (
            <Pressable key={i} accessibilityRole="button" accessibilityLabel={`${i} étoile${i > 1 ? 's' : ''}`} onPress={() => setRating(i)} hitSlop={6}>
              <Icon name={rating >= i ? 'star' : 'star-outline'} size={40} color={c.warning} />
            </Pressable>
          ))}
        </View>
        <Muted style={{ textAlign: 'center', marginVertical: 8 }}>{rating ? LABELS[rating] : 'Touchez une étoile'}</Muted>
        <TextField label="Votre commentaire (facultatif)" value={comment} onChangeText={setComment} multiline maxLength={2000}
          placeholder="Ponctualité, qualité, accueil…" />
        {info && <Notice icon="check-decagram-outline">{info.reviews}</Notice>}
        <View style={{ height: 16 }} />
        <Button title="Publier l’avis" icon="star" loading={save.isPending} disabled={!rating}
          onPress={() => save.mutate({ requestId, reviewId, rating, comment: comment.trim() || null }, { onSuccess: () => router.back() })} />
      </ScrollView>
    </Screen>
  );
}
