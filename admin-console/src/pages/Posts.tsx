import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Eye, EyeOff, MessageSquare, Search, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Pagination, Select, Table } from '@/components/ui'
import { api, apiUrl, errorMessage, qs } from '@/lib/api'
import { fmtDateTime } from '@/lib/format'
import type { AdminComment, AdminPost, Paged } from '@/lib/types'
import { useDebounced } from './Members'

const TAKE = 25
const VISIBILITY: Record<AdminPost['visibility'], string> = { Public: 'Public', CommunityMembers: 'Membres', OnlyMe: 'Privé' }

type Pending = { kind: 'hide' | 'unhide' | 'delete'; post: AdminPost } | { kind: 'deleteComment'; comment: AdminComment }

export function PostsPage() {
  const toast = useToast()
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [skip, setSkip] = useState(0)
  const [pending, setPending] = useState<Pending | null>(null)
  const [commentsOf, setCommentsOf] = useState<AdminPost | null>(null)
  const q = useDebounced(search.trim())
  useEffect(() => setSkip(0), [q, status])

  const list = useQuery({
    queryKey: ['posts', q, status, skip],
    queryFn: () => api<Paged<AdminPost>>(`api/admin/posts${qs({ search: q, status, skip, take: TAKE })}`),
  })
  const comments = useQuery({
    queryKey: ['comments', commentsOf?.id],
    queryFn: () => api<AdminComment[]>(`api/admin/posts/${commentsOf!.id}/comments`),
    enabled: !!commentsOf,
  })

  const act = useMutation({
    mutationFn: async ({ p, reason }: { p: Pending; reason: string }) => {
      if (p.kind === 'deleteComment') return api(`api/admin/comments/${p.comment.id}`, { method: 'DELETE' })
      if (p.kind === 'delete') return api(`api/admin/posts/${p.post.id}`, { method: 'DELETE' })
      return api(`api/admin/posts/${p.post.id}/${p.kind}`, { method: 'POST', body: p.kind === 'hide' ? { reason } : undefined })
    },
    onSuccess: (_r, { p }) => {
      toast({ hide: 'Publication masquée aux membres.', unhide: 'Publication rétablie.', delete: 'Publication supprimée.', deleteComment: 'Commentaire supprimé.' }[p.kind])
      setPending(null)
      ;['posts', 'comments', 'stats'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  const titles = { hide: 'Masquer la publication ?', unhide: 'Rétablir la publication ?', delete: 'Supprimer définitivement ?', deleteComment: 'Supprimer le commentaire ?' }

  return (
    <>
      <PageHeader title="Publications" description="Masquer garde la publication (réversible) ; supprimer l'efface avec ses commentaires." />
      <Card>
        <div className="flex flex-col gap-3 border-b border-line p-4 sm:flex-row">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-3 top-3 size-4 text-muted" />
            <Input aria-label="Rechercher" placeholder="Rechercher dans le texte" value={search} onChange={(e) => setSearch(e.target.value)} className="[&_input]:pl-9" />
          </div>
          <Select aria-label="Filtre" value={status} onChange={(e) => setStatus(e.target.value)} className="sm:w-52">
            <option value="all">Toutes</option>
            <option value="reported">Signalées (ouvertes)</option>
            <option value="visible">Visibles</option>
            <option value="hidden">Masquées</option>
          </Select>
        </div>
        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.items.length === 0 ? (
          <Empty title="Aucune publication" />
        ) : (
          <>
            <Table head={['Publication', 'Auteur', 'Date', 'Réactions', 'État', '']}>
              {list.data.items.map((p) => (
                <tr key={p.id} className={p.isHidden ? 'bg-surface-2/60' : undefined}>
                  <td className="max-w-md px-4 py-3">
                    <div className="flex gap-3">
                      {p.imagePath?.startsWith('uploads/') && <img src={apiUrl(p.imagePath)} alt="" className="size-12 shrink-0 rounded-md object-cover" />}
                      <div className="min-w-0">
                        <p className="line-clamp-2">{p.content}</p>
                        <p className="mt-1 text-xs text-muted">{p.communityName ?? 'Fil public'} · {VISIBILITY[p.visibility]}</p>
                        {p.isHidden && p.hiddenReason && <p className="mt-1 text-xs text-danger">Masquée : {p.hiddenReason}</p>}
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-muted">{p.authorName}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-muted">{fmtDateTime(p.createdAt)}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-muted">{p.likeCount} ♥ · {p.commentCount} comm.</td>
                  <td className="px-4 py-3">
                    <div className="flex flex-col items-start gap-1">
                      {p.isHidden ? <Badge tone="danger">Masquée</Badge> : <Badge tone="brand">Visible</Badge>}
                      {p.reportCount > 0 && <Badge tone="warning">{p.reportCount} signal.</Badge>}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" aria-label="Commentaires" title="Commentaires" onClick={() => setCommentsOf(p)} icon={<MessageSquare className="size-4" />} />
                      {p.isHidden
                        ? <Button variant="ghost" aria-label="Rétablir" title="Rétablir" onClick={() => setPending({ kind: 'unhide', post: p })} icon={<Eye className="size-4" />} />
                        : <Button variant="ghost" aria-label="Masquer" title="Masquer" onClick={() => setPending({ kind: 'hide', post: p })} icon={<EyeOff className="size-4" />} />}
                      <Button variant="ghost" aria-label="Supprimer" title="Supprimer" onClick={() => setPending({ kind: 'delete', post: p })} icon={<Trash2 className="size-4 text-danger" />} />
                    </div>
                  </td>
                </tr>
              ))}
            </Table>
            <Pagination skip={skip} take={TAKE} total={list.data.totalCount} onChange={setSkip} />
          </>
        )}
      </Card>

      <Modal open={!!commentsOf} title="Commentaires" onClose={() => setCommentsOf(null)} wide>
        {comments.isPending ? <Loading /> : comments.error ? <ErrorBox message={errorMessage(comments.error)} /> : comments.data!.length === 0 ? (
          <Empty title="Aucun commentaire" />
        ) : (
          <ul className="divide-y divide-line">
            {comments.data!.map((c) => (
              <li key={c.id} className="flex items-start gap-3 py-3">
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium">{c.authorName} <span className="font-normal text-muted">· {fmtDateTime(c.createdAt)}</span></p>
                  <p className="mt-0.5 text-sm">{c.content}</p>
                </div>
                <Button variant="ghost" aria-label="Supprimer le commentaire" onClick={() => setPending({ kind: 'deleteComment', comment: c })} icon={<Trash2 className="size-4 text-danger" />} />
              </li>
            ))}
          </ul>
        )}
      </Modal>

      <ConfirmDialog open={!!pending} busy={act.isPending} title={pending ? titles[pending.kind] : ''}
        danger={pending?.kind === 'delete' || pending?.kind === 'deleteComment'}
        message={pending ? `« ${(pending.kind === 'deleteComment' ? pending.comment.content : pending.post.content).slice(0, 160)} »` : ''}
        confirmLabel={pending?.kind === 'unhide' ? 'Rétablir' : pending?.kind === 'hide' ? 'Masquer' : 'Supprimer'}
        reason={pending?.kind === 'hide' ? { label: 'Motif (facultatif)', placeholder: 'Ex. : contenu commercial hors sujet' } : undefined}
        onClose={() => setPending(null)} onConfirm={(reason) => pending && act.mutate({ p: pending, reason })} />
    </>
  )
}
