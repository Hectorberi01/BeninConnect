import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useAuth } from '@/auth'
import { useToast } from '@/components/toast'
import { Avatar, Badge, Button, Card, ConfirmDialog, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Pagination, Select, Table } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDate, fmtDateTime } from '@/lib/format'
import type { AdminMember, AdminMemberDetail, Paged } from '@/lib/types'

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
  const [selected, setSelected] = useState<string | null>(null)
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
            <Table head={['Membre', 'Localisation', 'Inscription', 'Activité', 'Signalements', 'Statut']}>
              {list.data.items.map((m) => (
                <tr key={m.id} onClick={() => setSelected(m.id)} className="cursor-pointer hover:bg-surface-2">
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
                </tr>
              ))}
            </Table>
            <Pagination skip={skip} take={TAKE} total={list.data.totalCount} onChange={setSkip} />
          </>
        )}
      </Card>
      <MemberDetail id={selected} onClose={() => setSelected(null)} />
    </>
  )
}

function MemberDetail({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { can } = useAuth()
  const toast = useToast()
  const qc = useQueryClient()
  const [confirm, setConfirm] = useState<'suspend' | 'reactivate' | null>(null)
  const detail = useQuery({ queryKey: ['member', id], queryFn: () => api<AdminMemberDetail>(`api/admin/members/${id}`), enabled: !!id })

  const action = useMutation({
    mutationFn: ({ kind, reason }: { kind: 'suspend' | 'reactivate'; reason: string }) =>
      api<AdminMember>(`api/admin/members/${id}/${kind}`, { method: 'POST', body: kind === 'suspend' ? { reason } : undefined }),
    onSuccess: (m) => {
      toast(m.isSuspended ? 'Membre suspendu : ses sessions sont fermées.' : 'Membre réactivé.')
      setConfirm(null)
      ;['members', 'member', 'stats'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  const d = detail.data
  return (
    <>
      <Modal open={!!id} title="Fiche membre" onClose={onClose} wide footer={d && can('MembersSuspend') ? (
        d.member.isSuspended
          ? <Button variant="primary" onClick={() => setConfirm('reactivate')}>Réactiver le compte</Button>
          : <Button variant="danger" onClick={() => setConfirm('suspend')}>Suspendre le compte</Button>
      ) : undefined}>
        {detail.isPending ? <Loading /> : detail.error || !d ? <ErrorBox message={errorMessage(detail.error)} /> : (
          <div className="space-y-5">
            <div className="flex items-center gap-4">
              <Avatar first={d.member.firstName} last={d.member.lastName} color={d.member.avatarColor} image={d.member.avatarImage} size={56} />
              <div className="min-w-0">
                <p className="text-lg font-semibold">{d.member.firstName} {d.member.lastName}</p>
                <p className="text-sm text-muted">{d.member.email}</p>
                <div className="mt-1 flex flex-wrap gap-2"><MemberStatus m={d.member} />{d.profession && <Badge>{d.profession}</Badge>}</div>
              </div>
            </div>
            {d.member.isSuspended && (
              <p className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">Suspendu : {d.member.suspensionReason}</p>
            )}
            <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
              {[['Ville', `${d.member.city}, ${d.member.country}`], ['Inscription', fmtDate(d.member.joinedAt)],
                ['Publications', d.member.postCount], ['Signalements', d.member.reportCount]].map(([k, v]) => (
                <div key={String(k)} className="rounded-lg bg-surface-2 p-3"><dt className="text-xs text-muted">{k}</dt><dd className="mt-0.5 font-medium">{v}</dd></div>
              ))}
            </dl>
            {d.bio && <p className="text-sm">{d.bio}</p>}
            <div>
              <p className="mb-2 text-sm font-medium">Communautés ({d.communities.length})</p>
              <div className="flex flex-wrap gap-2">{d.communities.length ? d.communities.map((c) => <Badge key={c}>{c}</Badge>) : <span className="text-sm text-muted">Aucune</span>}</div>
            </div>
            <div>
              <p className="mb-2 text-sm font-medium">Publications récentes</p>
              {d.recentPosts.length === 0 ? <p className="text-sm text-muted">Aucune publication.</p> : (
                <ul className="space-y-2">
                  {d.recentPosts.map((p) => (
                    <li key={p.id} className="rounded-lg border border-line p-3 text-sm">
                      <p className="line-clamp-2">{p.content}</p>
                      <p className="mt-1 text-xs text-muted">{fmtDateTime(p.createdAt)} · {p.likeCount} j'aime · {p.commentCount} comm.{p.isHidden ? ' · masquée' : ''}</p>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        )}
      </Modal>
      <ConfirmDialog open={!!confirm} busy={action.isPending}
        title={confirm === 'suspend' ? 'Suspendre ce membre ?' : 'Réactiver ce membre ?'}
        message={confirm === 'suspend'
          ? 'Le membre ne pourra plus se connecter et ses sessions ouvertes seront fermées immédiatement.'
          : 'Le membre pourra de nouveau se connecter.'}
        confirmLabel={confirm === 'suspend' ? 'Suspendre' : 'Réactiver'} danger={confirm === 'suspend'}
        reason={confirm === 'suspend' ? { label: 'Motif', required: true, placeholder: 'Ex. : harcèlement signalé à plusieurs reprises' } : undefined}
        onClose={() => setConfirm(null)} onConfirm={(reason) => confirm && action.mutate({ kind: confirm, reason })} />
    </>
  )
}
