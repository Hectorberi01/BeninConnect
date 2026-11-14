import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Eye, EyeOff, Flag, PackageCheck, Plane, Star } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, cx, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Pagination, Select, Stat } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { countryName, fmtDateTime, fmtRelative } from '@/lib/format'
import type {
  AdminBooking, AdminTrip, AdminTripDetail, AdminTripView, AdminVoyageReport, AdminVoyageReview, BookingStatus, Paged, ParcelCategory, TripStatus,
  VoyagesOverview,
} from '@/lib/types'

type Tab = 'trips' | 'disputes' | 'reports'
const TAKE = 20

export const PARCEL_LABELS: Record<ParcelCategory, string> = {
  Documents: 'Documents', Clothes: 'Vêtements', DryFood: 'Alimentaire sec', Electronics: 'Électronique', Cosmetics: 'Cosmétiques', Other: 'Autre',
}
const TRIP_STATUS: Record<TripStatus, string> = { Open: 'Ouvert', Full: 'Complet', Departed: 'Parti', Cancelled: 'Annulé' }
const BOOKING_STATUS: Record<BookingStatus, string> = {
  Pending: 'En attente', Accepted: 'Acceptée', Declined: 'Refusée', Cancelled: 'Annulée', Delivered: 'Livrée', Disputed: 'Litige',
}
const HIDDEN: Record<string, string> = { Reports: 'Masqué (signalements)', Admin: 'Masqué par l’équipe', Suspended: 'Voyageur suspendu' }

const day = (d: string) => new Date(`${d}T12:00:00`).toLocaleDateString('fr-FR', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })

function useRefresh() {
  const qc = useQueryClient()
  return () => ['voyages', 'voyages-trips', 'voyages-trip', 'voyages-disputes', 'voyages-reports'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
}

/** Voyages : trajets publiés, litiges à trancher, signalements. Mise en relation seulement, aucun paiement. */
export function VoyagesPage() {
  const [params, setParams] = useSearchParams()
  const tab = (params.get('tab') as Tab) || 'trips'
  const overview = useQuery({ queryKey: ['voyages'], queryFn: () => api<VoyagesOverview>('api/admin/trips/overview') })
  const o = overview.data
  const tabs: { key: Tab; label: string; count?: number }[] = [
    { key: 'trips', label: 'Trajets' },
    { key: 'disputes', label: 'Litiges', count: o?.disputes },
    { key: 'reports', label: 'Signalements', count: o?.openReports },
  ]

  return (
    <>
      <PageHeader title="Voyages" description="« Qui part au pays ? » : trajets publiés, réservations de kilos, litiges et signalements." />
      {overview.error ? <ErrorBox message={errorMessage(overview.error)} onRetry={overview.refetch} /> : (
        <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Stat label="Trajets à venir" count={o?.upcomingTrips ?? 0} icon={<Plane />} hint={`${o?.openTrips ?? 0} encore ouverts`} />
          <Stat label="Réservations en attente" count={o?.pendingBookings ?? 0} accent="info" icon={<PackageCheck />}
            hint={`${o?.acceptedBookings ?? 0} acceptées en cours`} />
          <Stat label="Livraisons (30 jours)" count={o?.deliveredLast30Days ?? 0} accent="violet" icon={<Star />} hint={`${o?.deliveredBookings ?? 0} au total`} />
          <Stat label="À traiter" count={(o?.disputes ?? 0) + (o?.openReports ?? 0)} accent="warning" icon={<AlertTriangle />}
            tone={(o?.disputes ?? 0) > 0 ? 'warning' : undefined} hint={`${o?.disputes ?? 0} litige(s), ${o?.openReports ?? 0} signalement(s)`} />
        </div>
      )}
      {o && o.topRoutes.length > 0 && (
        <Card className="mb-6 px-5 py-4">
          <p className="mb-2 text-sm font-semibold">Itinéraires les plus publiés (90 jours)</p>
          <div className="flex flex-wrap gap-2">{o.topRoutes.map((r) => <Badge key={r.label}>{r.label} · {r.count}</Badge>)}</div>
        </Card>
      )}
      <div className="mb-4 flex flex-wrap gap-2" role="tablist">
        {tabs.map((t) => (
          <button key={t.key} role="tab" aria-selected={tab === t.key} onClick={() => setParams({ tab: t.key })}
            className={cx('rounded-full px-3.5 py-1.5 text-sm font-medium', tab === t.key ? 'bg-brand text-white' : 'border border-line bg-surface text-muted hover:text-fg')}>
            {t.label}{t.count ? ` · ${t.count}` : ''}
          </button>
        ))}
      </div>
      {tab === 'trips' && <Trips />}
      {tab === 'disputes' && <Disputes />}
      {tab === 'reports' && <Reports />}
    </>
  )
}

function List<T>({ query, empty, children, skip, setSkip }: {
  query: { isPending: boolean; error: unknown; data?: Paged<T>; refetch: () => void }; empty: string; children: (item: T) => React.ReactNode
  skip: number; setSkip: (n: number) => void
}) {
  if (query.isPending) return <Loading />
  if (query.error) return <ErrorBox message={errorMessage(query.error)} onRetry={query.refetch} />
  if (!query.data || query.data.items.length === 0) return <Empty title="Rien à afficher" description={empty} />
  return (
    <>
      <ul className="divide-y divide-line">{query.data.items.map(children)}</ul>
      <Pagination skip={skip} take={TAKE} total={query.data.totalCount} onChange={setSkip} />
    </>
  )
}

function TripStatusBadge({ t }: { t: AdminTrip }) {
  if (t.hiddenReason !== 'None') return <Badge tone="danger">{HIDDEN[t.hiddenReason]}</Badge>
  return <Badge tone={t.status === 'Open' ? 'brand' : t.status === 'Full' ? 'info' : 'neutral'}>{TRIP_STATUS[t.status]}</Badge>
}

function Trips() {
  const [view, setView] = useState<AdminTripView>('Upcoming')
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [skip, setSkip] = useState(0)
  const [open, setOpen] = useState<string | null>(null)
  const query = useQuery({
    queryKey: ['voyages-trips', view, search, skip],
    queryFn: () => api<Paged<AdminTrip>>(`api/admin/trips${qs({ view, q: search || undefined, skip, take: TAKE })}`),
  })
  return (
    <Card>
      <form className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3" onSubmit={(e) => { e.preventDefault(); setSearch(q.trim()); setSkip(0) }}>
        <Select label="Afficher" value={view} onChange={(e) => { setView(e.target.value as AdminTripView); setSkip(0) }} className="w-56">
          <option value="Upcoming">À venir</option>
          <option value="Reported">Signalés</option>
          <option value="Hidden">Masqués</option>
          <option value="All">Tous</option>
        </Select>
        <Input label="Ville" placeholder="Cotonou, Paris…" value={q} onChange={(e) => setQ(e.target.value)} className="w-56" />
        <Button type="submit">Rechercher</Button>
      </form>
      <List query={query} skip={skip} setSkip={setSkip} empty="Aucun trajet pour ce filtre.">
        {(t) => (
          <li key={t.id} className="flex cursor-pointer items-center gap-3 px-5 py-3 hover:bg-surface-2" onClick={() => setOpen(t.id)}>
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-medium">{t.fromCity} → {t.toCity}</p>
                <TripStatusBadge t={t} />
                {t.reportCount > 0 && <Badge tone="warning">{t.reportCount} signalement(s)</Badge>}
              </div>
              <p className="truncate text-sm text-muted">
                {day(t.departureDate)} · {t.travelerName} · {t.reservedKg}/{t.capacityKg} kg · {t.bookingCount} réservation(s) · publié {fmtRelative(t.createdAt)}
              </p>
            </div>
            <span className="text-sm text-brand">Examiner</span>
          </li>
        )}
      </List>
      {open && <TripModal id={open} onClose={() => setOpen(null)} />}
    </Card>
  )
}

function TripModal({ id, onClose }: { id: string; onClose: () => void }) {
  const toast = useToast()
  const refresh = useRefresh()
  const [hiding, setHiding] = useState(false)
  const [review, setReview] = useState<AdminVoyageReview | null>(null)
  const detail = useQuery({ queryKey: ['voyages-trip', id], queryFn: () => api<AdminTripDetail>(`api/admin/trips/${id}`) })
  const decide = useMutation({
    mutationFn: ({ show, reason }: { show: boolean; reason?: string }) => api(`api/admin/trips/${id}/${show ? 'show' : 'hide'}`, { method: 'POST', body: { reason } }),
    onSuccess: (_, v) => { toast(v.show ? 'Trajet rétabli.' : 'Trajet masqué.', 'success'); setHiding(false); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const reviewVisibility = useMutation({
    mutationFn: ({ r, reason }: { r: AdminVoyageReview; reason?: string }) =>
      api(`api/admin/trips/reviews/${r.id}/${r.isHidden ? 'show' : 'hide'}`, { method: 'POST', body: { reason } }),
    onSuccess: () => { toast('Avis mis à jour.', 'success'); setReview(null); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const d = detail.data
  const t = d?.trip
  return (
    <Modal open wide title={t ? `${t.fromCity} → ${t.toCity}` : 'Trajet'} onClose={onClose} footer={t && (
      t.hiddenReason === 'None'
        ? <Button variant="danger" icon={<EyeOff className="size-4" />} onClick={() => setHiding(true)}>Masquer le trajet</Button>
        : <Button variant="primary" icon={<Eye className="size-4" />} loading={decide.isPending} onClick={() => decide.mutate({ show: true })}>Rétablir</Button>
    )}>
      {detail.isPending ? <Loading /> : detail.error ? <ErrorBox message={errorMessage(detail.error)} /> : d && t && (
        <div className="space-y-5">
          <div className="flex flex-wrap items-center gap-2">
            <TripStatusBadge t={t} />
            {t.hiddenNote && <span className="text-sm text-muted">Motif : {t.hiddenNote}</span>}
          </div>
          <dl className="grid grid-cols-[12rem_1fr] gap-x-4 gap-y-1 text-sm">
            <dt className="text-muted">Voyageur</dt><dd>{t.travelerName}</dd>
            <dt className="text-muted">Itinéraire</dt><dd>{t.fromCity} ({countryName(t.fromCountry)}) → {t.toCity} ({countryName(t.toCountry)})</dd>
            <dt className="text-muted">Départ</dt><dd>{day(t.departureDate)}</dd>
            <dt className="text-muted">Kilos</dt><dd>{t.reservedKg} réservés sur {t.capacityKg}</dd>
            <dt className="text-muted">Prix indicatif</dt><dd>{d.pricePerKg == null ? 'À convenir' : `${d.pricePerKg} ${d.currency}/kg`}</dd>
            <dt className="text-muted">Colis acceptés</dt><dd>{d.acceptedCategories.map((c) => PARCEL_LABELS[c]).join(', ')}</dd>
            {d.dropOffZones && <><dt className="text-muted">Zones de remise</dt><dd>{d.dropOffZones}</dd></>}
            {d.notes && <><dt className="text-muted">Informations</dt><dd className="whitespace-pre-wrap">{d.notes}</dd></>}
            <dt className="text-muted">Publié</dt><dd>{fmtDateTime(t.createdAt)}</dd>
          </dl>
          <section>
            <h3 className="mb-2 text-sm font-semibold">Réservations ({d.bookings.length})</h3>
            {d.bookings.length === 0 ? <p className="text-sm text-muted">Aucune réservation.</p> : (
              <ul className="space-y-2">{d.bookings.map((b) => <BookingRow key={b.id} b={b} />)}</ul>
            )}
          </section>
          {d.reviews.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold">Avis</h3>
              <ul className="space-y-2">
                {d.reviews.map((r) => (
                  <li key={r.id} className="flex items-start gap-3 rounded-lg border border-line px-3 py-2 text-sm">
                    <div className="min-w-0 flex-1">
                      <p><span className="font-medium">{r.rating}/5</span> · {r.authorName} sur {r.targetName} ({r.aboutTraveler ? 'voyageur' : 'expéditeur'})
                        {r.isHidden && <Badge tone="danger"> masqué</Badge>}</p>
                      {r.comment && <p className="text-muted">{r.comment}</p>}
                    </div>
                    <Button onClick={() => (r.isHidden ? reviewVisibility.mutate({ r }) : setReview(r))}>{r.isHidden ? 'Rétablir' : 'Masquer'}</Button>
                  </li>
                ))}
              </ul>
            </section>
          )}
          {d.reports.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold">Signalements</h3>
              <ul className="space-y-1 text-sm text-muted">
                {d.reports.map((r) => <li key={r.id}>{fmtDateTime(r.createdAt)} — {r.targetLabel} : {r.reason} ({r.authorName}){r.handled ? ` · traité : ${r.decision}` : ''}</li>)}
              </ul>
            </section>
          )}
        </div>
      )}
      <ConfirmDialog open={hiding} title="Masquer ce trajet" confirmLabel="Masquer" danger busy={decide.isPending}
        message="Le trajet disparaît des recherches. Le voyageur reçoit le motif ; les réservations existantes ne changent pas."
        reason={{ label: 'Motif envoyé au voyageur', required: true, placeholder: 'Ex. : transport d’objets interdits annoncé' }}
        onClose={() => setHiding(false)} onConfirm={(reason) => decide.mutate({ show: false, reason })} />
      <ConfirmDialog open={!!review} title="Masquer cet avis" confirmLabel="Masquer" danger busy={reviewVisibility.isPending}
        message="Un avis n’est jamais retiré parce qu’il est négatif : seulement s’il enfreint les règles."
        reason={{ label: 'Motif', required: true }} onClose={() => setReview(null)} onConfirm={(reason) => review && reviewVisibility.mutate({ r: review, reason })} />
    </Modal>
  )
}

function BookingRow({ b, onResolve }: { b: AdminBooking; onResolve?: () => void }) {
  return (
    <li className="rounded-lg border border-line px-3 py-2 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{b.senderName} → {b.travelerName}</span>
        <Badge tone={b.status === 'Disputed' ? 'warning' : b.status === 'Delivered' ? 'brand' : 'neutral'}>{BOOKING_STATUS[b.status]}</Badge>
        <span className="text-muted">{b.weightKg} kg · {PARCEL_LABELS[b.category]} · {b.route} · {day(b.departureDate)}</span>
      </div>
      <p className="mt-1">{b.description}</p>
      {b.disputeReason && (
        <p className="mt-1 text-warning">
          Litige ouvert par {b.disputedBy === b.senderId ? 'l’expéditeur' : 'le voyageur'} {b.disputedAt && fmtRelative(b.disputedAt)} : {b.disputeReason}
          {b.statusBeforeDispute && ` (statut avant : ${BOOKING_STATUS[b.statusBeforeDispute]})`}
        </p>
      )}
      {b.deliveryAttempts > 0 && b.status !== 'Delivered' && <p className="mt-1 text-muted">{b.deliveryAttempts} essai(s) de code erroné(s)</p>}
      {b.resolutionNote && <p className="mt-1 text-muted">Décision : {b.resolutionNote}</p>}
      {onResolve && <div className="mt-2"><Button variant="primary" onClick={onResolve}>Trancher</Button></div>}
    </li>
  )
}

function Disputes() {
  const toast = useToast()
  const refresh = useRefresh()
  const [resolved, setResolved] = useState(false)
  const [skip, setSkip] = useState(0)
  const [deciding, setDeciding] = useState<{ b: AdminBooking; outcome: 'Delivered' | 'Cancelled' } | null>(null)
  const [choosing, setChoosing] = useState<AdminBooking | null>(null)
  const query = useQuery({
    queryKey: ['voyages-disputes', resolved, skip],
    queryFn: () => api<Paged<AdminBooking>>(`api/admin/trips/disputes${qs({ resolved: resolved ? 'true' : undefined, skip, take: TAKE })}`),
  })
  const resolve = useMutation({
    mutationFn: ({ id, outcome, note }: { id: string; outcome: string; note: string }) =>
      api(`api/admin/trips/bookings/${id}/resolve`, { method: 'POST', body: { outcome, note } }),
    onSuccess: () => { toast('Litige tranché : les deux membres sont prévenus.', 'success'); setDeciding(null); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Card>
      <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
        <Select label="Afficher" value={resolved ? 'resolved' : 'open'} onChange={(e) => { setResolved(e.target.value === 'resolved'); setSkip(0) }} className="w-56">
          <option value="open">En cours</option>
          <option value="resolved">Tranchés</option>
        </Select>
        <p className="pb-2 text-sm text-muted">Les avis sont bloqués tant que le litige est en cours. Les plus anciens d’abord.</p>
      </div>
      <List query={query} skip={skip} setSkip={setSkip} empty={resolved ? 'Aucun litige tranché.' : 'Aucun litige en cours.'}>
        {(b) => <div key={b.id} className="px-5 py-3"><BookingRow b={b} onResolve={resolved ? undefined : () => setChoosing(b)} /></div>}
      </List>
      <Modal open={!!choosing} title="Trancher le litige" onClose={() => setChoosing(null)} footer={
        <>
          <Button onClick={() => { if (choosing) setDeciding({ b: choosing, outcome: 'Cancelled' }); setChoosing(null) }}>Annuler la réservation</Button>
          <Button variant="primary" onClick={() => { if (choosing) setDeciding({ b: choosing, outcome: 'Delivered' }); setChoosing(null) }}>Confirmer la livraison</Button>
        </>
      }>
        <p className="text-sm text-muted">Confirmez la livraison si la remise est établie (les avis s’ouvrent), sinon annulez la réservation.</p>
      </Modal>
      <ConfirmDialog open={!!deciding} title={deciding?.outcome === 'Delivered' ? 'Confirmer la livraison' : 'Annuler la réservation'}
        confirmLabel="Enregistrer la décision" busy={resolve.isPending} danger={deciding?.outcome === 'Cancelled'}
        message="La décision est envoyée aux deux membres et journalisée."
        reason={{ label: 'Décision motivée', required: true, placeholder: 'Ex. : remise confirmée par les deux parties dans la conversation' }}
        onClose={() => setDeciding(null)} onConfirm={(note) => deciding && resolve.mutate({ id: deciding.b.id, outcome: deciding.outcome, note })} />
    </Card>
  )
}

function Reports() {
  const toast = useToast()
  const refresh = useRefresh()
  const [handled, setHandled] = useState(false)
  const [skip, setSkip] = useState(0)
  const [open, setOpen] = useState<string | null>(null)
  const [closing, setClosing] = useState<AdminVoyageReport | null>(null)
  const query = useQuery({
    queryKey: ['voyages-reports', handled, skip],
    queryFn: () => api<Paged<AdminVoyageReport>>(`api/admin/trips/reports${qs({ handled: handled ? 'true' : undefined, skip, take: TAKE })}`),
  })
  const close = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => api(`api/admin/trips/reports/${id}/close`, { method: 'POST', body: { reason } }),
    onSuccess: () => { toast('Signalement traité.', 'success'); setClosing(null); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Card>
      <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
        <Select label="Afficher" value={handled ? 'handled' : 'open'} onChange={(e) => { setHandled(e.target.value === 'handled'); setSkip(0) }} className="w-56">
          <option value="open">À traiter</option>
          <option value="handled">Traités</option>
        </Select>
        <p className="pb-2 text-sm text-muted">3 signalements masquent automatiquement un trajet ou un avis, en attendant la décision.</p>
      </div>
      <List query={query} skip={skip} setSkip={setSkip} empty="Aucun signalement.">
        {(r) => (
          <li key={r.id} className="flex items-center gap-3 px-5 py-3">
            <Flag className="size-4 shrink-0 text-warning" />
            <div className="min-w-0 flex-1">
              <p className="font-medium">{r.targetLabel}</p>
              <p className="truncate text-sm text-muted">{r.reason} · {r.authorName} · {fmtRelative(r.createdAt)}{r.handled ? ` · ${r.decision}` : ''}</p>
            </div>
            {r.tripId && <Button onClick={() => setOpen(r.tripId)}>Voir le trajet</Button>}
            {!r.handled && <Button variant="primary" onClick={() => setClosing(r)}>Classer</Button>}
          </li>
        )}
      </List>
      {open && <TripModal id={open} onClose={() => setOpen(null)} />}
      <ConfirmDialog open={!!closing} title="Classer le signalement" confirmLabel="Classer" busy={close.isPending}
        message="Pour masquer un trajet ou un avis, ouvrez le trajet. Classer ne change pas le contenu."
        reason={{ label: 'Décision', placeholder: 'Classé sans suite' }} onClose={() => setClosing(null)}
        onConfirm={(reason) => closing && close.mutate({ id: closing.id, reason })} />
    </Card>
  )
}
