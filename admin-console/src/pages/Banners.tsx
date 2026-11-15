import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowDown, ArrowUp, Bell, Briefcase, CalendarDays, Download, Eye, EyeOff, Gift, Globe, HandHeart, ImagePlus, type LucideIcon, Pencil, Plane, Plus,
  Search, SquarePen, Star, Trash2, Users, Wrench, X,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Select } from '@/components/ui'
import { UploadImage } from '@/components/UploadImage'
import { api, errorMessage } from '@/lib/api'
import { fmtDateTime, fmtRelative } from '@/lib/format'
import type { BannerAction, BannerStatus, HomeBanner } from '@/lib/types'

const KEY = ['banners']

/** Format attendu (mêmes règles que l'API). */
const SIZE = { width: 1600, height: 900, minWidth: 1080, ratio: 16 / 9, tolerance: 0.03, maxBytes: 10 * 1024 * 1024 }
/** Hauteur de la zone des boutons dans l'application (part de l'image, en bas). */
const BUTTON_ZONE = 0.3

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
const NONE = ''
const isScreen = (t: string) => SCREENS.some((s) => s.value === t)
const targetLabel = (t: string) => SCREENS.find((s) => s.value === t)?.label ?? t

const STATUS: Record<BannerStatus, { label: string; tone: 'brand' | 'info' | 'neutral' | 'warning' }> = {
  Live: { label: 'En ligne', tone: 'brand' },
  Scheduled: { label: 'Programmée', tone: 'info' },
  Ended: { label: 'Terminée', tone: 'neutral' },
  Hidden: { label: 'Masquée', tone: 'warning' },
}

interface Form {
  title: string
  imagePath: string | null
  target: string
  actions: BannerAction[]
  isActive: boolean
  startsAt: string
  endsAt: string
}

const EMPTY: Form = { title: '', imagePath: null, target: NONE, actions: [], isActive: true, startsAt: '', endsAt: '' }

const toLocalInput = (iso: string | null) => {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

const toForm = (b: HomeBanner): Form => ({
  title: b.title, imagePath: b.imagePath, target: b.target ?? NONE, actions: b.actions, isActive: b.isActive,
  startsAt: toLocalInput(b.startsAt), endsAt: toLocalInput(b.endsAt),
})

const toBody = (f: Form) => ({
  title: f.title, imagePath: f.imagePath, target: f.target || null, actions: f.actions, isActive: f.isActive,
  startsAt: f.startsAt ? new Date(f.startsAt).toISOString() : null, endsAt: f.endsAt ? new Date(f.endsAt).toISOString() : null,
})

/** Dimensions réelles d'une image choisie, avant l'envoi. */
function readSize(file: File) {
  return new Promise<{ width: number; height: number }>((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const img = new Image()
    img.onload = () => { resolve({ width: img.naturalWidth, height: img.naturalHeight }); URL.revokeObjectURL(url) }
    img.onerror = () => { reject(new Error('Image illisible.')); URL.revokeObjectURL(url) }
    img.src = url
  })
}

const sizeProblem = ({ width, height }: { width: number; height: number }) =>
  width < SIZE.minWidth || Math.abs(width / height / SIZE.ratio - 1) > SIZE.tolerance
    ? `Image de ${width} × ${height} px : il faut le format 16:9, ${SIZE.width} × ${SIZE.height} px conseillé (${SIZE.minWidth} px de large au minimum).`
    : null

/** Gabarit 1600 × 900 à donner au graphiste : zone des boutons et marges de sécurité. */
function downloadTemplate() {
  const canvas = document.createElement('canvas')
  canvas.width = SIZE.width
  canvas.height = SIZE.height
  const g = canvas.getContext('2d')!
  g.fillStyle = '#E8F5EF'
  g.fillRect(0, 0, SIZE.width, SIZE.height)
  const margin = 64
  g.setLineDash([18, 12])
  g.lineWidth = 4
  g.strokeStyle = '#087F5B'
  g.strokeRect(margin, margin, SIZE.width - margin * 2, SIZE.height - margin * 2)
  const zoneTop = SIZE.height * (1 - BUTTON_ZONE)
  g.fillStyle = 'rgba(234, 88, 12, 0.18)'
  g.fillRect(0, zoneTop, SIZE.width, SIZE.height - zoneTop)
  g.setLineDash([])
  g.fillStyle = '#065F46'
  g.font = 'bold 56px Inter, Arial, sans-serif'
  g.fillText('Bannière BéninConnect · 1600 × 900 px (16:9)', margin + 24, margin + 90)
  g.font = '36px Inter, Arial, sans-serif'
  g.fillText('Textes et visuels importants dans le cadre pointillé (bords arrondis et rognés selon l’écran).', margin + 24, margin + 150)
  g.fillStyle = '#9A3412'
  g.font = 'bold 40px Inter, Arial, sans-serif'
  g.fillText('Zone des boutons (30 % du bas) : à laisser libre si la bannière a des boutons', margin + 24, zoneTop + 80)
  const link = document.createElement('a')
  link.download = 'gabarit-banniere-1600x900.png'
  link.href = canvas.toDataURL('image/png')
  link.click()
}

/** Rendu proche de l'application : image 16:9, boutons blancs en bas. */
function BannerPreview({ banner, guides }: { banner: Pick<Form, 'imagePath' | 'actions'>; guides?: boolean }) {
  return (
    <div className="relative aspect-video w-[360px] max-w-full shrink-0 overflow-hidden rounded-3xl bg-surface-2">
      {banner.imagePath
        ? <UploadImage path={banner.imagePath} className="absolute inset-0 h-full w-full object-cover" />
        : <div className="absolute inset-0 grid place-items-center text-sm text-muted">Image 16:9 · {SIZE.width} × {SIZE.height} px</div>}
      {guides && (
        <div className="pointer-events-none absolute inset-x-0 bottom-0 border-t-2 border-dashed border-orange-500/80 bg-orange-500/10"
          style={{ height: `${BUTTON_ZONE * 100}%` }} title="Zone des boutons" />
      )}
      {banner.actions.length > 0 && (
        <div className="absolute inset-x-3 bottom-3 flex gap-2">
          {banner.actions.map((act, i) => {
            const Icon = iconOf(act.icon)
            return (
              <span key={i} className="flex h-[46px] flex-1 flex-col items-center justify-center gap-0.5 rounded-xl bg-white px-1 text-[10px] font-semibold text-gray-900 shadow">
                <Icon className="size-4 text-[#087F5B]" />
                <span className="max-w-full truncate">{act.label || '—'}</span>
              </span>
            )
          })}
        </div>
      )}
    </div>
  )
}

/** Destination : écran de l'application ou lien https (« aucune » possible pour le lien de l'image). */
function TargetPicker({ label, value, onChange, allowNone }: { label: string; value: string; onChange: (v: string) => void; allowNone?: boolean }) {
  const url = value !== NONE && !isScreen(value)
  return (
    <div className="space-y-2">
      <Select label={label} value={url ? URL_TARGET : value} onChange={(e) => onChange(e.target.value === URL_TARGET ? 'https://' : e.target.value)}>
        {allowNone && <option value={NONE}>Aucun</option>}
        {SCREENS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        <option value={URL_TARGET}>Lien externe (https)</option>
      </Select>
      {url && <Input placeholder="https://…" value={value} onChange={(e) => onChange(e.target.value)} />}
    </div>
  )
}

function ActionRow({ action, onChange, onRemove }: { action: BannerAction; onChange: (a: BannerAction) => void; onRemove: () => void }) {
  return (
    <div className="grid gap-2 rounded-xl border border-line p-3 sm:grid-cols-[1fr_1fr_1.3fr_auto] sm:items-end">
      <Input label="Libellé" maxLength={24} value={action.label} onChange={(e) => onChange({ ...action, label: e.target.value })} />
      <Select label="Icône" value={action.icon} onChange={(e) => onChange({ ...action, icon: e.target.value })}>
        {ICONS.map((i) => <option key={i.value} value={i.value}>{i.label}</option>)}
      </Select>
      <TargetPicker label="Destination" value={action.target} onChange={(target) => onChange({ ...action, target })} />
      <Button variant="ghost" aria-label="Retirer le bouton" icon={<X className="size-4" />} onClick={onRemove} />
    </div>
  )
}

function BannerEditor({ banner, open, onClose }: { banner: HomeBanner | null; open: boolean; onClose: () => void }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [form, setForm] = useState<Form>(EMPTY)
  const [uploading, setUploading] = useState(false)
  const [problem, setProblem] = useState<string | null>(null)
  useEffect(() => {
    if (!open) return
    setForm(banner ? toForm(banner) : EMPTY)
    setProblem(null)
  }, [open, banner])
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
    setProblem(null)
    try {
      if (file.size > SIZE.maxBytes) throw new Error("L'image ne doit pas dépasser 10 Mo.")
      const issue = sizeProblem(await readSize(file))
      if (issue) throw new Error(issue)
      setUploading(true)
      const body = new FormData()
      body.append('file', file)
      const { path } = await api<{ path: string }>('api/admin/banners/images', { method: 'POST', body })
      set({ imagePath: path })
    } catch (e) {
      setProblem(errorMessage(e))
    } finally {
      setUploading(false)
    }
  }

  return (
    <Modal open={open} wide title={banner ? 'Modifier la bannière' : 'Nouvelle bannière'} onClose={onClose} footer={
      <>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" loading={save.isPending} disabled={uploading || !form.imagePath || form.title.trim().length < 3} onClick={() => save.mutate()}>
          Enregistrer
        </Button>
      </>
    }>
      <div className="space-y-4">
        <div className="flex flex-col items-center gap-3 rounded-xl bg-surface-2 p-4">
          <BannerPreview banner={form} guides={form.actions.length > 0} />
          <div className="flex flex-wrap items-center justify-center gap-2">
            <label className="inline-flex h-10 cursor-pointer items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm hover:bg-surface-2">
              <ImagePlus className="size-4" /> {uploading ? 'Envoi…' : form.imagePath ? "Remplacer l'image" : 'Importer l’image'}
              <input type="file" accept="image/jpeg,image/png,image/webp" className="hidden" disabled={uploading}
                onChange={(e) => { void upload(e.target.files?.[0]); e.target.value = '' }} />
            </label>
            <Button variant="ghost" icon={<Download className="size-4" />} onClick={downloadTemplate}>Gabarit 1600 × 900</Button>
          </div>
          <p className="text-center text-xs text-muted">
            JPEG, PNG ou WebP · format 16:9 · {SIZE.width} × {SIZE.height} px conseillé ({SIZE.minWidth} px de large au minimum) · 10 Mo max.
            {form.actions.length > 0 && ' La zone orange (30 % du bas) est couverte par les boutons : gardez-la sans texte.'}
          </p>
          {problem && <p className="text-center text-sm text-danger">{problem}</p>}
        </div>

        <Input label="Nom (console et lecteurs d'écran)" maxLength={80} value={form.title} onChange={(e) => set({ title: e.target.value })}
          placeholder="Fête de l'indépendance 2027" />
        <TargetPicker label="Lien en touchant l'image (facultatif)" value={form.target} allowNone onChange={(target) => set({ target })} />

        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium">Boutons sur l'image ({form.actions.length}/4, facultatifs)</span>
            <Button icon={<Plus className="size-4" />} disabled={form.actions.length >= 4}
              onClick={() => set({ actions: [...form.actions, { label: '', icon: 'account-group-outline', target: 'communities' }] })}>Ajouter</Button>
          </div>
          {form.actions.map((a, i) => (
            <ActionRow key={i} action={a}
              onChange={(next) => set({ actions: form.actions.map((x, j) => (j === i ? next : x)) })}
              onRemove={() => set({ actions: form.actions.filter((_, j) => j !== i) })} />
          ))}
          <p className="text-xs text-muted">Les boutons et liens vers Voyages ne s'affichent pas tant que le module Voyages est masqué.</p>
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

/** Bannières de l'accueil de l'application : image, boutons, ordre, période de diffusion. */
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
        description={`Images 16:9 (${SIZE.width} × ${SIZE.height} px) affichées en haut de l'accueil de l'application, dans l'ordre ci-dessous, avec des boutons facultatifs. Sans bannière en ligne, l'application affiche ses bannières par défaut.`}
        actions={<Button variant="primary" icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>Nouvelle bannière</Button>} />

      <Card className="flex flex-wrap items-center justify-between gap-3 px-5 py-3 text-sm">
        <span>{live > 0 ? `${live} bannière(s) en ligne dans l'application.` : "Aucune bannière en ligne : l'application affiche ses bannières par défaut."}</span>
        <Button variant="ghost" icon={<Download className="size-4" />} onClick={downloadTemplate}>Gabarit pour le graphiste</Button>
      </Card>

      {list.length === 0 ? <Empty title="Aucune bannière" description="Importez une image 16:9 pour remplacer les bannières par défaut." /> : (
        <div className="space-y-3">
          {list.map((b, i) => (
            <Card key={b.id} className="flex flex-col gap-4 p-4 lg:flex-row lg:items-center">
              <BannerPreview banner={b} />
              <div className="min-w-0 flex-1 space-y-1.5">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-sm text-muted">#{i + 1}</span>
                  <Badge tone={STATUS[b.status].tone}>{STATUS[b.status].label}</Badge>
                </div>
                <p className="font-semibold">{b.title}</p>
                <p className="text-sm text-muted">
                  {b.startsAt || b.endsAt
                    ? `Diffusion ${b.startsAt ? `du ${fmtDateTime(b.startsAt)}` : ''}${b.endsAt ? ` jusqu'au ${fmtDateTime(b.endsAt)}` : ''}`
                    : 'Diffusion sans limite de date'}
                </p>
                {b.target && <p className="text-sm text-muted">Image → {targetLabel(b.target)}</p>}
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
        message={`« ${deleting?.title ?? ''} » disparaîtra de l'application. Son image sera supprimée.`}
        onClose={() => setDeleting(null)} onConfirm={() => deleting && remove.mutate(deleting)} />
    </div>
  )
}
