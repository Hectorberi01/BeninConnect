import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Eye, EyeOff, FileText, Star, X } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, cx, Empty, ErrorBox, Loading, PageHeader, Pagination, Select } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { countryName, fmtBytes, fmtDateTime, fmtRelative } from '@/lib/format'
import type {
  AdminProvider, AdminServiceDocument, AdminServiceOffering, AdminServiceReport, AdminServiceReview, ModerationView, Paged, ServicesOverview,
} from '@/lib/types'
import {
  Avatar, DOCUMENT_LABELS, HIDDEN_LABELS, LEGAL_LABELS, OfferingCard, openPrivateFile, ProviderModal, ProviderStatusBadge, VERIFICATION_LABELS,
} from './shared'

type Tab = 'profiles' | 'offerings' | 'reviews' | 'reports' | 'documents'
const TAKE = 20

export function ServicesModerationPage() {
  const [params, setParams] = useSearchParams()
  const tab = (params.get('tab') as Tab) || 'profiles'
  const overview = useQuery({ queryKey: ['services'], queryFn: () => api<ServicesOverview>('api/admin/services') })
  const o = overview.data
  const tabs: { key: Tab; label: string; count?: number }[] = [
    { key: 'profiles', label: 'Profils', count: o ? o.pendingProfiles + o.hiddenProfiles : undefined },
    { key: 'offerings', label: 'Offres', count: o?.offeringsToReview },
    { key: 'reviews', label: 'Avis', count: o?.reviewsToReview },
    { key: 'reports', label: 'Signalements', count: o?.openReports },
    { key: 'documents', label: 'Justificatifs', count: o?.pendingDocuments },
  ]

  return (
    <>
      <PageHeader title="Modération des services" description="Profils à valider, contenus masqués automatiquement, signalements et justificatifs." />
      <div className="mb-4 flex flex-wrap gap-2" role="tablist">
        {tabs.map((t) => (
          <button key={t.key} role="tab" aria-selected={tab === t.key} onClick={() => setParams({ tab: t.key })}
            className={cx('rounded-full px-3.5 py-1.5 text-sm font-medium', tab === t.key ? 'bg-brand text-white' : 'border border-line bg-surface text-muted hover:text-fg')}>
            {t.label}{t.count ? ` · ${t.count}` : ''}
          </button>
        ))}
      </div>
      {tab === 'profiles' && <Profiles />}
      {tab === 'offerings' && <Offerings />}
      {tab === 'reviews' && <Reviews />}
      {tab === 'reports' && <Reports />}
      {tab === 'documents' && <Documents />}
    </>
  )
}

function useRefresh() {
  const qc = useQueryClient()
  return () => ['services', 'services-providers', 'services-offerings', 'services-reviews', 'services-reports', 'services-documents', 'services-provider']
    .forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
}

function List<T>({ query, empty, children, skip, setSkip }: {
  query: { isPending: boolean; error: unknown; data?: Paged<T>; refetch: () => void }; empty: string; children: (item: T) => React.ReactNode
  skip: number; setSkip: (n: number) => void
}) {
  if (query.isPending) return <Loading />
  if (query.error) return <ErrorBox message={errorMessage(query.error)} onRetry={query.refetch} />
  if (!query.data || query.data.items.length === 0) return <Empty title="Rien à traiter" description={empty} />
  return (
    <>
      <ul className="divide-y divide-line">{query.data.items.map(children)}</ul>
      <Pagination skip={skip} take={TAKE} total={query.data.totalCount} onChange={setSkip} />
    </>
  )
}

function Profiles() {
  const [view, setView] = useState<'Review' | 'Pending' | 'Hidden' | 'Suspended'>('Review')
  const [skip, setSkip] = useState(0)
  const [open, setOpen] = useState<string | null>(null)
  const query = useQuery({
    queryKey: ['services-providers', view, skip],
    queryFn: () => api<Paged<AdminProvider>>(`api/admin/services/providers${qs({ view, skip, take: TAKE })}`),
  })
  return (
    <Card>
      <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
        <Select label="Afficher" value={view} onChange={(e) => { setView(e.target.value as typeof view); setSkip(0) }} className="w-64">
          <option value="Review">À traiter (validation + masqués)</option>
          <option value="Pending">Premières publications à valider</option>
          <option value="Hidden">Masqués automatiquement</option>
          <option value="Suspended">Suspendus</option>
        </Select>
        <p className="pb-2 text-sm text-muted">La première publication d’un profil est toujours validée par l’équipe. Les plus anciens envois d’abord.</p>
      </div>
      <List query={query} skip={skip} setSkip={setSkip} empty="Aucun profil en attente.">
        {(p) => (
          <li key={p.id} className="flex cursor-pointer items-center gap-3 px-5 py-3 hover:bg-surface-2" onClick={() => setOpen(p.id)}>
            <Avatar path={p.photoPath} name={p.displayName} />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-medium">{p.displayName}</p>
                <ProviderStatusBadge p={p} />
                {p.pendingDocuments > 0 && <Badge tone="info">{p.pendingDocuments} justificatif(s)</Badge>}
                {p.reportCount > 0 && <Badge tone="warning">{p.reportCount} signalement(s)</Badge>}
              </div>
              <p className="truncate text-sm text-muted">
                {p.memberName} · {p.city}, {countryName(p.country)} · {LEGAL_LABELS[p.legalStatus]} · {p.offerings} offre(s)
                {p.submittedAt && ` · envoyé ${fmtRelative(p.submittedAt)}`}
              </p>
            </div>
            <span className="text-sm text-brand">Examiner</span>
          </li>
        )}
      </List>
      {open && <ProviderModal id={open} onClose={() => setOpen(null)} />}
    </Card>
  )
}

function Offerings() {
  const toast = useToast()
  const refresh = useRefresh()
  const [view, setView] = useState<ModerationView>('Review')
  const [skip, setSkip] = useState(0)
  const [hiding, setHiding] = useState<AdminServiceOffering | null>(null)
  const [open, setOpen] = useState<string | null>(null)
  const query = useQuery({
    queryKey: ['services-offerings', view, skip],
    queryFn: () => api<Paged<AdminServiceOffering>>(`api/admin/services/offerings${qs({ view, skip, take: TAKE })}`),
  })
  const decide = useMutation({
    mutationFn: ({ id, show, reason }: { id: string; show: boolean; reason?: string }) =>
      api(`api/admin/services/offerings/${id}/${show ? 'show' : 'hide'}`, { method: 'POST', body: { reason } }),
    onSuccess: (_, v) => { toast(v.show ? 'Offre réaffichée.' : 'Offre masquée.', 'success'); setHiding(null); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Card>
      <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
        <Select label="Afficher" value={view} onChange={(e) => { setView(e.target.value as ModerationView); setSkip(0) }} className="w-64">
          <option value="Review">À revoir (mot-clé, signalements)</option>
          <option value="Hidden">Toutes les offres masquées</option>
          <option value="All">Toutes les offres</option>
        </Select>
        <p className="pb-2 text-sm text-muted">Les offres sont publiées directement, sauf si un mot-clé d’alerte ou des signalements les masquent.</p>
      </div>
      <List query={query} skip={skip} setSkip={setSkip} empty="Aucune offre à revoir.">
        {(o) => (
          <li key={o.offering.id} className="px-5 py-3">
            <p className="mb-1 text-sm text-muted">
              <button className="text-brand hover:underline" onClick={() => setOpen(o.providerId)}>{o.providerName}</button>
              {' '}· modifiée {fmtRelative(o.updatedAt)}{o.reportCount > 0 && ` · ${o.reportCount} signalement(s)`}{o.moderationNote && ` · ${o.moderationNote}`}
            </p>
            <OfferingCard o={o.offering} actions={<>
              {o.offering.hiddenReason !== 'None' &&
                <Button variant="primary" icon={<Eye className="size-4" />} onClick={() => decide.mutate({ id: o.offering.id, show: true })}>Réafficher</Button>}
              {o.offering.hiddenReason !== 'Admin' &&
                <Button variant="dangerSoft" icon={<EyeOff className="size-4" />} onClick={() => setHiding(o)}>
                  {o.offering.hiddenReason === 'None' ? 'Masquer' : 'Garder masquée'}
                </Button>}
            </>} />
          </li>
        )}
      </List>
      <ConfirmDialog open={!!hiding} title="Masquer cette offre" confirmLabel="Masquer" danger busy={decide.isPending}
        message={`« ${hiding?.offering.title ?? ''} » ne sera plus visible. Le prestataire garde son profil.`}
        reason={{ label: 'Motif', required: true, placeholder: 'Ex. : demande de paiement d’avance, service interdit…' }}
        onClose={() => setHiding(null)} onConfirm={(reason) => hiding && decide.mutate({ id: hiding.offering.id, show: false, reason })} />
      {open && <ProviderModal id={open} onClose={() => setOpen(null)} />}
    </Card>
  )
}

function Stars({ n }: { n: number }) {
  return <span className="inline-flex text-warning">{Array.from({ length: 5 }, (_, i) => <Star key={i} className={cx('size-4', i < n && 'fill-current')} />)}</span>
}

function Reviews() {
  const toast = useToast()
  const refresh = useRefresh()
  const [view, setView] = useState<ModerationView>('Review')
  const [skip, setSkip] = useState(0)
  const [hiding, setHiding] = useState<AdminServiceReview | null>(null)
  const query = useQuery({
    queryKey: ['services-reviews', view, skip],
    queryFn: () => api<Paged<AdminServiceReview>>(`api/admin/services/reviews${qs({ view, skip, take: TAKE })}`),
  })
  const decide = useMutation({
    mutationFn: ({ id, show, reason }: { id: string; show: boolean; reason?: string }) =>
      api(`api/admin/services/reviews/${id}/${show ? 'show' : 'hide'}`, { method: 'POST', body: { reason } }),
    onSuccess: (_, v) => { toast(v.show ? 'Avis maintenu.' : 'Avis masqué.', 'success'); setHiding(null); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Card>
      <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
        <Select label="Afficher" value={view} onChange={(e) => { setView(e.target.value as ModerationView); setSkip(0) }} className="w-56">
          <option value="Review">Signalés</option>
          <option value="Hidden">Masqués</option>
          <option value="All">Tous les avis</option>
        </Select>
        <p className="max-w-xl pb-2 text-sm text-muted">
          Un avis n’est jamais retiré parce qu’il est négatif. Masquage seulement pour un motif de modération, conservé avec l’avis.
        </p>
      </div>
      <List query={query} skip={skip} setSkip={setSkip} empty="Aucun avis signalé.">
        {(r) => (
          <li key={r.id} className="flex flex-col gap-3 px-5 py-3 lg:flex-row lg:items-start">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <Stars n={r.rating} />
                <span className="text-sm">{r.authorName} → <b>{r.providerName}</b></span>
                <span className="text-xs text-muted">{fmtDateTime(r.createdAt)}</span>
                {r.status === 'Hidden' && <Badge tone="danger">Masqué · {HIDDEN_LABELS[r.hiddenReason]}</Badge>}
                {r.reportCount > 0 && <Badge tone="warning">{r.reportCount} signalement(s)</Badge>}
              </div>
              {r.comment && <p className="mt-1 whitespace-pre-wrap text-sm">{r.comment}</p>}
              {r.providerReply && <p className="mt-1 border-l-2 border-line pl-3 text-sm text-muted">Réponse : {r.providerReply}</p>}
              {r.moderationNote && <p className="mt-1 text-xs text-muted">Motif de modération : {r.moderationNote}</p>}
            </div>
            <div className="flex gap-2 lg:w-48 lg:flex-col">
              {(r.status === 'Hidden' || r.reportCount > 0) &&
                <Button variant="primary" icon={<Eye className="size-4" />} onClick={() => decide.mutate({ id: r.id, show: true })}>
                  {r.status === 'Hidden' ? 'Réafficher' : 'Maintenir'}
                </Button>}
              {r.status === 'Published' && <Button variant="dangerSoft" icon={<EyeOff className="size-4" />} onClick={() => setHiding(r)}>Masquer</Button>}
            </div>
          </li>
        )}
      </List>
      <ConfirmDialog open={!!hiding} title="Masquer cet avis" confirmLabel="Masquer" danger busy={decide.isPending}
        message="Indiquez le motif de modération (propos injurieux, données personnelles, avis sans rapport…). Il reste attaché à l’avis et au journal d’audit."
        reason={{ label: 'Motif de modération (10 caractères au moins)', required: true }}
        onClose={() => setHiding(null)} onConfirm={(reason) => hiding && decide.mutate({ id: hiding.id, show: false, reason })} />
    </Card>
  )
}

const TARGET_LABELS = { Profile: 'Profil', Offering: 'Offre', Review: 'Avis' }

function Reports() {
  const toast = useToast()
  const refresh = useRefresh()
  const [handled, setHandled] = useState(false)
  const [skip, setSkip] = useState(0)
  const [open, setOpen] = useState<string | null>(null)
  const query = useQuery({
    queryKey: ['services-reports', handled, skip],
    queryFn: () => api<Paged<AdminServiceReport>>(`api/admin/services/reports${qs({ handled, skip, take: TAKE })}`),
  })
  const close = useMutation({
    mutationFn: (id: string) => api(`api/admin/services/reports/${id}/close`, { method: 'POST', body: { reason: 'Classé sans suite' } }),
    onSuccess: () => { toast('Signalement classé.', 'success'); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Card>
      <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
        <Select label="Afficher" value={String(handled)} onChange={(e) => { setHandled(e.target.value === 'true'); setSkip(0) }} className="w-48">
          <option value="false">À traiter</option>
          <option value="true">Traités</option>
        </Select>
        <p className="pb-2 text-sm text-muted">Au seuil réglé (3 par défaut), la cible est masquée automatiquement en attendant votre décision.</p>
      </div>
      <List query={query} skip={skip} setSkip={setSkip} empty="Aucun signalement.">
        {(r) => (
          <li key={r.id} className="flex flex-col gap-2 px-5 py-3 sm:flex-row sm:items-center">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <Badge tone="neutral">{TARGET_LABELS[r.targetType]}</Badge>
                <span className="font-medium">{r.targetLabel}</span>
                <span className="text-xs text-muted">{fmtDateTime(r.createdAt)} · par {r.authorName}</span>
              </div>
              <p className="mt-1 text-sm">{r.reason}</p>
              {r.decision && <p className="text-xs text-muted">Décision : {r.decision}</p>}
            </div>
            <div className="flex gap-2">
              {r.providerId && <Button onClick={() => setOpen(r.providerId!)}>Voir le prestataire</Button>}
              {!r.handled && <Button variant="ghost" onClick={() => close.mutate(r.id)}>Classer</Button>}
            </div>
          </li>
        )}
      </List>
      {open && <ProviderModal id={open} onClose={() => setOpen(null)} />}
    </Card>
  )
}

function Documents() {
  const toast = useToast()
  const refresh = useRefresh()
  const [status, setStatus] = useState<'Pending' | 'Approved' | 'Rejected'>('Pending')
  const [skip, setSkip] = useState(0)
  const [rejecting, setRejecting] = useState<AdminServiceDocument | null>(null)
  const [open, setOpen] = useState<string | null>(null)
  const query = useQuery({
    queryKey: ['services-documents', status, skip],
    queryFn: () => api<Paged<AdminServiceDocument>>(`api/admin/services/documents${qs({ status, skip, take: TAKE })}`),
  })
  const decide = useMutation({
    mutationFn: ({ id, approve, reason }: { id: string; approve: boolean; reason?: string }) =>
      api<AdminServiceDocument>(`api/admin/services/documents/${id}/${approve ? 'approve' : 'reject'}`, { method: 'POST', body: { reason } }),
    onSuccess: (d, v) => {
      toast(v.approve ? `Justificatif validé : ${VERIFICATION_LABELS[d.verificationLevel]}.` : 'Justificatif refusé.', 'success')
      setRejecting(null)
      refresh()
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Card>
      <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
        <Select label="Afficher" value={status} onChange={(e) => { setStatus(e.target.value as typeof status); setSkip(0) }} className="w-48">
          <option value="Pending">À vérifier</option>
          <option value="Approved">Validés</option>
          <option value="Rejected">Refusés</option>
        </Select>
        <p className="max-w-2xl pb-2 text-sm text-muted">
          Pièce d’identité validée : « Identité vérifiée ». Diplôme ou inscription au registre validé + SIRET ou IFU renseigné : « Pro vérifié ».
          Fichiers privés : ouverture tracée dans le journal d’audit.
        </p>
      </div>
      <List query={query} skip={skip} setSkip={setSkip} empty="Aucun justificatif à vérifier.">
        {(d) => (
          <li key={d.document.id} className="flex flex-col gap-2 px-5 py-3 sm:flex-row sm:items-center">
            <FileText className="size-5 shrink-0 text-muted" />
            <div className="min-w-0 flex-1">
              <p className="text-sm">
                <button className="font-medium text-brand hover:underline" onClick={() => void openPrivateFile(`api/admin/services/documents/${d.document.id}/file`)}>
                  {DOCUMENT_LABELS[d.document.kind]} — {d.document.originalName}
                </button>
                <span className="text-muted"> · {fmtBytes(d.document.size)} · envoyé {fmtRelative(d.document.uploadedAt)}</span>
              </p>
              <p className="text-sm text-muted">
                <button className="hover:underline" onClick={() => setOpen(d.providerId)}>{d.providerName}</button> · {LEGAL_LABELS[d.legalStatus]}
                {' '}· {d.siret ? `SIRET ${d.siret}` : d.ifu ? `IFU ${d.ifu}` : 'sans SIRET ni IFU'} · {VERIFICATION_LABELS[d.verificationLevel]}
              </p>
              {d.document.reviewNote && <p className="text-xs text-muted">Motif : {d.document.reviewNote}</p>}
            </div>
            {d.document.status === 'Pending' && (
              <div className="flex gap-2">
                <Button variant="dangerSoft" icon={<X className="size-4" />} onClick={() => setRejecting(d)}>Refuser</Button>
                <Button variant="primary" icon={<Check className="size-4" />} loading={decide.isPending && decide.variables?.id === d.document.id}
                  onClick={() => decide.mutate({ id: d.document.id, approve: true })}>Valider</Button>
              </div>
            )}
          </li>
        )}
      </List>
      <ConfirmDialog open={!!rejecting} title="Refuser ce justificatif" confirmLabel="Refuser" danger busy={decide.isPending}
        message="Le prestataire reçoit votre motif et peut envoyer un autre document."
        reason={{ label: 'Motif', required: true, placeholder: 'Ex. : document illisible, nom différent du profil…' }}
        onClose={() => setRejecting(null)} onConfirm={(reason) => rejecting && decide.mutate({ id: rejecting.document.id, approve: false, reason })} />
      {open && <ProviderModal id={open} onClose={() => setOpen(null)} />}
    </Card>
  )
}
