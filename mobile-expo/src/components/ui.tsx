import { MaterialCommunityIcons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { router } from 'expo-router';
import { type ComponentProps, type ReactNode, useState } from 'react';
import {
  ActivityIndicator, Modal, Pressable, ScrollView, type StyleProp, StyleSheet, Switch, Text, TextInput, type TextInputProps,
  type TextStyle, View, type ViewStyle,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { depth, font, glow, radius, type, useColors } from '@/core/theme';
import { imageSource } from '@/core/images';
import { errorMessage } from '@/data/types';
import { Backdrop, Highlight } from './glass';

export type IconName = ComponentProps<typeof MaterialCommunityIcons>['name'];

export function Icon({ name, size = 22, color }: { name: IconName; size?: number; color?: string }) {
  const c = useColors();
  return <MaterialCommunityIcons name={name} size={size} color={color ?? c.text} />;
}

// --- Texte --------------------------------------------------------------------------------

type Variant = keyof typeof type;

export function T({ v = 'body', color, style, children, lines, onPress }: {
  v?: Variant; color?: string; style?: StyleProp<TextStyle>; children: ReactNode; lines?: number; onPress?: () => void;
}) {
  const c = useColors();
  return (
    <Text style={[type[v], { color: color ?? c.text }, style]} numberOfLines={lines} onPress={onPress}>
      {children}
    </Text>
  );
}

export const Muted = (p: ComponentProps<typeof T>) => {
  const c = useColors();
  return <T v="caption" {...p} color={p.color ?? c.textSecondary} />;
};

// --- Boutons ------------------------------------------------------------------------------

type ButtonKind = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';

export function Button({ title, onPress, kind = 'primary', icon, loading, disabled, small, style, testID }: {
  title: string; onPress?: () => void; kind?: ButtonKind; icon?: IconName; loading?: boolean; disabled?: boolean;
  small?: boolean; style?: StyleProp<ViewStyle>; testID?: string;
}) {
  const c = useColors();
  const off = disabled || loading;
  const solid = kind === 'primary';
  const fg = solid ? c.onPrimary : kind === 'danger' ? c.error : kind === 'secondary' ? c.primaryDark : c.primary;
  const bg = kind === 'secondary' ? c.primaryLight : kind === 'ghost' ? 'transparent' : c.glassStrong;
  const border = kind === 'outline' ? c.primary : kind === 'danger' ? c.error : kind === 'ghost' ? 'transparent' : c.glassBorder;
  const content = (
    <>
      {loading ? <ActivityIndicator color={fg} size="small" /> : icon ? <Icon name={icon} size={small ? 16 : 20} color={fg} /> : null}
      <Text style={[small ? type.label : styles.buttonText, { color: fg }]} numberOfLines={1}>{title}</Text>
    </>
  );
  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      accessibilityState={{ disabled: !!off, busy: !!loading }}
      onPress={off ? undefined : onPress}
      style={({ pressed }) => [
        styles.buttonShell,
        small && styles.buttonSmallShell,
        solid ? glow(c.primaryGlow, 12) : kind !== 'ghost' ? depth(c, 1) : null,
        { opacity: off ? 0.55 : 1, transform: [{ scale: pressed && !off ? 0.97 : 1 }] },
        style,
      ]}
    >
      {solid ? (
        <LinearGradient colors={[c.primaryTop, c.primary, c.primaryDark]} start={{ x: 0.2, y: 0 }} end={{ x: 0.8, y: 1 }}
          style={[styles.button, small && styles.buttonSmall]}>
          <Highlight height="50%" opacity={0.5} />
          {content}
        </LinearGradient>
      ) : (
        <View style={[styles.button, small && styles.buttonSmall, { backgroundColor: bg, borderColor: border, borderWidth: kind === 'ghost' ? 0 : 1.2 }]}>
          {kind !== 'ghost' && <Highlight height="50%" opacity={0.6} />}
          {content}
        </View>
      )}
    </Pressable>
  );
}

export function IconButton({ name, onPress, badge, color, label, size = 24 }: {
  name: IconName; onPress?: () => void; badge?: number; color?: string; label: string; size?: number;
}) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} hitSlop={8} onPress={onPress}
      style={({ pressed }) => [styles.iconButton, pressed && { opacity: 0.6 }]}>
      <Icon name={name} size={size} color={color} />
      {!!badge && badge > 0 && <Badge count={badge} style={styles.iconBadge} />}
    </Pressable>
  );
}

export function Badge({ count, style }: { count: number; style?: StyleProp<ViewStyle> }) {
  const c = useColors();
  return (
    <View style={[styles.badge, { backgroundColor: c.error, borderColor: c.surface }, glow(c.error, 6), style]}>
      <Text style={styles.badgeText}>{count > 99 ? '99+' : count}</Text>
    </View>
  );
}

// --- Champs -------------------------------------------------------------------------------

export function TextField({ label, error, hint, style, multiline, counter, icon, ...input }: TextInputProps & {
  label?: string; error?: string | null; hint?: string; counter?: string; icon?: IconName;
}) {
  const c = useColors();
  const [focused, setFocused] = useState(false);
  return (
    <View style={[styles.field, style as ViewStyle]}>
      {!!label && <T v="label" style={{ marginBottom: 6 }}>{label}</T>}
      <View style={[
        styles.inputBox,
        depth(c, 1),
        multiline && { minHeight: 110, alignItems: 'flex-start', paddingVertical: 10 },
        { backgroundColor: c.glassStrong, borderColor: error ? c.error : focused ? c.primary : c.glassBorder },
      ]}>
        {icon && <Icon name={icon} size={20} color={c.textSecondary} />}
        <TextInput
          placeholderTextColor={c.textSecondary}
          {...input}
          multiline={multiline}
          accessibilityLabel={input.accessibilityLabel ?? label}
          onFocus={(e) => { setFocused(true); input.onFocus?.(e); }}
          onBlur={(e) => { setFocused(false); input.onBlur?.(e); }}
          style={[styles.input, { color: c.text }, multiline && { textAlignVertical: 'top', minHeight: 90 }]}
        />
      </View>
      <View style={styles.fieldFooter}>
        {error ? <T v="caption" color={c.error} style={{ flex: 1 }}>{error}</T>
          : hint ? <Muted style={{ flex: 1 }}>{hint}</Muted> : <View style={{ flex: 1 }} />}
        {!!counter && <Muted>{counter}</Muted>}
      </View>
    </View>
  );
}

export function PasswordField(props: Omit<ComponentProps<typeof TextField>, 'secureTextEntry'>) {
  const c = useColors();
  const [visible, setVisible] = useState(false);
  return (
    <View>
      <TextField {...props} secureTextEntry={!visible} autoCapitalize="none" autoCorrect={false} />
      <Pressable accessibilityRole="button" accessibilityLabel={visible ? 'Masquer le mot de passe' : 'Afficher le mot de passe'}
        onPress={() => setVisible((v) => !v)} hitSlop={10}
        style={[styles.eye, { top: props.label ? 38 : 13 }]}>
        <Icon name={visible ? 'eye-off-outline' : 'eye-outline'} size={20} color={c.textSecondary} />
      </Pressable>
    </View>
  );
}

export function SearchBar({ value, onChangeText, placeholder, autoFocus }: {
  value: string; onChangeText: (s: string) => void; placeholder: string; autoFocus?: boolean;
}) {
  const c = useColors();
  return (
    <View style={[styles.search, { backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 1)]}>
      <Icon name="magnify" size={20} color={c.textSecondary} />
      <TextInput value={value} onChangeText={onChangeText} placeholder={placeholder} placeholderTextColor={c.textSecondary}
        autoFocus={autoFocus} autoCorrect={false} returnKeyType="search" accessibilityLabel={placeholder}
        style={[styles.input, { color: c.text }]} />
      {value.length > 0 && <IconButton name="close-circle" size={18} color={c.textSecondary} label="Effacer" onPress={() => onChangeText('')} />}
    </View>
  );
}

export function Select<T extends string | null>({ label, value, options, onChange, error, placeholder = 'Choisir…' }: {
  label: string; value: T; options: { value: T; label: string; description?: string }[]; onChange: (v: T) => void;
  error?: string | null; placeholder?: string;
}) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  const [open, setOpen] = useState(false);
  const current = options.find((o) => o.value === value);
  return (
    <View style={styles.field}>
      <T v="label" style={{ marginBottom: 6 }}>{label}</T>
      <Pressable accessibilityRole="button" accessibilityLabel={`${label} : ${current?.label ?? placeholder}`} onPress={() => setOpen(true)}
        style={[styles.inputBox, { backgroundColor: c.glassStrong, borderColor: error ? c.error : c.glassBorder }, depth(c, 1)]}>
        <T style={{ flex: 1 }} color={current ? c.text : c.textSecondary} lines={1}>{current?.label ?? placeholder}</T>
        <Icon name="chevron-down" color={c.textSecondary} />
      </Pressable>
      {!!error && <T v="caption" color={c.error} style={{ marginTop: 4 }}>{error}</T>}
      <Modal visible={open} transparent animationType="slide" onRequestClose={() => setOpen(false)}>
        <Pressable style={[styles.backdrop, { backgroundColor: c.overlay }]} onPress={() => setOpen(false)} />
        <View style={[styles.sheet, { backgroundColor: c.surface, paddingBottom: insets.bottom + 12 }]}>
          <T v="h3" style={{ marginBottom: 8 }}>{label}</T>
          <ScrollView style={{ maxHeight: 420 }}>
            {options.map((o) => (
              <Pressable key={String(o.value)} accessibilityRole="button" onPress={() => { onChange(o.value); setOpen(false); }}
                style={[styles.option, { borderColor: c.border }]}>
                <View style={{ flex: 1 }}>
                  <T style={o.value === value ? { fontFamily: font.semibold, color: c.primary } : undefined}>{o.label}</T>
                  {!!o.description && <Muted>{o.description}</Muted>}
                </View>
                {o.value === value && <Icon name="check" color={c.primary} />}
              </Pressable>
            ))}
          </ScrollView>
        </View>
      </Modal>
    </View>
  );
}

export function Checkbox({ checked, onChange, label, error }: { checked: boolean; onChange: (v: boolean) => void; label: string; error?: string | null }) {
  const c = useColors();
  return (
    <View style={{ marginBottom: 12 }}>
      <Pressable accessibilityRole="checkbox" accessibilityState={{ checked }} onPress={() => onChange(!checked)} style={styles.row}>
        <Icon name={checked ? 'checkbox-marked' : 'checkbox-blank-outline'} color={checked ? c.primary : c.textSecondary} />
        <T style={{ flex: 1, marginLeft: 8 }}>{label}</T>
      </Pressable>
      {!!error && <T v="caption" color={c.error} style={{ marginTop: 4 }}>{error}</T>}
    </View>
  );
}

export function SwitchRow({ label, description, value, onChange, icon }: {
  label: string; description?: string; value: boolean; onChange: (v: boolean) => void; icon?: IconName;
}) {
  const c = useColors();
  return (
    <View style={[styles.listRow, { borderColor: c.border }]}>
      {icon && <Icon name={icon} color={c.textSecondary} />}
      <View style={{ flex: 1 }}>
        <T>{label}</T>
        {!!description && <Muted>{description}</Muted>}
      </View>
      <Switch value={value} onValueChange={onChange} accessibilityLabel={label}
        trackColor={{ true: c.primary, false: c.border }} thumbColor="#FFFFFF" />
    </View>
  );
}

export function ListRow({ label, value, icon, onPress, danger }: { label: string; value?: string; icon?: IconName; onPress?: () => void; danger?: boolean }) {
  const c = useColors();
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={({ pressed }) => [styles.listRow, { borderColor: c.border }, pressed && { opacity: 0.6 }]}>
      {icon && <Icon name={icon} color={danger ? c.error : c.textSecondary} />}
      <T style={{ flex: 1 }} color={danger ? c.error : undefined}>{label}</T>
      {!!value && <Muted>{value}</Muted>}
      {!danger && <Icon name="chevron-right" color={c.textSecondary} />}
    </Pressable>
  );
}

// --- Puces --------------------------------------------------------------------------------

export function Chip({ label, selected, onPress, icon }: { label: string; selected?: boolean; onPress?: () => void; icon?: IconName }) {
  const c = useColors();
  const inner = (
    <>
      {icon && <Icon name={icon} size={16} color={selected ? c.onPrimary : c.textSecondary} />}
      <Text style={[type.label, { color: selected ? c.onPrimary : c.text }]}>{label}</Text>
    </>
  );
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ selected: !!selected }} onPress={onPress}
      style={({ pressed }) => [{ borderRadius: radius.pill, transform: [{ scale: pressed ? 0.96 : 1 }] }, selected ? glow(c.primaryGlow, 8) : depth(c, 1)]}>
      {selected ? (
        <LinearGradient colors={[c.primaryTop, c.primary]} style={[styles.chip, { borderColor: 'transparent' }]}>{inner}</LinearGradient>
      ) : (
        <View style={[styles.chip, { backgroundColor: c.glassStrong, borderColor: c.glassBorder }]}>{inner}</View>
      )}
    </Pressable>
  );
}

export function ChipRow({ items, selected, onSelect }: { items: { key: string; label: string }[]; selected: string; onSelect: (k: string) => void }) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.chipScroll} contentContainerStyle={styles.chipRow}>
      {items.map((i) => <Chip key={i.key} label={i.label} selected={i.key === selected} onPress={() => onSelect(i.key)} />)}
    </ScrollView>
  );
}

export function Tag({ label, tone = 'primary' }: { label: string; tone?: 'primary' | 'warning' | 'neutral' }) {
  const c = useColors();
  const bg = tone === 'warning' ? c.warningLight : tone === 'neutral' ? c.surfaceVariant : c.primaryLight;
  const fg = tone === 'warning' ? c.warning : tone === 'neutral' ? c.textSecondary : c.primaryDark;
  return (
    <View style={[styles.tag, { backgroundColor: bg }]}>
      <Text style={[type.label, { color: fg, fontSize: 12 }]}>{label}</Text>
    </View>
  );
}

// --- Conteneurs ---------------------------------------------------------------------------

export function Card({ children, style, onPress }: { children: ReactNode; style?: StyleProp<ViewStyle>; onPress?: () => void }) {
  const c = useColors();
  const flat = StyleSheet.flatten(style) ?? {};
  // Ombre sur l'enveloppe, coins arrondis et débordement sur l'intérieur (sinon l'ombre est coupée sur iOS).
  const { padding, paddingHorizontal, paddingVertical, paddingTop, paddingBottom, ...outer } = flat;
  const inner = [styles.card, { backgroundColor: c.glassStrong, borderColor: c.glassBorder, padding, paddingHorizontal, paddingVertical, paddingTop, paddingBottom }];
  const body = (
    <View style={inner}>
      <Highlight height={60} opacity={0.7} />
      {children}
    </View>
  );
  return onPress
    ? <Pressable onPress={onPress} style={({ pressed }) => [styles.cardShell, depth(c, 2), outer, pressed && { transform: [{ scale: 0.985 }] }]}>{body}</Pressable>
    : <View style={[styles.cardShell, depth(c, 2), outer]}>{body}</View>;
}

export function Avatar({ initials, color, size = 44, onPress, image }: {
  initials: string; color: string; size?: number; onPress?: () => void; image?: string | null;
}) {
  const c = useColors();
  const photo = imageSource(image);
  const body = (
    <View style={[{ width: size, height: size, borderRadius: size / 2 }, depth(c, 1)]}>
      <View style={[styles.avatar, { width: size, height: size, borderRadius: size / 2, backgroundColor: color }]}>
        {photo ? (
          <Image source={photo} style={StyleSheet.absoluteFill} contentFit="cover" transition={120} accessibilityIgnoresInvertColors />
        ) : (
          <Text style={{ color: '#FFFFFF', fontFamily: font.semibold, fontSize: size * 0.38 }}>{initials}</Text>
        )}
        <LinearGradient colors={photo ? ['rgba(255,255,255,0.25)', 'rgba(255,255,255,0)', 'rgba(0,0,0,0.08)'] : ['rgba(255,255,255,0.45)', 'rgba(255,255,255,0)', 'rgba(0,0,0,0.18)']}
          style={StyleSheet.absoluteFill} pointerEvents="none" />
      </View>
    </View>
  );
  return onPress ? <Pressable accessibilityRole="button" accessibilityLabel={`Profil ${initials}`} onPress={onPress}>{body}</Pressable> : body;
}

export function Cover({ path, height = 160, style, children }: { path: string | null | undefined; height?: number; style?: StyleProp<ViewStyle>; children?: ReactNode }) {
  const c = useColors();
  const source = imageSource(path);
  return (
    <View style={[{ height, backgroundColor: c.primaryLight, overflow: 'hidden' }, style]}>
      {source && <Image source={source} style={StyleSheet.absoluteFill} contentFit="cover" transition={150} accessibilityIgnoresInvertColors />}
      {children}
    </View>
  );
}

export function SectionTitle({ title, action, onAction }: { title: string; action?: string; onAction?: () => void }) {
  const c = useColors();
  return (
    <View style={styles.sectionTitle}>
      <T v="h3" style={{ flex: 1 }}>{title}</T>
      {!!action && <T v="label" color={c.primary} onPress={onAction}>{action}</T>}
    </View>
  );
}

export function Segmented<K extends string>({ items, value, onChange }: { items: { key: K; label: string }[]; value: K; onChange: (k: K) => void }) {
  const c = useColors();
  return (
    <View style={[styles.segmented, { backgroundColor: c.glass, borderColor: c.glassBorder }]}>
      {items.map((i) => {
        const active = i.key === value;
        return (
          <Pressable key={i.key} accessibilityRole="tab" accessibilityState={{ selected: active }} onPress={() => onChange(i.key)}
            style={[styles.segment, active && [{ backgroundColor: c.surface }, depth(c, 1)]]}>
            <Text style={[type.label, { color: active ? c.primary : c.textSecondary }]}>{i.label}</Text>
          </Pressable>
        );
      })}
    </View>
  );
}

// --- En-tête d'écran ----------------------------------------------------------------------

export function Header({ title, subtitle, back = true, right, onBack }: {
  title: string; subtitle?: string; back?: boolean; right?: ReactNode; onBack?: () => void;
}) {
  const c = useColors();
  const insets = useSafeAreaInsets();
  return (
    <View style={[styles.header, { paddingTop: insets.top + 6, backgroundColor: c.glassStrong, borderColor: c.glassBorder }, depth(c, 1)]}>
      {back && <IconButton name="arrow-left" label="Retour" onPress={onBack ?? (() => (router.canGoBack() ? router.back() : router.replace('/home')))} />}
      <View style={{ flex: 1, marginLeft: back ? 4 : 8 }}>
        <T v="h3" lines={1}>{title}</T>
        {!!subtitle && <Muted lines={1}>{subtitle}</Muted>}
      </View>
      {right}
    </View>
  );
}

export function Screen({ children, style }: { children: ReactNode; style?: StyleProp<ViewStyle> }) {
  const c = useColors();
  return (
    <View style={[{ flex: 1, backgroundColor: c.background }, style]}>
      <Backdrop />
      {children}
    </View>
  );
}

// --- États --------------------------------------------------------------------------------

export function Loading({ label = 'Chargement…' }: { label?: string }) {
  const c = useColors();
  return (
    <View style={styles.state} accessibilityLabel={label}>
      <ActivityIndicator color={c.primary} size="large" />
      <Muted style={{ marginTop: 12 }}>{label}</Muted>
    </View>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const c = useColors();
  return (
    <View style={styles.state}>
      <View style={[styles.stateIcon, { backgroundColor: c.errorLight }]}><Icon name="wifi-off" size={32} color={c.error} /></View>
      <T v="h3" style={styles.center}>Oups, un problème</T>
      <Muted style={styles.center}>{message}</Muted>
      {onRetry && <Button title="Réessayer" icon="refresh" onPress={onRetry} style={{ marginTop: 16 }} />}
    </View>
  );
}

export function EmptyState({ icon = 'inbox-outline', title, message, action, onAction }: {
  icon?: IconName; title: string; message?: string; action?: string; onAction?: () => void;
}) {
  const c = useColors();
  return (
    <View style={styles.state}>
      <View style={[styles.stateIcon, { backgroundColor: c.primaryLight }]}><Icon name={icon} size={32} color={c.primary} /></View>
      <T v="h3" style={styles.center}>{title}</T>
      {!!message && <Muted style={styles.center}>{message}</Muted>}
      {!!action && <Button title={action} kind="outline" onPress={onAction} style={{ marginTop: 16 }} />}
    </View>
  );
}

/** Rend loading / erreur / contenu selon l'état d'une requête React Query. */
export function QueryView<D>({ query, children, loadingLabel }: {
  query: { data: D | undefined; isPending: boolean; error: unknown; refetch: () => unknown };
  children: (data: D) => ReactNode; loadingLabel?: string;
}) {
  if (query.isPending) return <Loading label={loadingLabel} />;
  if (query.error || query.data === undefined) {
    return <ErrorState message={errorMessage(query.error)} onRetry={() => query.refetch()} />;
  }
  return <>{children(query.data)}</>;
}

export const styles = StyleSheet.create({
  buttonShell: { borderRadius: radius.control },
  buttonSmallShell: { borderRadius: radius.pill },
  button: {
    minHeight: 50, borderRadius: radius.control, paddingHorizontal: 18, overflow: 'hidden',
    flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8,
  },
  buttonSmall: { minHeight: 36, paddingHorizontal: 14, borderRadius: radius.pill },
  buttonText: { fontSize: 15, fontFamily: font.semibold },
  iconButton: { width: 40, height: 40, alignItems: 'center', justifyContent: 'center' },
  iconBadge: { position: 'absolute', top: 2, right: 0 },
  badge: { minWidth: 20, height: 20, borderRadius: 10, paddingHorizontal: 5, borderWidth: 1.5, alignItems: 'center', justifyContent: 'center' },
  badgeText: { color: '#FFFFFF', fontSize: 11, fontFamily: font.bold },
  field: { marginBottom: 8 },
  inputBox: { minHeight: 48, borderWidth: 1.5, borderRadius: radius.control, paddingHorizontal: 12, flexDirection: 'row', alignItems: 'center', gap: 8 },
  input: { flex: 1, fontSize: 15, fontFamily: font.regular, paddingVertical: 10 },
  fieldFooter: { flexDirection: 'row', minHeight: 18, marginTop: 4, gap: 8 },
  eye: { position: 'absolute', right: 12 },
  search: { flexDirection: 'row', alignItems: 'center', gap: 8, borderRadius: radius.pill, paddingHorizontal: 14, minHeight: 46, borderWidth: 1 },
  backdrop: { flex: 1 },
  sheet: { borderTopLeftRadius: 20, borderTopRightRadius: 20, padding: 16 },
  option: { flexDirection: 'row', alignItems: 'center', paddingVertical: 14, borderBottomWidth: StyleSheet.hairlineWidth },
  row: { flexDirection: 'row', alignItems: 'center' },
  listRow: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 14, borderBottomWidth: StyleSheet.hairlineWidth, minHeight: 52 },
  chip: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 15, height: 36, borderRadius: radius.pill, borderWidth: 1, overflow: 'hidden' },
  chipScroll: { flexGrow: 0, flexShrink: 0 },
  chipRow: { gap: 8, paddingHorizontal: 16, paddingVertical: 8, alignItems: 'center' },
  tag: { alignSelf: 'flex-start', paddingHorizontal: 8, paddingVertical: 3, borderRadius: 6 },
  cardShell: { borderRadius: radius.card },
  card: { borderRadius: radius.card, borderWidth: 1, overflow: 'hidden' },
  avatar: { alignItems: 'center', justifyContent: 'center', overflow: 'hidden' },
  sectionTitle: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, marginTop: 20, marginBottom: 10 },
  segmented: { flexDirection: 'row', borderRadius: radius.pill, padding: 4, marginHorizontal: 16, borderWidth: 1 },
  segment: { flex: 1, alignItems: 'center', paddingVertical: 9, borderRadius: radius.pill },
  header: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 8, paddingBottom: 10, borderBottomWidth: 1, zIndex: 2 },
  state: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 32, minHeight: 280 },
  stateIcon: { width: 72, height: 72, borderRadius: 36, alignItems: 'center', justifyContent: 'center', marginBottom: 16 },
  center: { textAlign: 'center', marginTop: 4 },
});
