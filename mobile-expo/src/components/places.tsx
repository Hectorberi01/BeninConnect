import { useState } from 'react';
import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';
import { T, TextField } from '@/components/ui';
import { useColors } from '@/core/theme';
import { capitalizeFirst, type City, countryCode, countryName, suggestCities, suggestCountries } from '@/data/cities';

type FieldProps = {
  label: string; value: string; onChangeText: (text: string) => void;
  placeholder?: string; error?: string | null; hint?: string; style?: StyleProp<ViewStyle>;
};

/** Ville saisie librement (première lettre en majuscule), avec des suggestions. onPick : ville connue choisie. */
export function CityField({ country, onPick, ...props }: FieldProps & { country?: string | null; onPick?: (city: City) => void }) {
  const [focused, setFocused] = useState(false);
  const items = focused ? suggestCities(props.value, country) : [];
  return (
    <PlaceInput {...props} focused={focused} setFocused={setFocused} textContentType="addressCity"
      suggestions={items.map((c) => ({ key: `${c.country}:${c.name}`, label: country ? c.name : `${c.name} (${countryName(c.country)})`,
        pick: () => { props.onChangeText(c.name); onPick?.(c); } }))} />
  );
}

/** Pays saisi librement (première lettre en majuscule), avec des suggestions. */
export function CountryField(props: FieldProps) {
  const [focused, setFocused] = useState(false);
  const items = focused ? suggestCountries(props.value) : [];
  return (
    <PlaceInput {...props} focused={focused} setFocused={setFocused} textContentType="countryName"
      suggestions={items.map((n) => ({ key: n, label: n, pick: () => props.onChangeText(n) }))} />
  );
}

/** Erreur d'un pays obligatoire pour la recherche (code à 2 lettres côté serveur). */
export const countryError = (text: string) => (text.trim() && !countryCode(text) ? 'Pays non reconnu (ex. France, Bénin).' : null);

function PlaceInput({ label, value, onChangeText, placeholder, error, hint, style, suggestions, setFocused, textContentType }: FieldProps & {
  focused: boolean; setFocused: (f: boolean) => void; textContentType: 'addressCity' | 'countryName';
  suggestions: { key: string; label: string; pick: () => void }[];
}) {
  const c = useColors();
  return (
    <View style={style}>
      <TextField label={label} value={value} placeholder={placeholder} error={error} hint={hint}
        onChangeText={(t) => onChangeText(capitalizeFirst(t))} autoCapitalize="words" autoCorrect={false} textContentType={textContentType}
        onFocus={() => setFocused(true)} onBlur={() => setTimeout(() => setFocused(false), 150)} />
      {suggestions.length > 0 && (
        <View style={[styles.list, { backgroundColor: c.surface, borderColor: c.border }]}>
          {suggestions.map((s) => (
            <Pressable key={s.key} onPress={s.pick} accessibilityRole="button" style={({ pressed }) => [styles.item, pressed && { backgroundColor: c.surfaceVariant }]}>
              <T lines={1}>{s.label}</T>
            </Pressable>
          ))}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  list: { marginTop: -8, marginBottom: 12, borderRadius: 12, borderWidth: StyleSheet.hairlineWidth, overflow: 'hidden' },
  item: { paddingHorizontal: 14, paddingVertical: 10 },
});
