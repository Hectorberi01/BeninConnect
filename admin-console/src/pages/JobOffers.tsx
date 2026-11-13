import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, Eye, EyeOff, ShieldAlert } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Pagination, Select, Table } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { contractText, countryName, fmtDateTime, fmtRelative } from '@/lib/format'
import type { AdminJobOffer, AdminJobOfferDetail, AdminOfferView, JobSource, JobsOverview, Paged } from '@/lib/types'

const TAKE = 25

const VIEWS: { key: AdminOfferView; label: string }[] = [
  { key: 'All', label: 'Toutes' },
  { key: 'Visible', label: 'Visibles par les membres' },
  { key: 'Expired', label: 'Expirées' },
  { key: 'Hidden', label: 'Masquées' },
  { key: 'Duplicates', label: 'Doublons' },
  { key: 'Review', label: 'À revoir' },
  { key: 'Reported', label: 'Signalées' },
]

/** Statut lisible : un doublon actif n'est pas visible des membres. */
function StatusBadge({ offer }: { offer: AdminJobOffer }) {
  if (offer.status === 'Hidden') return <Badge tone="danger">{offer.needsReview ? 'Masquée · à revoir' : 'Masquée'}</Badge>
  if (offer.status === 'Expired') return <Badge tone="neutral">Expirée</Badge>
  if (offer.isDuplicate) return <Badge tone="info">Doublon</Badge>
  return <Badge tone="brand">Visible</Badge>
}

export function JobOffersPage() {
  const [view, setView] = useState<AdminOfferView>('All')
  const [source, setSource] = useState<JobSource | ''>('')
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [skip, setSkip] = useState(0)
  const [openId, setOpenId] = useState<string | null>(null)

  useEffect(() => {
    const t = setTimeout(() => { setSearch(q); setSkip(0) }, 300)
    return () => clearTimeout(t)
  }, [q])

  const overview = useQuery({ queryKey: ['jobs'], queryFn: () => api<JobsOverview>('api/admin/jobs') })
  const list = useQuery({
    queryKey: ['jobs-offers', view, source, search, skip],
    queryFn: () => api<Paged<AdminJobOffer>>(`api/admin/jobs/offers${qs({ view, source: source || undefined, q: search, skip, take: TAKE })}`),
  })

  return (
    <>
      <PageHeader title="Offres importées" description="Toutes les offres reçues des sources, quel que soit leur statut. Cliquez sur une offre pour voir son détail." />
      {overview.data && (
        <div className="mb-4 flex flex-wrap gap-2 text-sm">
          {overview.data.counts.map((c) => <Badge key={c.label} tone={c.label === 'À revoir' && c.count > 0 ? 'warning' : 'neutral'}>{c.label} : {c.count}</Badge>)}
        </div>
      )}

      <Card>
        <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
          <Select label="Statut" value={view} onChange={(e) => { setView(e.target.value as AdminOfferView); setSkip(0) }} className="w-56">
            {VIEWS.map((v) => <option key={v.key} value={v.key}>{v.label}</option>)}
          </Select>
          <Select label="Source" value={source} onChange={(e) => { setSource(e.target.value as JobSource | ''); setSkip(0) }} className="w-44">
            <option value="">Toutes</option>
            {overview.data?.sources.map((s) => <option key={s.source} value={s.source}>{s.label}</option>)}
          </Select>
          <Input label="Rechercher" placeholder="Titre ou entreprise" value={q} onChange={(e) => setQ(e.target.value)} className="min-w-56 flex-1" />
        </div>

        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.items.length === 0 ? (
          <Empty title="Aucune offre" description="Lancez un import depuis « Sources et réglages » ou changez les filtres." />
        ) : (
          <>
            <Table head={['Offre', 'Source', 'Statut', 'Lieu', 'Contrat', 'Publiée', 'Vue chez la source', '']}>
              {list.data.items.map((o) => (
                <tr key={o.id} className="cursor-pointer hover:bg-surface-2" onClick={() => setOpenId(o.id)}>
                  <td className="max-w-80 px-4 py-3">
                    <p className="truncate font-medium">{o.title}</p>
                    <p className="truncate text-xs text-muted">{o.companyName ?? '—'}</p>
                  </td>
                  <td className="px-4 py-3 text-muted">{o.sourceLabel}<div className="text-xs">{o.searchKey}</div></td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      <StatusBadge offer={o} />
                      {o.reportCount > 0 && <Badge tone="warning">{o.reportCount} sign.</Badge>}
                    </div>
                  </td>
                  <td className="max-w-48 px-4 py-3"><p className="truncate">{o.locationLabel || '—'}</p><p className="text-xs text-muted">{countryName(o.country)}</p></td>
                  <td className="px-4 py-3">{contractText(o)}{o.isRemote && <span className="text-xs text-muted"> · télétravail</span>}</td>
                  <td className="px-4 py-3 text-muted" title={fmtDateTime(o.publishedAt)}>{fmtRelative(o.publishedAt)}</td>
                  <td className="px-4 py-3 text-muted" title={fmtDateTime(o.lastSeenAt)}>{fmtRelative(o.lastSeenAt)}</td>
                  <td className="px-4 py-3 text-right text-xs text-brand">Détail</td>
                </tr>
              ))}
            </Table>
            <Pagination skip={skip} take={TAKE} total={list.data.totalCount} onChange={setSkip} />
          </>
        )}
      </Card>

      {openId && <OfferModal id={openId} onClose={() => setOpenId(null)} onOpen={setOpenId} />}
    </>
  )
}

function OfferModal({ id, onClose, onOpen }: { id: string; onClose: () => void; onOpen: (id: string) => void }) {
  const toast = useToast()
  const qc = useQueryClient()
  const detail = useQuery({ queryKey: ['jobs-offer', id], queryFn: () => api<AdminJobOfferDetail>(`api/admin/jobs/offers/${id}`) })
  const decide = useMutation({
    mutationFn: (verb: 'show' | 'hide') => api<AdminJobOffer>(`api/admin/jobs/offers/${id}/${verb}`, { method: 'POST' }),
    onSuccess: (_, verb) => {
      toast(verb === 'show' ? 'Offre réaffichée.' : 'Offre masquée définitivement.', 'success')
      ;['jobs', 'jobs-offers', 'jobs-offer'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const d = detail.data
  const o = d?.offer

  const Row = ({ label, value }: { label: string; value: string | number | null | undefined }) =>
    value === null || value === undefined || value === '' ? null : (<><dt className="text-muted">{label}</dt><dd className="break-words">{value}</dd></>)

  return (
    <Modal open wide title={o?.title ?? 'Offre'} onClose={onClose}
      footer={o && <>
        <a href={o.applyUrl} target="_blank" rel="noreferrer"
          className="mr-auto inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium hover:bg-surface-2">
          Voir chez la source <ExternalLink className="size-4" />
        </a>
        {o.status === 'Hidden'
          ? <Button variant="primary" icon={<Eye className="size-4" />} loading={decide.isPending} onClick={() => decide.mutate('show')}>Réafficher</Button>
          : <Button variant="dangerSoft" icon={<EyeOff className="size-4" />} loading={decide.isPending} onClick={() => decide.mutate('hide')}>Masquer définitivement</Button>}
      </>}>
      {detail.isPending ? <Loading /> : detail.error ? <ErrorBox message={errorMessage(detail.error)} /> : d && o && (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge offer={o} />
            <Badge tone="neutral">{o.sourceLabel}</Badge>
            {o.alertKeyword && <Badge tone="warning"><ShieldAlert className="mr-1 inline size-3.5" />« {o.alertKeyword} »</Badge>}
            {o.reportCount > 0 && <Badge tone="warning">{o.reportCount} signalement(s)</Badge>}
            {o.favorites > 0 && <Badge tone="info">{o.favorites} favori(s)</Badge>}
          </div>

          {d.duplicateOfId && (
            <p className="rounded-lg bg-surface-2 px-3 py-2 text-sm">
              Doublon de « {d.duplicateOfTitle} » ({d.duplicateOfSource}), source prioritaire : cette copie n'est pas montrée aux membres.{' '}
              <button className="text-brand hover:underline" onClick={() => onOpen(d.duplicateOfId!)}>Voir l'offre d'origine</button>
            </p>
          )}

          <dl className="grid grid-cols-[11rem_1fr] gap-x-4 gap-y-1 text-sm">
            <Row label="Entreprise" value={o.companyName} />
            <Row label="Lieu" value={[o.locationLabel, o.department ? `dép. ${o.department}` : null, countryName(o.country)].filter(Boolean).join(' · ')} />
            <Row label="Contrat" value={`${contractText(o)}${o.contractLabel && o.contractLabel !== contractText(o) ? ` (${o.contractLabel})` : ''}`} />
            <Row label="Télétravail" value={o.isRemote ? 'Oui' : 'Non'} />
            <Row label="Temps de travail" value={d.workingTime} />
            <Row label="Salaire" value={d.salaryLabel} />
            <Row label="Secteur" value={d.sectorLabel} />
            <Row label="Publiée" value={fmtDateTime(o.publishedAt)} />
            <Row label="Clôture" value={o.closingAt ? fmtDateTime(o.closingAt) : null} />
            <Row label="Importée" value={fmtDateTime(o.importedAt)} />
            <Row label="Vue chez la source" value={fmtDateTime(o.lastSeenAt)} />
            <Row label="Expirée le" value={o.expiredAt ? fmtDateTime(o.expiredAt) : null} />
            <Row label="Recherche" value={o.searchKey} />
            <Row label="Identifiant source" value={o.externalId} />
          </dl>

          <div>
            <h3 className="mb-1 text-sm font-semibold">Description</h3>
            <p className="max-h-80 overflow-y-auto whitespace-pre-wrap rounded-lg border border-line p-3 text-sm">{d.description || '—'}</p>
          </div>

          {o.reports.length > 0 && (
            <div>
              <h3 className="mb-1 text-sm font-semibold">Signalements</h3>
              <ul className="space-y-1 text-sm text-muted">
                {o.reports.map((r, i) => <li key={i}>{fmtDateTime(r.createdAt)} — {r.reason}{r.handled ? ' (traité)' : ''}</li>)}
              </ul>
            </div>
          )}
        </div>
      )}
    </Modal>
  )
}
