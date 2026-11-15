import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowDown, ArrowUp, Bell, Briefcase, CalendarDays, Eye, EyeOff, Gift, Globe, HandHeart, ImagePlus, type LucideIcon, Pencil, Plane, Plus, Search,
  SquarePen, Star, Trash2, Users, Wrench, X,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Select, Textarea } from '@/components/ui'
import { UploadImage } from '@/components/UploadImage'
import { api, errorMessage } from '@/lib/api'
import { fmtDateTime, fmtRelative } from '@/lib/format'
import type { BannerAction, BannerLayout, BannerStatus, BannerTheme, HomeBanner } from '@/lib/types'

const KEY = ['banners']

/** Mêmes dégradés que l'application. */
const THEMES: Record<BannerTheme, { label: string; colors: [string, string, string] }> = {
  Green: { label: 'Vert BéninConnect', colors: ['#12945F', '#087F5B', '#05553F'] },
  Night: { label: 'Nuit', colors: ['#334155', '#1E293B', '#0F172A'] },
  Ocean: { label: 'Océan', colors: ['#0EA5E9', '#0369A1', '#0C4A6E'] },
  Sunset: { label: 'Coucher de soleil', colors: ['#F59E0B', '#EA580C', '#9A3412'] },
}

/** Icônes proposées (nom Material Design Icons utilisé par l'application → aperçu ici). */
const ICONS: { value: string; label: string; icon: LucideIcon }[] = [
  { value: 'account-group-outline', label: 'Communautés', icon: Users },
  { value: 'briefcase-outline', label: 'Emplois', icon: Briefcase },
  { value: 'calendar-month-outline', label: 'Événements', icon: CalendarDays },
  { value: 'airplane', label: 'Voyages', icon: Plane },
  { value: 'hand-heart-outline', label: 'Services', icon: HandHeart },
  { value: 'account-wrench-outline', label: 'Proposer', icon: Wrench },
  { value: 'square-edit-outline', label: 'Publier', icon: SquarePen },
  { value: 'magnify', label: 'Rechercher', icon: Search },
  { value: 'bell-outline', label: 'Notifications', icon: Bell },
  { value: 'gift-outline', label: 'Offre', icon: Gift },
  { value: 'star-outline', label: 'Nouveauté', icon: Star },
  { value: 'web', label: 'Lien', icon: Globe },
]
const iconOf = (name: string) => ICONS.find((i) => i.value === name)?.icon ?? Globe

/** Écrans de l'application (mêmes clés que l'API). */
const SCREENS: { value: string; label: string }[] = [
  { value: 'communities', label: 'Communautés' },
  { value: 'community.new', label: 'Créer une communauté' },
  { value: 'jobs', label: "Offres d'emploi" },
  { value: 'events', label: 'Événements' },
  { value: 'event.new', label: 'Créer un événement' },
  { value: 'services', label: 'Services' },
  { value: 'services.offer', label: 'Proposer un service' },
  { value: 'voyages', label: 'Voyages (si le module est ouvert)' },
  { value: 'voyages.publish', label: 'Proposer un voyage' },
  { value: 'post.new', label: 'Nouvelle publication' },
  { value: 'search', label: 'Recherche' },
  { value: 'notifications', label: 'Notifications' },
]
const URL_TARGET = '__url'
const targetLabel = (t: string) => SCREENS.find((s) => s.value === t)?.label ?? t

const STATUS: Record<BannerStatus, { label: string; tone: 'brand' | 'info' | 'neutral' | 'warning' }> = {
  Live: { label: 'En ligne', tone: 'brand' },
  Scheduled: { label: 'Programmée', tone: 'info' },
  Ended: { label: 'Terminée', tone: 'neutral' },
  Hidden: { label: 'Masquée', tone: 'warning' },
}

interface Form {
  title: string
  highlight: string
  subtitle: string
  imagePath: string | null
  layout: BannerLayout
  theme: BannerTheme
  actions: BannerAction[]
  isActive: boolean
  startsAt: string
  endsAt: string
}

const EMPTY: Form = {
  title: '', highlight: '', subtitle: '', imagePath: null, layout: 'Illustration', theme: 'Green',
  actions: [{ label: 'Voir', icon: 'account-group-outline', target: 'communities' }], isActive: true, startsAt: '', endsAt: '',
}

const toLocalInput = (iso: string | null) => {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

const toForm = (b: HomeBanner): Form => ({
  title: b.title, highlight: b.highlight ?? '', subtitle: b.subtitle ?? '', imagePath: b.imagePath, layout: b.layout, theme: b.theme,
  actions: b.actions, isActive: b.isActive, startsAt: toLocalInput(b.startsAt), endsAt: toLocalInput(b.endsAt),
})

const toBody = (f: Form) => ({
  title: f.title, highlight: f.highlight || null, subtitle: f.subtitle || null, imagePath: f.imagePath, layout: f.layout, theme: f.theme,
  actions: f.actions, isActive: f.isActive,
  startsAt: f.startsAt ? new Date(f.startsAt).toISOString() : null, endsAt: f.endsAt ? new Date(f.endsAt).toISOString() : null,
})

/** Rendu proche de l'application (largeur d'un téléphone). */
function BannerPreview({ banner }: { banner: Pick<Form, 'title' | 'highlight' | 'subtitle' | 'imagePath' | 'layout' | 'theme' | 'actions'> }) {
  const [a, b, c] = THEMES[banner.theme].colors
  const background = banner.layout === 'Background' && banner.imagePath
  const parts = banner.highlight && banner.title.includes(banner.highlight) ? banner.title.split(banner.highlight) : null
  return (
    <div className="relative flex h-[188px] w-[340px] max-w-full shrink-0 flex-col overflow-hidden rounded-3xl p-4 text-white"
      style={{ background: `linear-gradient(135deg, ${a}, ${b} 55%, ${c})` }}>
      {banner.imagePath && (
        background ? (
          <>
            <UploadImage path={banner.imagePath} className="absolute inset-0 h-full w-full object-cover" />
            <div className="absolute inset-0" style={{ background: 'linear-gradient(90deg, rgba(0,0,0,0.65), rgba(0,0,0,0.15))' }} />
          </>
        ) : (
          <div className="absolute bottom-0 right-0 h-[112px] w-[176px]">
            <UploadImage path={banner.imagePath} className="h-full w-full object-cover" />
            <div className="absolute inset-0" style={{ background: `linear-gradient(90deg, ${b}, transparent 25%), linear-gradient(180deg, ${b}, transparent 30%)` }} />
          </div>
        )
      )}
      <div className="relative max-w-[62%]">
        <p className="text-[17px] font-bold leading-snug">
          {parts ? <>{parts[0]}<span className="text-[#FCD116]">{banner.highlight}</span>{parts.slice(1).join(banner.highlight)}</> : banner.title || 'Titre de la bannière'}
        </p>
        {banner.subtitle && <p className="mt-1 line-clamp-2 text-[11.5px] text-white/85">{banner.subtitle}</p>}
      </div>
      <div className="relative mt-auto flex gap-2">
        {banner.actions.map((act, i) => {
          const Icon = iconOf(act.icon)
          return (
            <span key={i} className="flex h-[46px] flex-1 flex-col items-center justify-center gap-0.5 rounded-xl bg-white px-1 text-[10px] font-semibold text-gray-900">
              <Icon className="size-4 text-[#087F5B]" />
              <span className="max-w-full truncate">{act.label || '—'}</span>
            </span>
          )
        })}
      </div>
    </div>
  )
}

function ActionRow({ action, onChange, onRemove }: { action: BannerAction; onChange: (a: BannerAction) => void; onRemove: () => void }) {
  const isUrl = !SCREENS.some((s) => s.value === action.target)
  return (
    <div className="grid gap-2 rounded-xl border border-line p-3 sm:grid-cols-[1fr_1fr_1.3fr_auto] sm:items-end">
      <Input label="Libellé" maxLength={24} value={action.label} onChange={(e) => onChange({ ...action, label: e.target.value })} />
      <Select label="Icône" value={action.icon} onChange={(e) => onChange({ ...action, icon: e.target.value })}>
        {ICONS.map((i) => <option key={i.value} value={i.value}>{i.label}</option>)}
      </Select>
      <div className="space-y-2">
        <Select label="Destination" value={isUrl ? URL_TARGET : action.target}
          onChange={(e) => onChange({ ...action, target: e.target.value === URL_TARGET ? 'https://' : e.target.value })}>
          {SCREENS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
          <option value={URL_TARGET}>Lien externe (https)</option>
        </Select>
        {isUrl && <Input placeholder="https://…" value={action.target} onChange={(e) => onChange({ ...action, target: e.target.value })} />}
      </div>
      <Button variant="ghost" aria-label="Retirer le bouton" icon={<X className="size-4" />} onClick={onRemove} />
    </div>
  )
}

function BannerEditor({ banner, open, onClose }: { banner: HomeBanner | null; open: boolean; onClose: () => void }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [form, setForm] = useState<Form>(EMPTY)
  const [uploading, setUploading] = useState(false)
  useEffect(() => { if (open) setForm(banner ? toForm(banner) : EMPTY) }, [open, banner])
  const set = (patch: Partial<Form>) => setForm((f) => ({ ...f, ...patch }))

  const save = useMutation({
    mutationFn: () => api<HomeBanner>(banner ? `api/admin/banners/${banner.id}` : 'api/admin/banners', { method: banner ? 'PUT' : 'POST', body: toBody(form) }),
    onSuccess: () => {
      toast(banner ? 'Bannière enregistrée.' : 'Bannière créée.')
      qc.invalidateQueries({ queryKey: KEY })
      onClose()
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  const upload = async (file: File | undefined) => {
    if (!file) return
    setUploading(true)
    try {
      const body = new FormData()
      body.append('file', file)
      const { path } = await api<{ path: string }>('api/admin/banners/images', { method: 'POST', body })
      set({ imagePath: path })
    } catch (e) {
      toast(errorMessage(e), 'error')
    } finally {
      setUploading(false)
    }
  }

  return (
    <Modal open={open} wide title={banner ? 'Modifier la bannière' : 'Nouvelle bannière'} onClose={onClose} footer={
      <>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" loading={save.isPending} disabled={uploading || form.title.trim().length < 3} onClick={() => save.mutate()}>Enregistrer</Button>
      </>
    }>
      <div className="space-y-4">
        <div className="flex justify-center rounded-xl bg-surface-2 p-4"><BannerPreview banner={form} /></div>
        <Input label="Titre" maxLength={80} value={form.title} onChange={(e) => set({ title: e.target.value })} placeholder="La communauté des Béninois du monde entier" />
        <div className="grid gap-3 sm:grid-cols-2">
          <Input label="Mot mis en valeur (jaune, facultatif)" value={form.highlight} onChange={(e) => set({ highlight: e.target.value })} placeholder="Béninois" />
          <Select label="Couleurs" value={form.theme} onChange={(e) => set({ theme: e.target.value as BannerTheme })}>
            {(Object.keys(THEMES) as BannerTheme[]).map((t) => <option key={t} value={t}>{THEMES[t].label}</option>)}
          </Select>
        </div>
        <Textarea label="Sous-titre (facultatif)" rows={2} maxLength={140} value={form.subtitle} onChange={(e) => set({ subtitle: e.target.value })} />

        <div className="grid gap-3 sm:grid-cols-2 sm:items-end">
          <div>
            <span className="mb-1.5 block text-sm font-medium">Photo (facultative)</span>
            <div className="flex items-center gap-2">
              <label className="inline-flex h-10 cursor-pointer items-center gap-2 rounded-lg border border-line px-3 text-sm hover:bg-surface-2">
                <ImagePlus className="size-4" /> {uploading ? 'Envoi…' : form.imagePath ? 'Remplacer' : 'Choisir une photo'}
                <input type="file" accept="image/*" className="hidden" disabled={uploading} onChange={(e) => { void upload(e.target.files?.[0]); e.target.value = '' }} />
              </label>
              {form.imagePath && <Button variant="ghost" onClick={() => set({ imagePath: null, layout: 'Illustration' })}>Retirer</Button>}
            </div>
          </div>
          <Select label="Mise en page" value={form.layout} disabled={!form.imagePath} onChange={(e) => set({ layout: e.target.value as BannerLayout })}>
            <option value="Illustration">Photo à droite, texte à gauche</option>
            <option value="Background">Photo en fond, assombrie</option>
          </Select>
        </div>

        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium">Boutons ({form.actions.length}/4)</span>
            <Button icon={<Plus className="size-4" />} disabled={form.actions.length >= 4}
              onClick={() => set({ actions: [...form.actions, { label: '', icon: 'account-group-outline', target: 'communities' }] })}>Ajouter</Button>
          </div>
          {form.actions.map((a, i) => (
            <ActionRow key={i} action={a}
              onChange={(next) => set({ actions: form.actions.map((x, j) => (j === i ? next : x)) })}
              onRemove={() => set({ actions: form.actions.filter((_, j) => j !== i) })} />
          ))}
          <p className="text-xs text-muted">Les boutons vers Voyages ne s'affichent pas tant que le module Voyages est masqué.</p>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <Input label="Début de diffusion (facultatif)" type="datetime-local" value={form.startsAt} onChange={(e) => set({ startsAt: e.target.value })} />
          <Input label="Fin de diffusion (facultative)" type="datetime-local" value={form.endsAt} onChange={(e) => set({ endsAt: e.target.value })} />
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={form.isActive} onChange={(e) => set({ isActive: e.target.checked })} /> Visible dans l'application
        </label>
      </div>
    </Modal>
  )
}

/** Bannières de l'accueil de l'application : contenu, ordre, période de diffusion. */
export function BannersPage() {
  const toast = useToast()
  const qc = useQueryClient()
  const [editing, setEditing] = useState<HomeBanner | null>(null)
  const [creating, setCreating] = useState(false)
  const [deleting, setDeleting] = useState<HomeBanner | null>(null)
  const q = useQuery({ queryKey: KEY, queryFn: () => api<HomeBanner[]>('api/admin/banners') })
  const refresh = () => qc.invalidateQueries({ queryKey: KEY })

  const reorder = useMutation({
    mutationFn: (ids: string[]) => api<HomeBanner[]>('api/admin/banners/order', { method: 'PUT', body: { ids } }),
    onSuccess: (list) => qc.setQueryData(KEY, list),
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const toggle = useMutation({
    mutationFn: (b: HomeBanner) => api(`api/admin/banners/${b.id}`, { method: 'PUT', body: toBody({ ...toForm(b), isActive: !b.isActive }) }),
    onSuccess: () => refresh(),
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const remove = useMutation({
    mutationFn: (b: HomeBanner) => api(`api/admin/banners/${b.id}`, { method: 'DELETE' }),
    onSuccess: () => { toast('Bannière supprimée.'); setDeleting(null); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  if (q.isLoading) return <Loading />
  if (q.error) return <ErrorBox message={errorMessage(q.error)} onRetry={() => q.refetch()} />
  const list = q.data!
  const move = (index: number, delta: number) => {
    const ids = list.map((b) => b.id)
    const [id] = ids.splice(index, 1)
    ids.splice(index + delta, 0, id)
    reorder.mutate(ids)
  }
  const live = list.filter((b) => b.status === 'Live').length

  return (
    <div className="space-y-5">
      <PageHeader title="Bannières de l'accueil"
        description="Diapositives affichées en haut de l'accueil de l'application, dans l'ordre ci-dessous. Sans bannière en ligne, l'application affiche ses bannières par défaut."
        actions={<Button variant="primary" icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>Nouvelle bannière</Button>} />

      <Card className="px-5 py-3 text-sm">
        {live > 0 ? `${live} bannière(s) en ligne dans l'application.` : "Aucune bannière en ligne : l'application affiche ses bannières par défaut."}
      </Card>

      {list.length === 0 ? <Empty title="Aucune bannière" description="Créez une bannière pour remplacer celles affichées par défaut." /> : (
        <div className="space-y-3">
          {list.map((b, i) => (
            <Card key={b.id} className="flex flex-col gap-4 p-4 lg:flex-row lg:items-center">
              <BannerPreview banner={toForm(b)} />
              <div className="min-w-0 flex-1 space-y-1.5">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-sm text-muted">#{i + 1}</span>
                  <Badge tone={STATUS[b.status].tone}>{STATUS[b.status].label}</Badge>
                  <Badge>{THEMES[b.theme].label}</Badge>
                </div>
                <p className="font-semibold">{b.title}</p>
                <p className="text-sm text-muted">
                  {b.startsAt || b.endsAt
                    ? `Diffusion ${b.startsAt ? `du ${fmtDateTime(b.startsAt)}` : ''}${b.endsAt ? ` jusqu'au ${fmtDateTime(b.endsAt)}` : ''}`
                    : 'Diffusion sans limite de date'}
                </p>
                <p className="text-sm text-muted">Boutons : {b.actions.map((a) => `${a.label} → ${targetLabel(a.target)}`).join(' · ') || 'aucun'}</p>
                <p className="text-xs text-muted">Modifiée {fmtRelative(b.updatedAt)} par {b.updatedBy}</p>
              </div>
              <div className="flex flex-wrap gap-2 lg:flex-col">
                <div className="flex gap-2">
                  <Button aria-label="Monter" icon={<ArrowUp className="size-4" />} disabled={i === 0 || reorder.isPending} onClick={() => move(i, -1)} />
                  <Button aria-label="Descendre" icon={<ArrowDown className="size-4" />} disabled={i === list.length - 1 || reorder.isPending} onClick={() => move(i, 1)} />
                </div>
                <Button icon={b.isActive ? <EyeOff className="size-4" /> : <Eye className="size-4" />} loading={toggle.isPending && toggle.variables?.id === b.id}
                  onClick={() => toggle.mutate(b)}>{b.isActive ? 'Masquer' : 'Afficher'}</Button>
                <Button icon={<Pencil className="size-4" />} onClick={() => setEditing(b)}>Modifier</Button>
                <Button variant="ghost" icon={<Trash2 className="size-4" />} onClick={() => setDeleting(b)}>Supprimer</Button>
              </div>
            </Card>
          ))}
        </div>
      )}

      <BannerEditor open={creating || !!editing} banner={editing} onClose={() => { setCreating(false); setEditing(null) }} />
      <ConfirmDialog open={!!deleting} danger busy={remove.isPending} title="Supprimer la bannière ?" confirmLabel="Supprimer"
        message={`« ${deleting?.title ?? ''} » disparaîtra de l'application. Sa photo sera supprimée.`}
        onClose={() => setDeleting(null)} onConfirm={() => deleting && remove.mutate(deleting)} />
    </div>
  )
}
