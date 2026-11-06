import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, Empty, ErrorBox, Input, Loading, PageHeader, Table } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import type { AdminCommunity } from '@/lib/types'
import { useDebounced } from './Members'

export function CommunitiesPage() {
  const toast = useToast()
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [pending, setPending] = useState<AdminCommunity | null>(null)
  const q = useDebounced(search.trim())
  const list = useQuery({ queryKey: ['communities', q], queryFn: () => api<AdminCommunity[]>(`api/admin/communities${qs({ search: q })}`) })
  const remove = useMutation({
    mutationFn: (id: string) => api(`api/admin/communities/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      toast('Communauté supprimée.')
      setPending(null)
      ;['communities', 'stats'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  return (
    <>
      <PageHeader title="Communautés" description={list.data ? `${list.data.length} communautés` : undefined} />
      <Card>
        <div className="border-b border-line p-4">
          <div className="relative max-w-md">
            <Search className="pointer-events-none absolute left-3 top-3 size-4 text-muted" />
            <Input aria-label="Rechercher" placeholder="Nom ou ville" value={search} onChange={(e) => setSearch(e.target.value)} className="[&_input]:pl-9" />
          </div>
        </div>
        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.length === 0 ? (
          <Empty title="Aucune communauté" />
        ) : (
          <Table head={['Communauté', 'Lieu', 'Créateur', 'Membres', 'Publications', 'Création', '']}>
            {list.data.map((c) => (
              <tr key={c.id}>
                <td className="px-4 py-3">
                  <p className="font-medium">{c.name}</p>
                  <div className="mt-1 flex gap-1.5"><Badge>{c.category}</Badge>{c.visibility === 'Private' && <Badge tone="info">Privée</Badge>}</div>
                </td>
                <td className="px-4 py-3 text-muted">{c.city || '—'}, {c.country}</td>
                <td className="px-4 py-3 text-muted">{c.ownerName}</td>
                <td className="px-4 py-3">{c.memberCount}{c.pendingRequests > 0 && <span className="ml-2 text-xs text-warning">+{c.pendingRequests} en attente</span>}</td>
                <td className="px-4 py-3 text-muted">{c.postCount}</td>
                <td className="px-4 py-3 text-muted">{fmtDate(c.createdAt)}</td>
                <td className="px-4 py-3 text-right">
                  <Button variant="ghost" aria-label={`Supprimer ${c.name}`} onClick={() => setPending(c)} icon={<Trash2 className="size-4 text-danger" />} />
                </td>
              </tr>
            ))}
          </Table>
        )}
      </Card>
      <ConfirmDialog open={!!pending} danger busy={remove.isPending} title={`Supprimer « ${pending?.name} » ?`} confirmLabel="Supprimer"
        message="Les adhésions et demandes sont supprimées. Les publications et événements restent, détachés de la communauté (les publications réservées aux membres deviennent invisibles)."
        onClose={() => setPending(null)} onConfirm={() => pending && remove.mutate(pending.id)} />
    </>
  )
}
