import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, Empty, ErrorBox, Input, Loading, PageHeader, Table } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDateTime } from '@/lib/format'
import type { AdminEvent } from '@/lib/types'
import { useDebounced } from './Members'

export function EventsPage() {
  const toast = useToast()
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [past, setPast] = useState(false)
  const [pending, setPending] = useState<AdminEvent | null>(null)
  const q = useDebounced(search.trim())
  const list = useQuery({ queryKey: ['events', q, past], queryFn: () => api<AdminEvent[]>(`api/admin/events${qs({ search: q, includePast: past })}`) })
  const remove = useMutation({
    mutationFn: (id: string) => api(`api/admin/events/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      toast('Événement supprimé.')
      setPending(null)
      ;['events', 'stats'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  return (
    <>
      <PageHeader title="Événements" description={list.data ? `${list.data.length} événement(s)${past ? '' : ' à venir'}` : undefined} />
      <Card>
        <div className="flex flex-col gap-3 border-b border-line p-4 sm:flex-row sm:items-center">
          <div className="relative max-w-md flex-1">
            <Search className="pointer-events-none absolute left-3 top-3 size-4 text-muted" />
            <Input aria-label="Rechercher" placeholder="Titre ou ville" value={search} onChange={(e) => setSearch(e.target.value)} className="[&_input]:pl-9" />
          </div>
          <label className="flex items-center gap-2 text-sm text-muted">
            <input type="checkbox" checked={past} onChange={(e) => setPast(e.target.checked)} className="size-4 accent-[var(--brand)]" /> Inclure les événements passés
          </label>
        </div>
        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.length === 0 ? (
          <Empty title="Aucun événement" />
        ) : (
          <Table head={['Événement', 'Date', 'Lieu', 'Organisateur', 'Participants', '']}>
            {list.data.map((e) => {
              const full = e.capacity != null && e.participants >= e.capacity
              return (
                <tr key={e.id}>
                  <td className="px-4 py-3">
                    <p className="font-medium">{e.title}</p>
                    <div className="mt-1 flex gap-1.5"><Badge>{e.category}</Badge>{e.communityName && <Badge tone="info">{e.communityName}</Badge>}</div>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-muted">{fmtDateTime(e.startsAt)}</td>
                  <td className="px-4 py-3 text-muted">{e.city}</td>
                  <td className="px-4 py-3 text-muted">{e.organizerName}</td>
                  <td className="px-4 py-3">{e.participants}{e.capacity != null ? ` / ${e.capacity}` : ''} {full && <Badge tone="warning">Complet</Badge>}</td>
                  <td className="px-4 py-3 text-right">
                    <Button variant="ghost" aria-label={`Supprimer ${e.title}`} onClick={() => setPending(e)} icon={<Trash2 className="size-4 text-danger" />} />
                  </td>
                </tr>
              )
            })}
          </Table>
        )}
      </Card>
      <ConfirmDialog open={!!pending} danger busy={remove.isPending} title={`Supprimer « ${pending?.title} » ?`} confirmLabel="Supprimer"
        message="L'événement et les inscriptions sont supprimés définitivement. Les participants ne sont pas prévenus automatiquement."
        onClose={() => setPending(null)} onConfirm={() => pending && remove.mutate(pending.id)} />
    </>
  )
}
