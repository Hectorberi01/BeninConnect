import { useQuery } from '@tanstack/react-query'
import { ChevronRight, Search } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Avatar, Badge, Card, Empty, ErrorBox, Input, Loading, PageHeader, Pagination, Select, Table } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import type { AdminMember, Paged } from '@/lib/types'

const TAKE = 25

export function useDebounced<T>(value: T, ms = 300) {
  const [v, setV] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms)
    return () => clearTimeout(t)
  }, [value, ms])
  return v
}

export function MemberStatus({ m }: { m: AdminMember }) {
  if (m.isSuspended) return <Badge tone="danger">Suspendu</Badge>
  if (!m.isEmailVerified) return <Badge tone="warning">Non vérifié</Badge>
  return <Badge tone="brand">Actif</Badge>
}

export function MembersPage() {
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [skip, setSkip] = useState(0)
  const navigate = useNavigate()
  const q = useDebounced(search.trim())
  useEffect(() => setSkip(0), [q, status])

  const list = useQuery({
    queryKey: ['members', q, status, skip],
    queryFn: () => api<Paged<AdminMember>>(`api/admin/members${qs({ search: q, status, skip, take: TAKE })}`),
  })

  return (
    <>
      <PageHeader title="Membres" description="Comptes de l'application BéninConnect." />
      <Card>
        <div className="flex flex-col gap-3 border-b border-line p-4 sm:flex-row">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-3 top-3 size-4 text-muted" />
            <Input aria-label="Rechercher un membre" placeholder="Nom, email ou ville" value={search} onChange={(e) => setSearch(e.target.value)} className="[&_input]:pl-9" />
          </div>
          <Select aria-label="Statut" value={status} onChange={(e) => setStatus(e.target.value)} className="sm:w-48">
            <option value="all">Tous les statuts</option>
            <option value="active">Actifs</option>
            <option value="suspended">Suspendus</option>
            <option value="unverified">Email non vérifié</option>
          </Select>
        </div>
        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.items.length === 0 ? (
          <Empty title="Aucun membre trouvé" />
        ) : (
          <>
            <Table head={['Membre', 'Localisation', 'Inscription', 'Activité', 'Signalements', 'Statut', '']}>
              {list.data.items.map((m) => (
                <tr key={m.id} onClick={() => navigate(`/members/${m.id}`)} className="group cursor-pointer transition-colors hover:bg-surface-2">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <Avatar first={m.firstName} last={m.lastName} color={m.avatarColor} image={m.avatarImage} />
                      <div className="min-w-0">
                        <p className="truncate font-medium">{m.firstName} {m.lastName}</p>
                        <p className="truncate text-xs text-muted">{m.email}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-muted">{m.city}, {m.country}</td>
                  <td className="px-4 py-3 text-muted">{fmtDate(m.joinedAt)}</td>
                  <td className="px-4 py-3 text-muted">{m.postCount} publ. · {m.commentCount} comm.</td>
                  <td className="px-4 py-3">{m.reportCount > 0 ? <Badge tone="warning">{m.reportCount}</Badge> : <span className="text-muted">—</span>}</td>
                  <td className="px-4 py-3"><MemberStatus m={m} /></td>
                  <td className="px-4 py-3 text-muted"><ChevronRight className="size-4 transition-transform group-hover:translate-x-1" /></td>
                </tr>
              ))}
            </Table>
            <Pagination skip={skip} take={TAKE} total={list.data.totalCount} onChange={setSkip} />
          </>
        )}
      </Card>
    </>
  )
}
