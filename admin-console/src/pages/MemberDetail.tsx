import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Ban, CalendarDays, Check, Copy, FileText, Flag, Heart, History, Lock, Mail, MapPin, MessageCircle, MessagesSquare,
  ShieldAlert, Smartphone, UserX, UsersRound,
} from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { type ReactNode, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '@/auth'
import { revealItem, Stagger } from '@/components/motion'
import { useToast } from '@/components/toast'
import { Avatar, Badge, Button, Card, ConfirmDialog, cx, Empty, ErrorBox, Loading, Stat } from '@/components/ui'
import { api, apiUrl, errorMessage } from '@/lib/api'
import { fmtDate, fmtDateTime, fmtRelative } from '@/lib/format'
import { COMMUNITY_ROLE_LABELS, REASON_LABELS, REPORT_STATUS_LABELS } from '@/lib/permissions'
import type { AdminMember, AdminMemberDetail, AuditEntry, Paged } from '@/lib/types'
import { MemberStatus } from './Members'

type Tab = 'posts' | 'communities' | 'events' | 'reports' | 'history'

/** Page « Fiche membre » : profil, activité, modération, appareils et historique des actions admin. */
export function MemberDetailPage() {
  const { id = '' } = useParams()
  const { can } = useAuth()
  const toast = useToast()
  const qc = useQueryClient()
  const [confirm, setConfirm] = useState<'suspend' | 'reactivate' | null>(null)
  const [tab, setTab] = useState<Tab>('posts')

  const detail = useQuery({ queryKey: ['member', id], queryFn: () => api<AdminMemberDetail>(`api/admin/members/${id}`) })
  const audit = useQuery({
    queryKey: ['member-audit', id],
    queryFn: () => api<Paged<AuditEntry>>(`api/admin/audit?targetId=${id}&take=50`),
    enabled: can('AuditView'),
  })

  const action = useMutation({
    mutationFn: ({ kind, reason }: { kind: 'suspend' | 'reactivate'; reason: string }) =>
      api<AdminMember>(`api/admin/members/${id}/${kind}`, { method: 'POST', body: kind === 'suspend' ? { reason } : undefined }),
    onSuccess: (m) => {
      toast(m.isSuspended ? 'Membre suspendu : ses sessions sont fermées.' : 'Membre réactivé.')
      setConfirm(null)
      ;['members', 'member', 'member-audit', 'stats'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  if (detail.isPending) return <Loading />
  if (detail.error || !detail.data) return <ErrorBox message={errorMessage(detail.error)} onRetry={() => detail.refetch()} />

  const d = detail.data
  const m = d.member
  const memberships = d.memberships ?? []
  const events = d.events ?? []
  const reports = d.reportsAbout ?? []
  const devices = d.devices ?? []
  const tabs: { key: Tab; label: string; count?: number; show: boolean }[] = [
    { key: 'posts', label: 'Publications', count: d.recentPosts.length, show: true },
    { key: 'communities', label: 'Communautés', count: memberships.length, show: true },
    { key: 'events', label: 'Événements', count: events.length, show: true },
    { key: 'reports', label: 'Signalements', count: reports.length, show: true },
    { key: 'history', label: 'Historique admin', count: audit.data?.totalCount, show: can('AuditView') },
  ]

  return (
    <Stagger>
      <motion.div variants={revealItem} className="mb-4">
        <Link to="/members" className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-fg"><ArrowLeft className="size-4" />Membres</Link>
      </motion.div>

      <Profile d={d} actions={can('MembersSuspend') && (m.isSuspended
        ? <Button variant="primary" icon={<Check className="size-4" />} onClick={() => setConfirm('reactivate')}>Réactiver</Button>
        : <Button variant="danger" icon={<Ban className="size-4" />} onClick={() => setConfirm('suspend')}>Suspendre</Button>)} />

      {m.isSuspended && (
        <motion.div variants={revealItem} className="mb-6 flex items-start gap-3 rounded-xl border border-danger/30 bg-danger-soft px-4 py-3 text-sm text-danger">
          <ShieldAlert className="mt-0.5 size-5 shrink-0" />
          <div><p className="font-semibold">Compte suspendu{d.suspendedAt ? ` le ${fmtDateTime(d.suspendedAt)}` : ''}</p><p>Motif : {m.suspensionReason}</p></div>
        </motion.div>
      )}

      <Stagger className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-3 xl:grid-cols-6" delay={0.1}>
        <Stat label="Publications" count={m.postCount} icon={<FileText />} accent="info" />
        <Stat label="Commentaires" count={m.commentCount} icon={<MessageCircle />} accent="violet" />
        <Stat label="Communautés" count={memberships.length} icon={<UsersRound />} accent="brand" />
        <Stat label="Événements" count={events.length} icon={<CalendarDays />} accent="warning" />
        <Stat label="Messages envoyés" count={d.messaging?.messagesSent ?? 0} icon={<MessagesSquare />} accent="info"
          hint={`${d.messaging?.conversations ?? 0} discussions${d.messaging?.lastMessageAt ? ` · ${fmtRelative(d.messaging.lastMessageAt)}` : ''}`} />
        <Stat label="Signalements reçus" count={m.reportCount} icon={<Flag />} accent={m.reportCount > 0 ? 'danger' : 'brand'}
          tone={m.reportCount > 0 ? 'warning' : undefined} hint={`${d.reportsFiled} envoyés par ce membre`} />
      </Stagger>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <div className="flex gap-1 overflow-x-auto border-b border-line px-3" role="tablist">
            {tabs.filter((t) => t.show).map((t) => (
              <button key={t.key} role="tab" aria-selected={tab === t.key} onClick={() => setTab(t.key)}
                className={cx('relative flex shrink-0 items-center gap-2 px-3 py-3.5 text-sm font-medium transition-colors', tab === t.key ? 'text-brand' : 'text-muted hover:text-fg')}>
                {t.label}
                {t.count !== undefined && <span className={cx('rounded-full px-1.5 text-xs', tab === t.key ? 'bg-brand-soft' : 'bg-surface-2')}>{t.count}</span>}
                {tab === t.key && <motion.span layoutId="member-tab" className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-brand" />}
              </button>
            ))}
          </div>
          <AnimatePresence mode="wait">
            <motion.div key={tab} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -6 }} transition={{ duration: 0.18 }}>
              {tab === 'posts' && <Posts d={d} />}
              {tab === 'communities' && (memberships.length === 0 ? <Empty title="Aucune communauté" /> : (
                <ul className="divide-y divide-line">
                  {memberships.map((c) => (
                    <li key={c.id} className="flex items-center gap-3 px-5 py-3">
                      <span className="grid size-9 place-items-center rounded-lg bg-brand-soft text-brand"><UsersRound className="size-4" /></span>
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-medium">{c.name}</p>
                        <p className="text-xs text-muted">Depuis le {fmtDate(c.joinedAt)}</p>
                      </div>
                      {c.isPrivate && <Badge><Lock className="size-3" />Privée</Badge>}
                      <Badge tone={c.role === 'Owner' ? 'warning' : c.role === 'Moderator' ? 'info' : 'neutral'}>{COMMUNITY_ROLE_LABELS[c.role]}</Badge>
                    </li>
                  ))}
                </ul>
              ))}
              {tab === 'events' && (events.length === 0 ? <Empty title="Aucun événement" /> : (
                <ul className="divide-y divide-line">
                  {events.map((e) => {
                    const date = new Date(e.startsAt)
                    return (
                      <li key={e.id} className="flex items-center gap-4 px-5 py-3">
                        <span className="grid w-12 shrink-0 place-items-center rounded-lg bg-warning-soft py-1 text-warning">
                          <span className="text-lg font-semibold leading-none">{date.getDate()}</span>
                          <span className="text-[10px] uppercase">{date.toLocaleDateString('fr-FR', { month: 'short' })}</span>
                        </span>
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-medium">{e.title}</p>
                          <p className="text-xs text-muted">{e.city} · {e.participants} participants · {fmtDateTime(e.startsAt)}</p>
                        </div>
                        <Badge tone={e.isOrganizer ? 'brand' : 'neutral'}>{e.isOrganizer ? 'Organisateur' : 'Inscrit'}</Badge>
                      </li>
                    )
                  })}
                </ul>
              ))}
              {tab === 'reports' && (reports.length === 0 ? <Empty title="Aucun signalement" description="Personne n'a signalé ce membre." /> : (
                <ul className="divide-y divide-line">
                  {reports.map((r) => (
                    <li key={r.id} className="px-5 py-3">
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge tone="warning">{REASON_LABELS[r.reason]}</Badge>
                        <Badge tone={r.status === 'Open' ? 'danger' : 'neutral'}>{REPORT_STATUS_LABELS[r.status]}</Badge>
                        <span className="text-xs text-muted">{fmtRelative(r.createdAt)} · par {r.reporterName}</span>
                      </div>
                      {r.details && <p className="mt-1.5 text-sm">« {r.details} »</p>}
                    </li>
                  ))}
                </ul>
              ))}
              {tab === 'history' && (audit.isPending ? <Loading /> : (audit.data?.items.length ?? 0) === 0 ? (
                <Empty title="Aucune action" description="Aucune action d'administration sur ce membre." />
              ) : (
                <ol className="relative space-y-4 px-6 py-5">
                  <span aria-hidden className="absolute bottom-6 left-[29px] top-6 w-px bg-line" />
                  {audit.data!.items.map((a) => (
                    <li key={a.id} className="relative flex gap-4">
                      <span className="z-10 mt-0.5 grid size-3 shrink-0 place-items-center rounded-full bg-brand ring-4 ring-surface" />
                      <div className="min-w-0">
                        <p className="text-sm font-medium">{a.action}</p>
                        <p className="text-xs text-muted">{fmtDateTime(a.at)} · {a.adminName}</p>
                        {a.details && <p className="mt-0.5 text-sm text-muted">{a.details}</p>}
                      </div>
                    </li>
                  ))}
                </ol>
              ))}
            </motion.div>
          </AnimatePresence>
        </Card>

        <div className="space-y-6">
          <Card className="p-5">
            <h2 className="mb-3 font-semibold">Profil</h2>
            {d.bio ? <p className="mb-4 whitespace-pre-line text-sm">{d.bio}</p> : <p className="mb-4 text-sm text-muted">Pas de présentation.</p>}
            <dl className="space-y-2.5 text-sm">
              <Row label="Profession" value={d.profession || '—'} />
              <Row label="Résidence" value={`${m.city}, ${m.country}`} />
              <Row label="Origine" value={d.originCity || '—'} />
              <Row label="Inscription" value={fmtDate(m.joinedAt)} />
              <Row label="Email" value={m.isEmailVerified ? <span className="text-brand">Vérifié</span> : <span className="text-warning">Non vérifié</span>} />
            </dl>
            {(d.interests?.length ?? 0) > 0 && (
              <div className="mt-4 flex flex-wrap gap-1.5">{d.interests!.map((i) => <Badge key={i} tone="brand"><Heart className="size-3" />{i}</Badge>)}</div>
            )}
          </Card>

          <Card className="p-5">
            <h2 className="mb-3 font-semibold">Relations et appareils</h2>
            <div className="mb-4 grid grid-cols-2 gap-3">
              <MiniStat icon={<UserX className="size-4" />} value={d.blockedCount} label="membres bloqués" />
              <MiniStat icon={<Ban className="size-4" />} value={d.blockedByCount} label="l'ont bloqué" />
            </div>
            {devices.length === 0 ? <p className="text-sm text-muted">Aucun téléphone inscrit aux notifications.</p> : (
              <ul className="space-y-2">
                {devices.map((dev, i) => (
                  <li key={i} className="flex items-center gap-3 rounded-lg bg-surface-2 px-3 py-2 text-sm">
                    <Smartphone className="size-4 text-muted" />
                    <span className="flex-1 font-medium">{dev.platform === 'ios' ? 'iPhone' : dev.platform === 'android' ? 'Android' : dev.platform}</span>
                    <span className="text-xs text-muted">vu {fmtRelative(dev.lastSeenAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>
      </div>

      <ConfirmDialog open={!!confirm} busy={action.isPending}
        title={confirm === 'suspend' ? 'Suspendre ce membre ?' : 'Réactiver ce membre ?'}
        message={confirm === 'suspend'
          ? 'Le membre ne pourra plus se connecter et ses sessions ouvertes seront fermées immédiatement.'
          : 'Le membre pourra de nouveau se connecter.'}
        confirmLabel={confirm === 'suspend' ? 'Suspendre' : 'Réactiver'} danger={confirm === 'suspend'}
        reason={confirm === 'suspend' ? { label: 'Motif', required: true, placeholder: 'Ex. : harcèlement signalé à plusieurs reprises' } : undefined}
        onClose={() => setConfirm(null)} onConfirm={(reason) => confirm && action.mutate({ kind: confirm, reason })} />
    </Stagger>
  )
}

/** En-tête : couverture, photo en relief, identité et actions. */
function Profile({ d, actions }: { d: AdminMemberDetail; actions: ReactNode }) {
  const m = d.member
  const toast = useToast()
  const [copied, setCopied] = useState(false)
  const cover = d.coverImage?.startsWith('uploads/') ? apiUrl(d.coverImage) : null
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(m.email)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      toast("Copie impossible dans ce navigateur.", 'error')
    }
  }

  return (
    <motion.section variants={revealItem} className="mb-6 overflow-hidden rounded-2xl border border-line bg-surface shadow-[0_24px_48px_-32px_rgba(16,24,40,0.35)]">
      <div className="relative h-36 sm:h-44">
        {cover
          ? <img src={cover} alt="" className="absolute inset-0 size-full object-cover" />
          : <div className="absolute inset-0 bg-gradient-to-br from-[#0b9e71] via-[#087f5b] to-[#043d2c]">
              <div className="absolute inset-0 bg-[radial-gradient(circle_at_85%_20%,rgba(252,209,22,0.3),transparent_45%)]" />
            </div>}
        <div className="absolute inset-0 bg-gradient-to-t from-black/35 to-transparent" />
      </div>
      <div className="relative flex flex-col gap-4 px-5 pb-5 sm:flex-row sm:items-end sm:px-6">
        <motion.div className="-mt-12 shrink-0 rounded-full bg-surface p-1.5 shadow-xl sm:-mt-14" style={{ transformPerspective: 600 }}
          initial={{ rotateY: -90, opacity: 0 }} animate={{ rotateY: 0, opacity: 1 }} whileHover={{ rotateY: 15, rotateX: -8, scale: 1.04 }}
          transition={{ type: 'spring', stiffness: 160, damping: 16 }}>
          <Avatar first={m.firstName} last={m.lastName} color={m.avatarColor} image={m.avatarImage} size={96} />
        </motion.div>
        <div className="min-w-0 flex-1 sm:pb-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="truncate text-2xl font-semibold tracking-tight">{m.firstName} {m.lastName}</h1>
            <MemberStatus m={m} />
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted">
            <button onClick={copy} className="inline-flex items-center gap-1.5 hover:text-fg" title="Copier l'adresse">
              <Mail className="size-4" />{m.email}{copied ? <Check className="size-3.5 text-brand" /> : <Copy className="size-3.5 opacity-60" />}
            </button>
            <span className="inline-flex items-center gap-1.5"><MapPin className="size-4" />{m.city}, {m.country}</span>
            <span className="inline-flex items-center gap-1.5"><History className="size-4" />Membre depuis le {fmtDate(m.joinedAt)}</span>
          </div>
        </div>
        {actions && <div className="flex gap-2 sm:pb-1">{actions}</div>}
      </div>
    </motion.section>
  )
}

function Posts({ d }: { d: AdminMemberDetail }) {
  if (d.recentPosts.length === 0) return <Empty title="Aucune publication" />
  return (
    <ul className="divide-y divide-line">
      {d.recentPosts.map((p) => (
        <li key={p.id} className="flex gap-4 px-5 py-4">
          {p.imagePath?.startsWith('uploads/') && <img src={apiUrl(p.imagePath)} alt="" className="size-16 shrink-0 rounded-lg object-cover" />}
          <div className="min-w-0 flex-1">
            <p className="line-clamp-3 text-sm">{p.content}</p>
            <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-muted">
              <span>{fmtDateTime(p.createdAt)}</span>
              {p.communityName && <Badge>{p.communityName}</Badge>}
              <span>{p.likeCount} j'aime · {p.commentCount} comm.</span>
              {p.isHidden && <Badge tone="danger">Masquée</Badge>}
              {p.reportCount > 0 && <Badge tone="warning">{p.reportCount} signalement(s)</Badge>}
            </div>
          </div>
        </li>
      ))}
    </ul>
  )
}

function Row({ label, value }: { label: string; value: ReactNode }) {
  return <div className="flex justify-between gap-4"><dt className="text-muted">{label}</dt><dd className="text-right font-medium">{value}</dd></div>
}

function MiniStat({ icon, value, label }: { icon: ReactNode; value: number; label: string }) {
  return (
    <div className="rounded-lg bg-surface-2 p-3">
      <div className="flex items-center gap-2 text-muted">{icon}<span className="text-lg font-semibold text-fg">{value}</span></div>
      <p className="mt-0.5 text-xs text-muted">{label}</p>
    </div>
  )
}
