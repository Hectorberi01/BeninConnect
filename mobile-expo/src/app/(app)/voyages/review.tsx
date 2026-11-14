import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { Button, Header, Icon, Muted, Screen, T, TextField } from '@/components/ui';
import { VoyagesGate } from '@/components/VoyagesGate';
import { useColors } from '@/core/theme';
import { useReviewBooking } from '@/features/queries';

const LABELS = ['', 'Très décevant', 'Décevant', 'Correct', 'Bien', 'Excellent'];

/** Avis après une livraison confirmée par le code : un par côté. */
export default function ReviewBookingScreen() {
  return <VoyagesGate><ReviewBooking /></VoyagesGate>;
}

function ReviewBooking() {
  const c = useColors();
  const { id, name } = useLocalSearchParams<{ id: string; name?: string }>();
  const save = useReviewBooking();
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState('');
  return (
    <Screen>
      <Header title="Laisser un avis" />
      <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled" keyboardDismissMode="on-drag">
        {!!name && <T v="h3" style={{ marginBottom: 12, textAlign: 'center' }}>{name}</T>}
        <View style={{ flexDirection: 'row', justifyContent: 'center', gap: 6 }} accessibilityRole="adjustable" accessibilityLabel={`Note : ${rating} sur 5`}>
          {[1, 2, 3, 4, 5].map((i) => (
            <Pressable key={i} accessibilityRole="button" accessibilityLabel={`${i} étoile${i > 1 ? 's' : ''}`} onPress={() => setRating(i)} hitSlop={6}>
              <Icon name={rating >= i ? 'star' : 'star-outline'} size={40} color={c.warning} />
            </Pressable>
          ))}
        </View>
        <Muted style={{ textAlign: 'center', marginVertical: 8 }}>{rating ? LABELS[rating] : 'Touchez une étoile'}</Muted>
        <TextField label="Votre commentaire (facultatif)" value={comment} onChangeText={setComment} multiline maxLength={500}
          placeholder="Ponctualité, soin du colis, communication…" />
        <Button title="Publier l’avis" icon="star" loading={save.isPending} disabled={!rating}
          onPress={() => save.mutate({ id, rating, comment: comment.trim() || null }, { onSuccess: () => router.back() })} />
      </ScrollView>
    </Screen>
  );
}
