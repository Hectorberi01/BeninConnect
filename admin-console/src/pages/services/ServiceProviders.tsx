import { useQuery } from '@tanstack/react-query'
import { Star } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Card, Empty, ErrorBox, Input, Loading, PageHeader, Pagination, Select, Table } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { countryName, fmtDateTime, fmtRelative } from '@/lib/format'
import type { AdminProvider, Paged, ProviderView } from '@/lib/types'
import { Avatar, LEGAL_LABELS, ProviderModal, ProviderStatusBadge, VerificationBadge } from './shared'

const TAKE = 25

const VIEWS: { key: ProviderView; label: string }[] = [
  { key: 'All', label: 'Tous' },
  { key: 'Active', label: 'En ligne' },
  { key: 'Pending', label: 'À valider' },
  { key: 'Draft', label: 'Brouillons' },
  { key: 'Hidden', label: 'Masqués' },
  { key: 'Suspended', label: 'Suspendus' },
]

export function ServiceProvidersPage() {
  const [view, setView] = useState<ProviderView>('All')
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [skip, setSkip] = useState(0)
  const [open, setOpen] = useState<string | null>(null)

  useEffect(() => {
    const t = setTimeout(() => { setSearch(q); setSkip(0) }, 300)
    return () => clearTimeout(t)
  }, [q])

  const list = useQuery({
    queryKey: ['services-providers', 'list', view, search, skip],
    queryFn: () => api<Paged<AdminProvider>>(`api/admin/services/providers${qs({ view, q: search, skip, take: TAKE })}`),
  })

  return (
    <>
      <PageHeader title="Prestataires" description="Tous les profils prestataires, quel que soit leur statut. Cliquez sur un profil pour le consulter ou le modérer." />
      <Card>
        <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
          <Select label="Statut" value={view} onChange={(e) => { setView(e.target.value as ProviderView); setSkip(0) }} className="w-48">
            {VIEWS.map((v) => <option key={v.key} value={v.key}>{v.label}</option>)}
          </Select>
          <Input label="Rechercher" placeholder="Nom ou ville" value={q} onChange={(e) => setQ(e.target.value)} className="min-w-56 flex-1" />
        </div>
        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.items.length === 0 ? (
          <Empty title="Aucun prestataire" />
        ) : (
          <>
            <Table head={['Prestataire', 'Statut', 'Ville', 'Statut juridique', 'Offres', 'Note', 'Mis à jour']}>
              {list.data.items.map((p) => (
                <tr key={p.id} className="cursor-pointer hover:bg-surface-2" onClick={() => setOpen(p.id)}>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <Avatar path={p.photoPath} name={p.displayName} size={36} />
                      <div className="min-w-0">
                        <p className="truncate font-medium">{p.displayName}</p>
                        <p className="truncate text-xs text-muted">{p.memberName}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3"><div className="flex flex-wrap gap-1"><ProviderStatusBadge p={p} /><VerificationBadge level={p.verificationLevel} /></div></td>
                  <td className="px-4 py-3">{p.city}<div className="text-xs text-muted">{countryName(p.country)}</div></td>
                  <td className="px-4 py-3 text-muted">{LEGAL_LABELS[p.legalStatus]}</td>
                  <td className="px-4 py-3">{p.offerings}</td>
                  <td className="px-4 py-3">{p.ratingCount ? <span className="inline-flex items-center gap-1"><Star className="size-3.5 fill-current text-warning" />{p.ratingAverage.toFixed(1)} <span className="text-xs text-muted">({p.ratingCount})</span></span> : '—'}</td>
                  <td className="px-4 py-3 text-muted" title={fmtDateTime(p.approvedAt ?? p.createdAt)}>{fmtRelative(p.approvedAt ?? p.createdAt)}</td>
                </tr>
              ))}
            </Table>
            <Pagination skip={skip} take={TAKE} total={list.data.totalCount} onChange={setSkip} />
          </>
        )}
      </Card>
      {open && <ProviderModal id={open} onClose={() => setOpen(null)} />}
    </>
  )
}
