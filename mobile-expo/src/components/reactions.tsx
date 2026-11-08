import { useState } from 'react';
import { type GestureResponderEvent, Modal, Pressable, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { depth, font, glow, useColors } from '@/core/theme';
import { REACTIONS } from '@/data/repositories';
import type { Reaction } from '@/data/types';
import { groupReactions } from '@/data/types';
import { Glass } from './glass';
import { Icon, type IconName } from './ui';

/** Résumé des réactions sous un commentaire ou un message ; un appui bascule l'emoji. */
export function ReactionBar({ reactions, me, onToggle, align = 'flex-start' }: {
  reactions: Reaction[]; me: string; onToggle: (emoji: string) => void; align?: 'flex-start' | 'flex-end';
}) {
  const c = useColors();
  const groups = groupReactions(reactions, me);
  if (groups.length === 0) return null;
  return (
    <View style={[styles.bar, { justifyContent: align }]}>
      {groups.map((g) => (
        <Pressable key={g.emoji} accessibilityRole="button" accessibilityState={{ selected: g.mine }}
          accessibilityLabel={`${g.emoji} ${g.count} réaction${g.count > 1 ? 's' : ''}${g.mine ? ', dont la vôtre' : ''}`}
          onPress={() => onToggle(g.emoji)}
          style={({ pressed }) => [styles.chip, depth(c, 1), {
            backgroundColor: g.mine ? c.primaryLight : c.glassStrong,
            borderColor: g.mine ? c.primary : c.glassBorder,
            transform: [{ scale: pressed ? 0.92 : 1 }],
          }]}>
          <Text style={styles.chipEmoji}>{g.emoji}</Text>
          {g.count > 1 && <Text style={[styles.count, { color: g.mine ? c.primaryDark : c.textSecondary }]}>{g.count}</Text>}
        </Pressable>
      ))}
    </View>
  );
}

/** Action affichée sous la barre d'emojis (ex. Modifier, Supprimer pour ses propres messages). */
export type PickerAction = { label: string; icon: IconName; destructive?: boolean; onPress: () => void };

type PickerState = { y: number; current: string | null; onPick: (emoji: string) => void; actions: PickerAction[] } | null;

/** Barre d'emojis flottante, ouverte par un appui long à hauteur du doigt. */
export function useReactionPicker() {
  const c = useColors();
  const { height } = useWindowDimensions();
  const [state, setState] = useState<PickerState>(null);

  const open = (e: GestureResponderEvent | null, current: string | null, onPick: (emoji: string) => void, actions: PickerAction[] = []) =>
    setState({ y: e?.nativeEvent.pageY ?? height / 2, current, onPick, actions });
  const close = () => setState(null);

  const extra = (state?.actions.length ?? 0) * 48;
  const top = state ? Math.min(Math.max(state.y - 84, 60), height - 140 - extra) : 0;
  const element = (
    <Modal visible={!!state} transparent animationType="fade" onRequestClose={close}>
      <Pressable style={[StyleSheet.absoluteFill, { backgroundColor: 'rgba(0,0,0,0.12)' }]} onPress={close} accessibilityLabel="Fermer" />
      <View style={[styles.pickerWrap, { top }]} pointerEvents="box-none">
        <View style={[styles.pickerShadow, depth(c, 3)]}>
          <Glass radius={32} intensity={60} strong style={styles.picker}>
            {REACTIONS.map((emoji) => {
              const active = state?.current === emoji;
              return (
                <Pressable key={emoji} accessibilityRole="button" accessibilityLabel={`Réagir ${emoji}`} accessibilityState={{ selected: active }}
                  onPress={() => { state?.onPick(emoji); close(); }}
                  style={({ pressed }) => [styles.option, active && [{ backgroundColor: c.primaryLight }, glow(c.primaryGlow, 6)],
                    { transform: [{ scale: pressed ? 1.25 : 1 }] }]}>
                  <Text style={styles.optionEmoji}>{emoji}</Text>
                </Pressable>
              );
            })}
          </Glass>
        </View>
        {state?.current && <Text style={[styles.hint, { color: c.onPrimary }]}>Touchez votre réaction pour la retirer</Text>}
        {!!state?.actions.length && (
          <View style={[styles.actionsShadow, depth(c, 3)]}>
            <Glass radius={18} intensity={60} strong style={styles.actions}>
              {state.actions.map((a, i) => (
                <Pressable key={a.label} accessibilityRole="button" accessibilityLabel={a.label}
                  onPress={() => { close(); a.onPress(); }}
                  style={({ pressed }) => [styles.action, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderColor: c.border },
                    pressed && { backgroundColor: c.primaryLight }]}>
                  <Text style={[styles.actionLabel, { color: a.destructive ? c.error : c.text }]}>{a.label}</Text>
                  <Icon name={a.icon} size={20} color={a.destructive ? c.error : c.textSecondary} />
                </Pressable>
              ))}
            </Glass>
          </View>
        )}
      </View>
    </Modal>
  );
  return { open, element };
}

const styles = StyleSheet.create({
  bar: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 4 },
  chip: { flexDirection: 'row', alignItems: 'center', gap: 3, paddingHorizontal: 8, height: 26, borderRadius: 13, borderWidth: 1 },
  chipEmoji: { fontSize: 14 },
  count: { fontSize: 12, fontFamily: font.semibold },
  pickerWrap: { position: 'absolute', left: 0, right: 0, alignItems: 'center' },
  pickerShadow: { borderRadius: 32 },
  picker: { flexDirection: 'row', padding: 6, gap: 2 },
  option: { width: 50, height: 50, borderRadius: 25, alignItems: 'center', justifyContent: 'center' },
  optionEmoji: { fontSize: 28 },
  actionsShadow: { marginTop: 10, borderRadius: 18, minWidth: 220 },
  actions: { overflow: 'hidden' },
  action: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 16, paddingHorizontal: 16, height: 48 },
  actionLabel: { fontSize: 16, fontFamily: font.medium },
  hint: { marginTop: 8, fontSize: 12, fontFamily: font.medium, textShadowColor: 'rgba(0,0,0,0.5)', textShadowRadius: 4 },
});
