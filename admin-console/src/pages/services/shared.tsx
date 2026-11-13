import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BadgeCheck, Ban, Check, FileText, MapPin, RotateCcw, ShieldAlert, ShieldOff, X } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, ConfirmDialog, ErrorBox, Loading, Modal } from '@/components/ui'
import { api, apiUrl, errorMessage } from '@/lib/api'
import { countryName, fmtDateTime, fmtRelative } from '@/lib/format'
import type {
  AdminProvider, AdminProviderDetail, DocumentKind, LegalStatus, PriceType, ProviderStatus, ServiceHiddenReason, ServiceOffering, VerificationLevel,
} from '@/lib/types'

export const STATUS_LABELS: Record<ProviderStatus, string> = {
  Draft: 'Brouillon', PendingReview: 'À valider', Active: 'En ligne', Suspended: 'Suspendu',
}

export const HIDDEN_LABELS: Record<ServiceHiddenReason, string> = {
  None: '', AlertKeyword: 'Mot-clé d’alerte', Reports: 'Signalements', Admin: 'Masqué par l’équipe',
}

export const LEGAL_LABELS: Record<LegalStatus, string> = { Individual: 'Particulier', MicroEntrepreneur: 'Micro-entrepreneur', Company: 'Société' }

export const DOCUMENT_LABELS: Record<DocumentKind, string> = {
  Identity: 'Pièce d’identité', Qualification: 'Diplôme / qualification', Registration: 'Inscription au registre',
}

export const VERIFICATION_LABELS: Record<VerificationLevel, string> = { None: 'Non vérifié', Identity: 'Identité vérifiée', Pro: 'Pro vérifié' }

const PRICE_SUFFIX: Record<PriceType, string> = { Fixed: '', From: 'à partir de ', Hourly: '/ heure', Quote: '' }

export function priceText(o: Pick<ServiceOffering, 'priceType' | 'priceAmount' | 'currency'>) {
  if (o.priceType === 'Quote' || o.priceAmount == null) return 'Sur devis'
  const amount = new Intl.NumberFormat('fr-FR', { style: 'currency', currency: o.currency, maximumFractionDigits: o.currency === 'XOF' ? 0 : 2 }).format(o.priceAmount)
  return o.priceType === 'From' ? `${PRICE_SUFFIX.From}${amount}` : o.priceType === 'Hourly' ? `${amount} ${PRICE_SUFFIX.Hourly}` : amount
}

export const photoUrl = (path: string) => (/^https?:/.test(path) ? path : apiUrl(path))

/** Fichier privé (justificatif) : récupéré avec le jeton puis ouvert dans un nouvel onglet. */
export async function openPrivateFile(path: string) {
  const response = await api<Response>(path, { raw: true })
  const url = URL.createObjectURL(await response.blob())
  window.open(url, '_blank', 'noopener')
  setTimeout(() => URL.revokeObjectURL(url), 60_000)
}

export function ProviderStatusBadge({ p }: { p: Pick<AdminProvider, 'status' | 'hiddenReason'> }) {
  if (p.status === 'Suspended') return <Badge tone="danger">Suspendu</Badge>
  if (p.hiddenReason !== 'None') return <Badge tone="warning">Masqué · {HIDDEN_LABELS[p.hiddenReason]}</Badge>
  return <Badge tone={p.status === 'Active' ? 'brand' : p.status === 'PendingReview' ? 'warning' : 'neutral'}>{STATUS_LABELS[p.status]}</Badge>
}

export function VerificationBadge({ level }: { level: VerificationLevel }) {
  if (level === 'None') return null
  return <Badge tone={level === 'Pro' ? 'brand' : 'info'}><BadgeCheck className="mr-1 inline size-3.5" />{VERIFICATION_LABELS[level]}</Badge>
}

export function Avatar({ path, name, size = 40 }: { path: string | null; name: string; size?: number }) {
  return path
    ? <img src={photoUrl(path)} alt="" className="shrink-0 rounded-full object-cover" style={{ width: size, height: size }} />
    : <span className="grid shrink-0 place-items-center rounded-full bg-brand-soft font-semibold text-brand" style={{ width: size, height: size }}>
      {name.slice(0, 1).toUpperCase()}
    </span>
}

export function OfferingCard({ o, actions }: { o: ServiceOffering; actions?: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-line p-3">
      <div className="flex flex-wrap items-center gap-2">
        <p className="font-medium">{o.title}</p>
        <Badge tone="neutral">{o.categoryName}</Badge>
        {o.requiresQualification && <Badge tone="warning">Métier réglementé</Badge>}
        {!o.isActive && <Badge tone="neutral">Désactivée</Badge>}
        {o.hiddenReason !== 'None' && <Badge tone="danger">{HIDDEN_LABELS[o.hiddenReason]}{o.alertKeyword ? ` « ${o.alertKeyword} »` : ''}</Badge>}
        <span className="ml-auto text-sm font-medium">{priceText(o)}</span>
      </div>
      {o.description && <p className="mt-1 line-clamp-3 whitespace-pre-wrap text-sm text-muted">{o.description}</p>}
      {o.images.length > 0 && (
        <div className="mt-2 flex gap-1.5">
          {o.images.map((p) => <img key={p} src={photoUrl(p)} alt="" className="size-14 rounded-md border border-line object-cover" />)}
        </div>
      )}
      {actions && <div className="mt-2 flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}

type Action = { verb: 'approve' | 'reject' | 'suspend' | 'reinstate' | 'revoke-verification'; title: string; label: string; danger?: boolean; reason?: boolean }

const ACTIONS: Record<Action['verb'], Action> = {
  approve: { verb: 'approve', title: 'Valider ce profil', label: 'Valider et publier' },
  reject: { verb: 'reject', title: 'Demander des corrections', label: 'Renvoyer en brouillon', reason: true },
  suspend: { verb: 'suspend', title: 'Suspendre ce prestataire', label: 'Suspendre', danger: true, reason: true },
  reinstate: { verb: 'reinstate', title: 'Rétablir ce prestataire', label: 'Rétablir' },
  'revoke-verification': { verb: 'revoke-verification', title: 'Retirer la vérification', label: 'Retirer le badge', danger: true },
}

/** Fiche complète d'un prestataire avec les décisions de modération. */
export function ProviderModal({ id, onClose }: { id: string; onClose: () => void }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [action, setAction] = useState<Action | null>(null)
  const detail = useQuery({ queryKey: ['services-provider', id], queryFn: () => api<AdminProviderDetail>(`api/admin/services/providers/${id}`) })
  const decide = useMutation({
    mutationFn: ({ verb, reason }: { verb: Action['verb']; reason?: string }) =>
      api<AdminProviderDetail>(`api/admin/services/providers/${id}/${verb}`, { method: 'POST', body: { reason } }),
    onSuccess: (d) => {
      qc.setQueryData(['services-provider', id], d)
      ;['services', 'services-providers', 'services-reports'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
      setAction(null)
      toast('Décision enregistrée.', 'success')
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const d = detail.data
  const p = d?.provider

  return (
    <Modal open wide title={p?.displayName ?? 'Prestataire'} onClose={onClose}
      footer={p && <>
        {p.status === 'PendingReview' && <>
          <Button variant="dangerSoft" icon={<X className="size-4" />} onClick={() => setAction(ACTIONS.reject)}>Demander des corrections</Button>
          <Button variant="primary" icon={<Check className="size-4" />} onClick={() => setAction(ACTIONS.approve)}>Valider et publier</Button>
        </>}
        {(p.status === 'Suspended' || p.hiddenReason !== 'None') &&
          <Button variant="primary" icon={<RotateCcw className="size-4" />} onClick={() => setAction(ACTIONS.reinstate)}>Rétablir</Button>}
        {p.verificationLevel !== 'None' &&
          <Button icon={<ShieldOff className="size-4" />} onClick={() => setAction(ACTIONS['revoke-verification'])}>Retirer la vérification</Button>}
        {p.status !== 'Suspended' && <Button variant="danger" icon={<Ban className="size-4" />} onClick={() => setAction(ACTIONS.suspend)}>Suspendre</Button>}
      </>}>
      {detail.isPending ? <Loading /> : detail.error ? <ErrorBox message={errorMessage(detail.error)} /> : d && p && (
        <div className="space-y-5">
          <div className="flex items-start gap-3">
            <Avatar path={p.photoPath} name={p.displayName} size={56} />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <ProviderStatusBadge p={p} />
                <VerificationBadge level={p.verificationLevel} />
                {p.alertKeyword && <Badge tone="warning"><ShieldAlert className="mr-1 inline size-3.5" />« {p.alertKeyword} »</Badge>}
                {p.reportCount > 0 && <Badge tone="warning">{p.reportCount} signalement(s)</Badge>}
              </div>
              <p className="mt-1 text-sm text-muted">Membre : {p.memberName} · <MapPin className="inline size-3.5" /> {p.city}, {countryName(p.country)}</p>
              {d.reviewNote && <p className="mt-2 rounded-lg bg-surface-2 px-3 py-2 text-sm">Dernière décision : {d.reviewNote}</p>}
            </div>
          </div>

          {d.bio && <p className="whitespace-pre-wrap text-sm">{d.bio}</p>}

          <dl className="grid grid-cols-[12rem_1fr] gap-x-4 gap-y-1 text-sm">
            <dt className="text-muted">Statut juridique</dt><dd>{LEGAL_LABELS[p.legalStatus]}</dd>
            <dt className="text-muted">SIRET / IFU</dt><dd>{d.siret ?? d.ifu ?? '—'}</dd>
            <dt className="text-muted">Téléphone</dt><dd>{d.phone ? `${d.phone}${d.phoneIsPublic ? ' (public)' : ' (après acceptation)'}` : '—'}</dd>
            <dt className="text-muted">Contact préféré</dt><dd>{{ InApp: 'Messagerie', Phone: 'Téléphone', WhatsApp: 'WhatsApp' }[d.contactPreference]}</dd>
            <dt className="text-muted">Zone</dt>
            <dd>{[d.servesAtHome && `se déplace (${d.serviceRadiusKm} km)`, d.hasPremises && 'reçoit sur place'].filter(Boolean).join(' · ')}
              {d.postalCode && ` · ${d.postalCode}`}</dd>
            <dt className="text-muted">Position (équipe seulement)</dt>
            <dd><a className="text-brand hover:underline" target="_blank" rel="noreferrer"
              href={`https://www.openstreetmap.org/?mlat=${d.latitude}&mlon=${d.longitude}#map=13/${d.latitude}/${d.longitude}`}>Voir sur la carte</a></dd>
            {d.hasRegulatedServices && <>
              <dt className="text-muted">Métier réglementé</dt>
              <dd>{d.qualificationDeclared ? 'Déclaration sur l’honneur cochée' : <span className="text-danger">Déclaration manquante</span>}</dd>
            </>}
            <dt className="text-muted">Activité</dt>
            <dd>{d.requestsReceived} demande(s), {d.requestsCompleted} terminée(s), réponse {Math.round(d.responseRate * 100)} %,
              {' '}{p.ratingCount ? `${p.ratingAverage.toFixed(1)}/5 (${p.ratingCount} avis)` : 'aucun avis'}</dd>
            <dt className="text-muted">Dates</dt>
            <dd>créé {fmtRelative(p.createdAt)}{p.submittedAt && ` · envoyé ${fmtDateTime(p.submittedAt)}`}{p.approvedAt && ` · publié ${fmtDateTime(p.approvedAt)}`}</dd>
          </dl>

          <section>
            <h3 className="mb-2 text-sm font-semibold">Offres ({d.offerings.length})</h3>
            <div className="space-y-2">{d.offerings.map((o) => <OfferingCard key={o.id} o={o} />)}</div>
            {d.offerings.length === 0 && <p className="text-sm text-muted">Aucune offre.</p>}
          </section>

          {d.documents.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold">Justificatifs</h3>
              <ul className="space-y-1 text-sm">
                {d.documents.map((doc) => (
                  <li key={doc.id} className="flex flex-wrap items-center gap-2">
                    <FileText className="size-4 text-muted" />
                    <button className="text-brand hover:underline" onClick={() => void openPrivateFile(`api/admin/services/documents/${doc.id}/file`)}>
                      {DOCUMENT_LABELS[doc.kind]} — {doc.originalName}
                    </button>
                    <Badge tone={doc.status === 'Approved' ? 'brand' : doc.status === 'Rejected' ? 'danger' : 'warning'}>
                      {{ Pending: 'À vérifier', Approved: 'Validé', Rejected: 'Refusé' }[doc.status]}
                    </Badge>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {d.reports.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold">Signalements</h3>
              <ul className="space-y-1 text-sm text-muted">
                {d.reports.map((r) => <li key={r.id}>{fmtDateTime(r.createdAt)} — {r.targetLabel} : {r.reason}{r.handled ? ` (traité : ${r.decision})` : ''}</li>)}
              </ul>
            </section>
          )}
        </div>
      )}
      <ConfirmDialog open={!!action} title={action?.title ?? ''} confirmLabel={action?.label ?? ''} danger={action?.danger} busy={decide.isPending}
        message={action?.verb === 'approve' ? 'Le profil et ses offres deviennent visibles dans les recherches. Le prestataire est prévenu.'
          : action?.verb === 'reject' ? 'Le profil repasse en brouillon : le prestataire voit votre message et peut corriger puis renvoyer.'
            : action?.verb === 'suspend' ? 'Le prestataire disparaît des recherches et ne reçoit plus de demandes.'
              : action?.verb === 'reinstate' ? 'Le prestataire redevient visible (signalements remis à zéro).' : 'Le badge de vérification est retiré.'}
        reason={action?.reason ? { label: 'Message au prestataire', required: true, placeholder: 'Ex. : ajoutez des photos de vos réalisations…' } : undefined}
        onClose={() => setAction(null)} onConfirm={(reason) => action && decide.mutate({ verb: action.verb, reason })} />
    </Modal>
  )
}
