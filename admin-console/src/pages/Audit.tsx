import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Badge, Card, Empty, ErrorBox, Input, Loading, PageHeader, Pagination, Table } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDateTime } from '@/lib/format'
import type { AuditEntry, Paged } from '@/lib/types'
import { useDebounced } from './Members'

const TAKE = 50

export function AuditPage() {
  const [search, setSearch] = useState('')
  const [skip, setSkip] = useState(0)
  const q = useDebounced(search.trim())
  useEffect(() => setSkip(0), [q])
  const list = useQuery({ queryKey: ['audit', q, skip], queryFn: () => api<Paged<AuditEntry>>(`api/admin/audit${qs({ search: q, skip, take: TAKE })}`) })

  return (
    <>
      <PageHeader title="Journal d'audit" description="Toutes les actions effectuées depuis la console : qui, quoi, quand." />
      <Card>
        <div className="border-b border-line p-4">
          <div className="relative max-w-md">
            <Search className="pointer-events-none absolute left-3 top-3 size-4 text-muted" />
            <Input aria-label="Rechercher" placeholder="Action, administrateur ou détail" value={search} onChange={(e) => setSearch(e.target.value)} className="[&_input]:pl-9" />
          </div>
        </div>
        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.items.length === 0 ? (
          <Empty title="Aucune entrée" />
        ) : (
          <>
            <Table head={['Date', 'Administrateur', 'Action', 'Cible', 'Détail']}>
              {list.data.items.map((e) => (
                <tr key={e.id}>
                  <td className="whitespace-nowrap px-4 py-3 text-muted">{fmtDateTime(e.at)}</td>
                  <td className="px-4 py-3 font-medium">{e.adminName}</td>
                  <td className="px-4 py-3">{e.action}</td>
                  <td className="px-4 py-3"><Badge>{e.targetType}</Badge></td>
                  <td className="max-w-md px-4 py-3 text-muted">{e.details}</td>
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
