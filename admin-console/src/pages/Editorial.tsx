import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  BadgeCheck, CalendarClock, ChevronLeft, ChevronRight, ExternalLink, ImagePlus, Newspaper, PenLine, Plus, Rss, Send, Sparkles, Trash2, X,
} from 'lucide-react'
import { useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, cx, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Pagination, Select, Textarea } from '@/components/ui'
import { api, apiUrl, errorMessage, qs } from '@/lib/api'
import { fmtDateTime, fmtRelative } from '@/lib/format'
import type { AdminCommunity, CandidateStatus, EditorialCandidate, EditorialOverview, Paged, PipelineReport } from '@/lib/types'

const TABS: { key: CandidateStatus; label: string; hint: string }[] = [
  { key: 'Drafted', label: 'À valider', hint: 'Brouillons rédigés, à relire avant publication.' },
  { key: 'Collected', label: 'À rédiger', hint: 'Articles collectés, pas encore rédigés.' },
  { key: 'Approved', label: 'Programmées', hint: 'Validées : publiées automatiquement à l’heure prévue.' },
  { key: 'Published', label: 'Publiées', hint: 'En ligne sous le compte officiel.' },
  { key: 'Skipped', label: 'Écartées', hint: 'Hors sujet ou sensibles selon la rédaction automatique. Repêchables.' },
  { key: 'Rejected', label: 'Rejetées', hint: 'Refusées par l’équipe.' },
  { key: 'Failed', label: 'Échecs', hint: 'Publication impossible après plusieurs essais.' },
]
const TAKE = 15
const MAX_IMAGES = 10
const SITE = 'https://monbenin.com'

type Editor = { mode: 'new' } | { mode: 'draft' | 'approve'; candidate: EditorialCandidate }

/** Date ISO → valeur d'un champ datetime-local (heure locale). */
const toLocalInput = (iso: string | null) => {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

export function EditorialPage() {
  const toast = useToast()
  const qc = useQueryClient()
  const [status, setStatus] = useState<CandidateStatus>('Drafted')
  const [skip, setSkip] = useState(0)
  const [editor, setEditor] = useState<Editor | null>(null)
  const [rejecting, setRejecting] = useState<EditorialCandidate | null>(null)

  const overview = useQuery({ queryKey: ['editorial'], queryFn: () => api<EditorialOverview>('api/admin/editorial') })
  const list = useQuery({
    queryKey: ['editorial-candidates', status, skip],
    queryFn: () => api<Paged<EditorialCandidate>>(`api/admin/editorial/candidates${qs({ status, skip, take: TAKE })}`),
  })

  const refresh = () => ['editorial', 'editorial-candidates'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
  const onError = (e: unknown) => toast(errorMessage(e), 'error')

  const run = useMutation({
    mutationFn: (step: 'collect' | 'draft' | 'publish') => api<PipelineReport>(`api/admin/editorial/run/${step}`, { method: 'POST' }),
    onSuccess: (r, step) => {
      toast(step === 'collect' ? `${r.collected} article(s) collecté(s).`
        : step === 'draft' ? `${r.drafted} brouillon(s), ${r.skipped} écarté(s).`
          : `${r.published} publication(s) en ligne${r.failed ? `, ${r.failed} échec(s)` : ''}.`)
      refresh()
    },
    onError,
  })

  const action = useMutation({
    mutationFn: ({ id, verb, body }: { id: string; verb: 'unschedule' | 'retry' | 'reject'; body?: unknown }) =>
      api<EditorialCandidate>(`api/admin/editorial/candidates/${id}/${verb}`, { method: 'POST', body: body ?? {} }),
    onSuccess: () => { setRejecting(null); refresh() },
    onError,
  })

  const o = overview.data
  const count = (s: CandidateStatus) => o?.counts.find((c) => c.label === s)?.count ?? 0
  const tab = TABS.find((t) => t.key === status)!

  return (
    <>
      <PageHeader title="Compte officiel" description={`Publications de « ${o?.officialName ?? 'BéninConnect'} » : rien n'est publié sans validation de l'équipe.`}
        actions={
          <>
            <Button icon={<Rss className="size-4" />} loading={run.isPending && run.variables === 'collect'} onClick={() => run.mutate('collect')}>Collecter</Button>
            <Button icon={<Sparkles className="size-4" />} disabled={!o?.llmConfigured} loading={run.isPending && run.variables === 'draft'}
              title={o?.llmConfigured ? undefined : 'Rédaction automatique non configurée'} onClick={() => run.mutate('draft')}>Rédiger</Button>
            <Button variant="primary" icon={<Plus className="size-4" />} onClick={() => setEditor({ mode: 'new' })}>Nouvelle publication</Button>
          </>
        } />

      {o && (
        <div className="mb-4 flex flex-wrap items-center gap-2 text-sm">
          <Badge tone="brand"><BadgeCheck className="mr-1 inline size-3.5" />{o.officialName}</Badge>
          <Badge tone={o.automationEnabled ? 'brand' : 'neutral'}>{o.automationEnabled ? 'Tâches automatiques actives' : 'Tâches automatiques désactivées'}</Badge>
          <Badge tone={o.llmConfigured ? 'info' : 'warning'}>{o.llmConfigured ? `Rédaction : ${o.llmModel}` : 'Rédaction automatique non configurée'}</Badge>
          {count('Approved') > 0 && (
            <Button variant="ghost" icon={<Send className="size-4" />} loading={run.isPending && run.variables === 'publish'} onClick={() => run.mutate('publish')}>
              Publier maintenant ce qui est dû
            </Button>
          )}
        </div>
      )}

      <div className="mb-4 flex flex-wrap gap-2" role="tablist">
        {TABS.map((t) => (
          <button key={t.key} role="tab" aria-selected={status === t.key} onClick={() => { setStatus(t.key); setSkip(0) }}
            className={cx('rounded-full px-3.5 py-1.5 text-sm font-medium', status === t.key ? 'bg-brand text-white' : 'border border-line bg-surface text-muted hover:text-fg')}>
            {t.label}{o ? ` · ${count(t.key)}` : ''}
          </button>
        ))}
      </div>

      <Card>
        <p className="border-b border-line px-5 py-3 text-sm text-muted">{tab.hint}</p>
        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.items.length === 0 ? (
          <Empty title="Rien ici" description={status === 'Drafted' ? 'Aucun brouillon à valider.' : undefined} />
        ) : (
          <>
            <ul className="divide-y divide-line">
              {list.data.items.map((c) => (
                <li key={c.id} className="flex flex-col gap-4 p-4 sm:p-5 lg:flex-row lg:items-start">
                  <div className="grid size-10 shrink-0 place-items-center rounded-full bg-brand-soft text-brand"><Newspaper className="size-4.5" /></div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge tone={c.isManual ? 'brand' : 'neutral'}>{c.isManual ? 'Écrite par l’équipe' : c.sourceName}</Badge>
                      {c.targetCommunityName && <Badge tone="info">{c.targetCommunityName}</Badge>}
                      {c.status === 'Approved' && c.scheduledAt && <Badge tone="warning"><CalendarClock className="mr-1 inline size-3.5" />{fmtDateTime(c.scheduledAt)}</Badge>}
                      <span className="text-xs text-muted" title={fmtDateTime(c.collectedAt)}>{fmtRelative(c.collectedAt)}</span>
                    </div>
                    {!c.isManual && <p className="mt-2 font-medium">{c.sourceTitle}</p>}
                    {c.draftContent
                      ? <p className="mt-2 whitespace-pre-wrap rounded-lg bg-surface-2 px-3 py-2 text-sm">{c.draftContent}</p>
                      : c.sourceExcerpt && <p className="mt-1 line-clamp-3 text-sm text-muted">{c.sourceExcerpt}</p>}
                    {c.images.length > 0 && <Thumbnails paths={c.images} />}
                    {!c.isManual && (
                      <a href={c.sourceUrl} target="_blank" rel="noreferrer" className="mt-2 inline-flex items-center gap-1 text-xs text-brand hover:underline">
                        Article source <ExternalLink className="size-3" />
                      </a>
                    )}
                    {c.reason && <p className="mt-2 text-xs text-muted">Motif : {c.reason}</p>}
                    {c.lastError && c.status !== 'Published' && <p className="mt-1 text-xs text-danger">Dernière erreur : {c.lastError}</p>}
                    {c.publishedAt && <p className="mt-2 text-xs text-muted">Publiée le {fmtDateTime(c.publishedAt)}</p>}
                  </div>
                  <div className="flex flex-wrap gap-2 lg:w-56 lg:flex-col">
                    {c.status === 'Drafted' && <Button variant="primary" icon={<Send className="size-4" />} onClick={() => setEditor({ mode: 'approve', candidate: c })}>Relire et programmer</Button>}
                    {c.status === 'Approved' && <>
                      <Button icon={<PenLine className="size-4" />} onClick={() => setEditor({ mode: 'approve', candidate: c })}>Modifier</Button>
                      <Button onClick={() => action.mutate({ id: c.id, verb: 'unschedule' })}>Déprogrammer</Button>
                    </>}
                    {(c.status === 'Collected' || c.status === 'Skipped' || c.status === 'Rejected') &&
                      <Button icon={<PenLine className="size-4" />} onClick={() => setEditor({ mode: 'draft', candidate: c })}>Rédiger moi-même</Button>}
                    {c.status === 'Failed' && <Button variant="primary" onClick={() => action.mutate({ id: c.id, verb: 'retry' })}>Réessayer</Button>}
                    {c.status === 'Published' && c.publishedPostId && (
                      <a href={`${SITE}/post/${c.publishedPostId}`} target="_blank" rel="noreferrer"
                        className="inline-flex h-9 items-center justify-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium hover:bg-surface-2">
                        Voir la publication <ExternalLink className="size-4" />
                      </a>
                    )}
                    {['Collected', 'Drafted', 'Approved', 'Skipped', 'Failed'].includes(c.status) &&
                      <Button variant="dangerSoft" onClick={() => setRejecting(c)}>Rejeter</Button>}
                  </div>
                </li>
              ))}
            </ul>
            <Pagination skip={skip} take={TAKE} total={list.data.totalCount} onChange={setSkip} />
          </>
        )}
      </Card>

      {o && <Sources overview={o} onChanged={refresh} />}

      {editor && <EditorModal editor={editor} maxLength={o?.maxLength ?? 2000} publishNow={!o?.automationEnabled} onClose={() => setEditor(null)}
        onDone={(next) => { setEditor(null); refresh(); if (next) { setStatus(next); setSkip(0) } }} />}

      <ConfirmDialog open={!!rejecting} title="Rejeter ce contenu" confirmLabel="Rejeter" danger busy={action.isPending}
        message={rejecting ? `« ${rejecting.sourceTitle} » ne sera pas publié.` : ''}
        reason={{ label: 'Motif (facultatif)', placeholder: 'Ex. : déjà publié, source peu fiable…' }}
        onClose={() => setRejecting(null)}
        onConfirm={(reason) => rejecting && action.mutate({ id: rejecting.id, verb: 'reject', body: { reason } })} />
    </>
  )
}

/** Rédaction et validation : texte final, communauté facultative, heure de publication. */
function EditorModal({ editor, maxLength, publishNow, onClose, onDone }: {
  editor: Editor; maxLength: number; publishNow: boolean; onClose: () => void; onDone: (next?: CandidateStatus) => void
}) {
  const toast = useToast()
  const candidate = editor.mode === 'new' ? null : editor.candidate
  const approving = editor.mode === 'approve'
  const [content, setContent] = useState(candidate?.draftContent ?? '')
  const [communityId, setCommunityId] = useState(candidate?.targetCommunityId ?? '')
  const [when, setWhen] = useState(toLocalInput(candidate?.scheduledAt ?? null))
  const [images, setImages] = useState<string[]>(candidate?.images ?? [])
  const [uploading, setUploading] = useState(0)
  const communities = useQuery({
    queryKey: ['communities', ''], enabled: approving,
    queryFn: () => api<AdminCommunity[]>('api/admin/communities'),
  })

  const save = useMutation({
    mutationFn: async () => {
      if (editor.mode === 'new') {
        await api('api/admin/editorial/candidates', { method: 'POST', body: { content, images } })
        return 'Drafted' as const
      }
      if (editor.mode === 'draft') {
        await api(`api/admin/editorial/candidates/${editor.candidate.id}/draft`, { method: 'PUT', body: { content, images } })
        return 'Drafted' as const
      }
      await api(`api/admin/editorial/candidates/${editor.candidate.id}/approve`, {
        method: 'POST',
        body: { content, communityId: communityId || null, scheduledAt: when ? new Date(when).toISOString() : null, images },
      })
      // Sans tâches automatiques : publication immédiate depuis la console
      if (!when && publishNow) {
        await api('api/admin/editorial/run/publish', { method: 'POST' })
        return 'Published' as const
      }
      return 'Approved' as const
    },
    onSuccess: (next) => {
      toast(next === 'Published' ? 'Publication en ligne.'
        : next === 'Approved' ? (when ? (publishNow ? 'Programmée : utilisez « Publier maintenant » à l’heure prévue (tâches automatiques désactivées).' : 'Publication programmée.')
          : 'Publication validée : en ligne dans quelques minutes.')
          : 'Brouillon enregistré, à valider.')
      onDone(next)
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  // Photos envoyées une à une, dans l'ordre choisi (la première sert de couverture)
  const addImages = async (files: FileList | null) => {
    const list = Array.from(files ?? []).slice(0, MAX_IMAGES - images.length)
    if ((files?.length ?? 0) > list.length) toast(`${MAX_IMAGES} photos au plus par publication.`, 'error')
    for (const file of list) {
      setUploading((n) => n + 1)
      try {
        const form = new FormData()
        form.append('file', file)
        const { path } = await api<{ path: string }>('api/admin/editorial/images', { method: 'POST', body: form })
        setImages((old) => [...old, path])
      } catch (e) {
        toast(`${file.name} : ${errorMessage(e)}`, 'error')
      } finally {
        setUploading((n) => n - 1)
      }
    }
  }
  const move = (from: number, to: number) => setImages((old) => {
    const next = [...old]
    const [item] = next.splice(from, 1)
    next.splice(to, 0, item)
    return next
  })

  const length = content.trim().length
  const invalid = length < 3 || length > maxLength || uploading > 0
  return (
    <Modal open wide title={approving ? 'Relire et programmer' : editor.mode === 'new' ? 'Nouvelle publication' : 'Rédiger'} onClose={onClose} footer={
      <>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" loading={save.isPending} disabled={invalid} onClick={() => save.mutate()}>
          {approving ? (when ? 'Valider et programmer' : 'Valider et publier') : 'Enregistrer le brouillon'}
        </Button>
      </>
    }>
      {candidate && !candidate.isManual && (
        <div className="mb-4 rounded-lg bg-surface-2 px-3 py-2 text-sm">
          <p className="font-medium">{candidate.sourceTitle}</p>
          {candidate.sourceExcerpt && <p className="mt-1 text-muted">{candidate.sourceExcerpt}</p>}
          <a href={candidate.sourceUrl} target="_blank" rel="noreferrer" className="mt-1 inline-flex items-center gap-1 text-xs text-brand hover:underline">
            Lire l'article <ExternalLink className="size-3" />
          </a>
        </div>
      )}
      <Textarea label="Texte publié" rows={9} value={content} onChange={(e) => setContent(e.target.value)}
        placeholder="Texte de la publication. Citez la source et ajoutez son lien pour un article repris." />
      <p className={cx('mt-1 text-right text-xs', length > maxLength ? 'text-danger' : 'text-muted')}>{length} / {maxLength}</p>

      <div className="mt-2">
        <span className="mb-1.5 block text-sm font-medium">Photos <span className="font-normal text-muted">({images.length} / {MAX_IMAGES}, la première sert de couverture)</span></span>
        <div className="flex flex-wrap gap-2">
          {images.map((path, i) => (
            <div key={path} className="group relative size-24 overflow-hidden rounded-lg border border-line bg-surface-2">
              <img src={imageUrl(path)} alt={`Photo ${i + 1}`} className="size-full object-cover" />
              <div className="absolute inset-x-0 bottom-0 flex justify-between bg-black/55 px-1 py-0.5 text-white opacity-0 transition group-hover:opacity-100 group-focus-within:opacity-100">
                <button type="button" aria-label="Avancer la photo" disabled={i === 0} onClick={() => move(i, i - 1)} className="px-1 disabled:opacity-30"><ChevronLeft className="size-4" /></button>
                <button type="button" aria-label="Retirer la photo" onClick={() => setImages((old) => old.filter((p) => p !== path))} className="px-1"><X className="size-4" /></button>
                <button type="button" aria-label="Reculer la photo" disabled={i === images.length - 1} onClick={() => move(i, i + 1)} className="px-1 disabled:opacity-30"><ChevronRight className="size-4" /></button>
              </div>
            </div>
          ))}
          {Array.from({ length: uploading }, (_, i) => (
            <div key={`u${i}`} className="grid size-24 place-items-center rounded-lg border border-dashed border-line text-xs text-muted">Envoi…</div>
          ))}
          {images.length + uploading < MAX_IMAGES && (
            <label className="grid size-24 cursor-pointer place-items-center rounded-lg border border-dashed border-line text-muted hover:border-brand hover:text-brand">
              <span className="flex flex-col items-center gap-1 text-xs"><ImagePlus className="size-5" />Ajouter</span>
              <input type="file" accept="image/jpeg,image/png,image/webp,image/heic" multiple className="sr-only"
                onChange={(e) => { void addImages(e.target.files); e.target.value = '' }} />
            </label>
          )}
        </div>
        <p className="mt-1 text-xs text-muted">JPEG, PNG, WebP ou HEIC, 10 Mo au plus. Uniquement des visuels dont BéninConnect détient les droits.</p>
      </div>
      {approving && (
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <Select label="Communauté (facultatif)" value={communityId} onChange={(e) => setCommunityId(e.target.value)}>
            <option value="">Fil général</option>
            {(communities.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
          <Input label="Publier le (vide : dès que possible)" type="datetime-local" value={when} onChange={(e) => setWhen(e.target.value)} />
        </div>
      )}
      {approving && <p className="mt-3 text-xs text-muted">Vous validez ce texte en tant que responsable éditorial : votre nom figure au journal d'audit.</p>}
    </Modal>
  )
}

/** Photo publiée (uploads/…) : servie par l'API, sur le même domaine que la console. */
const imageUrl = (path: string) => apiUrl(path)

function Thumbnails({ paths }: { paths: string[] }) {
  return (
    <div className="mt-2 flex gap-1.5">
      {paths.slice(0, 5).map((p) => <img key={p} src={imageUrl(p)} alt="" className="size-14 rounded-md border border-line object-cover" />)}
      {paths.length > 5 && <span className="grid size-14 place-items-center rounded-md bg-surface-2 text-xs text-muted">+{paths.length - 5}</span>}
    </div>
  )
}

/** Flux RSS / Atom suivis : ajout, pause, suppression. */
function Sources({ overview, onChanged }: { overview: EditorialOverview; onChanged: () => void }) {
  const toast = useToast()
  const [name, setName] = useState('')
  const [url, setUrl] = useState('')
  const [removing, setRemoving] = useState<string | null>(null)
  const onError = (e: unknown) => toast(errorMessage(e), 'error')

  const add = useMutation({
    mutationFn: () => api('api/admin/editorial/sources', { method: 'POST', body: { name, url } }),
    onSuccess: () => { setName(''); setUrl(''); toast('Source ajoutée.'); onChanged() },
    onError,
  })
  const update = useMutation({
    mutationFn: (s: { id: string; name: string; url: string; isEnabled: boolean }) =>
      api(`api/admin/editorial/sources/${s.id}`, { method: 'PUT', body: s }),
    onSuccess: onChanged,
    onError,
  })
  const remove = useMutation({
    mutationFn: (id: string) => api(`api/admin/editorial/sources/${id}`, { method: 'DELETE' }),
    onSuccess: () => { setRemoving(null); onChanged() },
    onError,
  })

  return (
    <Card className="mt-6">
      <div className="border-b border-line px-5 py-4">
        <h2 className="flex items-center gap-2 font-semibold"><Rss className="size-4 text-brand" />Sources</h2>
        <p className="mt-1 text-sm text-muted">
          Flux RSS ou Atom. Privilégiez les sources officielles (institutions, ambassades, tourisme) ou les médias qui autorisent la reprise.
        </p>
      </div>
      {overview.sources.length === 0 ? <Empty title="Aucune source" description="Ajoutez un premier flux ci-dessous." /> : (
        <ul className="divide-y divide-line">
          {overview.sources.map((s) => (
            <li key={s.id} className="flex flex-col gap-3 px-5 py-3 sm:flex-row sm:items-center">
              <div className="min-w-0 flex-1">
                <p className="font-medium">{s.name} {!s.isEnabled && <Badge>En pause</Badge>}</p>
                <p className="truncate text-xs text-muted">{s.url}</p>
                <p className={cx('mt-0.5 text-xs', s.lastError ? 'text-danger' : 'text-muted')}>
                  {s.lastError ? `Erreur : ${s.lastError}` : s.lastFetchedAt ? `Lu ${fmtRelative(s.lastFetchedAt)} · ${s.collectedCount} article(s) collecté(s)` : 'Jamais lu'}
                </p>
              </div>
              <div className="flex gap-2">
                <Button onClick={() => update.mutate({ id: s.id, name: s.name, url: s.url, isEnabled: !s.isEnabled })}>{s.isEnabled ? 'Mettre en pause' : 'Réactiver'}</Button>
                <Button variant="dangerSoft" aria-label={`Supprimer ${s.name}`} onClick={() => setRemoving(s.id)}><Trash2 className="size-4" /></Button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <form className="grid gap-3 border-t border-line px-5 py-4 sm:grid-cols-[1fr_2fr_auto] sm:items-end"
        onSubmit={(e) => { e.preventDefault(); add.mutate() }}>
        <Input label="Nom" placeholder="Ex. : Office du tourisme" value={name} onChange={(e) => setName(e.target.value)} />
        <Input label="Adresse du flux" placeholder="https://…/rss" value={url} onChange={(e) => setUrl(e.target.value)} />
        <Button type="submit" variant="primary" loading={add.isPending} disabled={name.trim().length < 2 || !url.trim()}>Ajouter</Button>
      </form>
      <ConfirmDialog open={!!removing} title="Supprimer la source" confirmLabel="Supprimer" danger busy={remove.isPending}
        message="Les articles déjà collectés restent dans l'historique." onClose={() => setRemoving(null)}
        onConfirm={() => removing && remove.mutate(removing)} />
    </Card>
  )
}
