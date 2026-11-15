import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, MessageCircleQuestion, Paperclip, Send } from 'lucide-react'
import { type FormEvent, useEffect, useRef, useState } from 'react'
import { useToast } from '@/components/toast'
import { Avatar, Badge, Button, Card, cx, Empty, ErrorBox, Loading, PageHeader } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDateTime, fmtRelative } from '@/lib/format'
import type { SupportConversation, SupportMessage, SupportThread } from '@/lib/types'

const MAX = 2000

/** Aperçu d'un message : texte, pièce jointe ou publication partagée. */
function preview(m: SupportMessage) {
  if (m.isDeleted) return 'Message supprimé'
  if (m.attachment) return `${m.attachment.kind === 'Image' ? '📷' : m.attachment.kind === 'Audio' ? '🎤' : '📄'} ${m.content || m.attachment.fileName}`
  if (m.sharedPostId) return `📰 ${m.content || 'Publication partagée'}`
  return m.content
}

export function SupportPage() {
  const [unreadOnly, setUnreadOnly] = useState(false)
  const [selected, setSelected] = useState<string | null>(null)

  const list = useQuery({
    queryKey: ['support', unreadOnly],
    queryFn: () => api<SupportConversation[]>(`api/admin/support${qs({ unread: unreadOnly ? 'true' : null })}`),
    refetchInterval: 20_000,
  })

  return (
    <>
      <PageHeader title="Support" description="Messages des membres au compte officiel BéninConnect. Les réponses partent au nom du compte officiel et sont journalisées." />
      <div className="grid gap-4 lg:grid-cols-[340px_1fr]">
        <Card className={cx('overflow-hidden', selected && 'hidden lg:block')}>
          <div className="flex gap-2 border-b border-line p-3" role="tablist">
            {[{ key: false, label: 'Toutes' }, { key: true, label: 'Non lues' }].map((t) => (
              <button key={t.label} role="tab" aria-selected={unreadOnly === t.key} onClick={() => setUnreadOnly(t.key)}
                className={cx('rounded-full px-3 py-1 text-sm font-medium', unreadOnly === t.key ? 'bg-brand text-white' : 'border border-line text-muted hover:text-fg')}>
                {t.label}
              </button>
            ))}
          </div>
          {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.length === 0 ? (
            <Empty title={unreadOnly ? 'Tout est lu' : 'Aucun message'} description={unreadOnly ? undefined : 'Les messages des membres apparaîtront ici.'} />
          ) : (
            <ul className="max-h-[70vh] divide-y divide-line overflow-y-auto">
              {list.data.map((c) => (
                <li key={c.id}>
                  <button onClick={() => setSelected(c.id)}
                    className={cx('flex w-full items-start gap-3 px-4 py-3 text-left hover:bg-surface-2', selected === c.id && 'bg-brand-soft')}>
                    <Avatar first={c.member.firstName} last={c.member.lastName} color={c.member.avatarColor} image={c.member.avatarImage} />
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-2">
                        <span className={cx('truncate text-sm', c.unreadCount > 0 ? 'font-semibold' : 'font-medium')}>{c.member.firstName} {c.member.lastName}</span>
                        <span className="ml-auto shrink-0 text-xs text-muted">{fmtRelative(c.updatedAt)}</span>
                      </span>
                      <span className="mt-0.5 flex items-center gap-2">
                        <span className={cx('truncate text-sm', c.unreadCount > 0 ? 'text-fg' : 'text-muted')}>
                          {c.lastMessage ? (c.lastMessage.senderId === c.member.id ? '' : 'Vous : ') + preview(c.lastMessage) : '—'}
                        </span>
                        {c.unreadCount > 0 && <span className="ml-auto shrink-0"><Badge tone="danger">{c.unreadCount}</Badge></span>}
                      </span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card className={cx('flex min-h-[60vh] flex-col overflow-hidden', !selected && 'hidden lg:flex')}>
          {selected ? <Thread id={selected} onBack={() => setSelected(null)} /> : (
            <div className="grid flex-1 place-items-center p-8 text-center text-muted">
              <div><MessageCircleQuestion className="mx-auto mb-3 size-8" /><p className="text-sm">Choisissez une discussion.</p></div>
            </div>
          )}
        </Card>
      </div>
    </>
  )
}

function Thread({ id, onBack }: { id: string; onBack: () => void }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [draft, setDraft] = useState('')
  const end = useRef<HTMLDivElement>(null)

  const thread = useQuery({ queryKey: ['support-thread', id], queryFn: () => api<SupportThread>(`api/admin/support/${id}`), refetchInterval: 15_000 })
  const refresh = () => ['support', 'support-thread', 'support-unread'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))

  // Ouverture : messages du membre marqués comme lus
  const unread = thread.data?.messages.some((m) => m.senderId !== thread.data.officialId && !m.isRead)
  useEffect(() => {
    if (unread) api(`api/admin/support/${id}/read`, { method: 'POST' }).then(refresh).catch(() => {})
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, unread])
  useEffect(() => end.current?.scrollIntoView({ block: 'end' }), [thread.data?.messages.length])

  const reply = useMutation({
    mutationFn: (content: string) => api<SupportMessage>(`api/admin/support/${id}/messages`, { method: 'POST', body: { content } }),
    onSuccess: () => { setDraft(''); refresh() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const send = (e: FormEvent) => {
    e.preventDefault()
    if (draft.trim()) reply.mutate(draft.trim())
  }

  if (thread.isPending) return <Loading />
  if (thread.error) return <ErrorBox message={errorMessage(thread.error)} onRetry={() => thread.refetch()} />
  const { member, officialId, messages } = thread.data

  return (
    <>
      <div className="flex items-center gap-3 border-b border-line px-4 py-3">
        <button onClick={onBack} className="rounded-lg p-1 text-muted hover:text-fg lg:hidden" aria-label="Retour à la liste"><ArrowLeft className="size-5" /></button>
        <Avatar first={member.firstName} last={member.lastName} color={member.avatarColor} image={member.avatarImage} />
        <div className="min-w-0">
          <p className="truncate font-medium">{member.firstName} {member.lastName}</p>
          <p className="truncate text-xs text-muted">{[member.email, member.city, member.country].filter(Boolean).join(' · ')}</p>
        </div>
      </div>

      <div className="max-h-[60vh] flex-1 space-y-2 overflow-y-auto bg-surface-2/40 p-4">
        {messages.length === 0 && <p className="py-8 text-center text-sm text-muted">Aucun message.</p>}
        {messages.map((m) => {
          const official = m.senderId === officialId
          return (
            <div key={m.id} className={cx('flex', official ? 'justify-end' : 'justify-start')}>
              <div className={cx('max-w-[80%] rounded-2xl px-3.5 py-2 text-sm', official ? 'bg-brand text-white' : 'border border-line bg-surface')}>
                {m.attachment && (
                  <p className={cx('mb-1 flex items-center gap-1.5 text-xs', official ? 'text-white/80' : 'text-muted')}>
                    <Paperclip className="size-3.5" />{m.attachment.fileName} (visible dans l'app seulement)
                  </p>
                )}
                {m.isDeleted ? <p className="italic opacity-70">Message supprimé</p> : (m.content || m.sharedPostId) && <p className="whitespace-pre-wrap break-words">{m.content || '📰 Publication partagée'}</p>}
                <p className={cx('mt-1 text-[11px]', official ? 'text-white/70' : 'text-muted')}>{fmtDateTime(m.sentAt)}</p>
              </div>
            </div>
          )
        })}
        <div ref={end} />
      </div>

      <form onSubmit={send} className="flex items-end gap-2 border-t border-line p-3">
        <textarea value={draft} onChange={(e) => setDraft(e.target.value.slice(0, MAX))} rows={2} placeholder="Votre réponse au nom de BéninConnect…"
          onKeyDown={(e) => { if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) send(e) }}
          className="min-h-[44px] flex-1 resize-y rounded-lg border border-line bg-surface px-3 py-2 text-sm outline-none focus:border-brand" />
        <Button type="submit" variant="primary" icon={<Send className="size-4" />} loading={reply.isPending} disabled={!draft.trim()}>Envoyer</Button>
      </form>
    </>
  )
}
