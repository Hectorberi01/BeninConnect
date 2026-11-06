import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Flag } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/auth'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, cx, Empty, ErrorBox, Loading, PageHeader, Pagination } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDateTime, fmtRelative } from '@/lib/format'
import { decisionsFor, REASON_LABELS, TARGET_LABELS } from '@/lib/permissions'
import type { AdminReport, Paged, ReportDecision, ReportStatus } from '@/lib/types'

const TABS: { key: ReportStatus | 'all'; label: string }[] = [
  { key: 'Open', label: 'À traiter' },
  { key: 'Resolved', label: 'Traités' },
  { key: 'Dismissed', label: 'Classés' },
  { key: 'all', label: 'Tous' },
]
const TAKE = 20

export function ReportsPage() {
  const { can } = useAuth()
  const toast = useToast()
  const qc = useQueryClient()
  const [status, setStatus] = useState<ReportStatus | 'all'>('Open')
  const [skip, setSkip] = useState(0)
  const [pending, setPending] = useState<{ report: AdminReport; decision: ReportDecision; label: string; danger?: boolean } | null>(null)

  const q = useQuery({
    queryKey: ['reports', status, skip],
    queryFn: () => api<Paged<AdminReport>>(`api/admin/reports${qs({ status: status === 'all' ? null : status, skip, take: TAKE })}`),
  })

  const resolve = useMutation({
    mutationFn: ({ id, decision, note }: { id: string; decision: ReportDecision; note: string }) =>
      api<AdminReport>(`api/admin/reports/${id}/resolve`, { method: 'POST', body: { decision, note } }),
    onSuccess: (r) => {
      toast(r.resolution || 'Signalement traité.')
      setPending(null)
      ;['reports', 'reports-open-count', 'stats', 'posts', 'members'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }))
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  return (
    <>
      <PageHeader title="Signalements" description="Contenus et membres signalés par la communauté. Une décision s'applique à tous les signalements de la même cible." />
      <div className="mb-4 flex flex-wrap gap-2" role="tablist">
        {TABS.map((t) => (
          <button key={t.key} role="tab" aria-selected={status === t.key} onClick={() => { setStatus(t.key); setSkip(0) }}
            className={cx('rounded-full px-3.5 py-1.5 text-sm font-medium', status === t.key ? 'bg-brand text-white' : 'border border-line bg-surface text-muted hover:text-fg')}>
            {t.label}
          </button>
        ))}
      </div>

      <Card>
        {q.isPending ? <Loading /> : q.error ? <ErrorBox message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : q.data.items.length === 0 ? (
          <Empty title={status === 'Open' ? 'File vide' : 'Aucun signalement'} description={status === 'Open' ? 'Aucun signalement en attente.' : undefined} />
        ) : (
          <>
            <ul className="divide-y divide-line">
              {q.data.items.map((r) => (
                <li key={r.id} className="flex flex-col gap-4 p-4 sm:p-5 lg:flex-row lg:items-start">
                  <div className="grid size-10 shrink-0 place-items-center rounded-full bg-warning-soft text-warning"><Flag className="size-4.5" /></div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge>{TARGET_LABELS[r.targetType]}</Badge>
                      <Badge tone="warning">{REASON_LABELS[r.reason]}</Badge>
                      {r.sameTargetOpenCount > 1 && <Badge tone="danger">{r.sameTargetOpenCount} signalements</Badge>}
                      {r.status !== 'Open' && <Badge tone={r.status === 'Resolved' ? 'brand' : 'neutral'}>{r.status === 'Resolved' ? 'Traité' : 'Classé'}</Badge>}
                      <span className="text-xs text-muted" title={fmtDateTime(r.createdAt)}>{fmtRelative(r.createdAt)}</span>
                    </div>
                    <p className={cx('mt-2 text-sm', !r.targetExists && 'italic text-muted')}>{r.targetPreview}</p>
                    <p className="mt-1 text-xs text-muted">
                      {r.targetAuthorName && r.targetType !== 'User' ? `Auteur : ${r.targetAuthorName} · ` : ''}Signalé par {r.reporterName}
                    </p>
                    {r.details && <p className="mt-2 rounded-lg bg-surface-2 px-3 py-2 text-sm">« {r.details} »</p>}
                    {r.status !== 'Open' && <p className="mt-2 text-xs text-muted">Décision : {r.resolution} · {fmtDateTime(r.closedAt)}</p>}
                  </div>
                  {r.status === 'Open' && (
                    <div className="flex flex-wrap gap-2 lg:w-64 lg:flex-col">
                      {decisionsFor(r.targetType, can('CommunitiesManage'))
                        .filter((d) => d.value === 'dismiss' || r.targetExists)
                        .map((d) => (
                          <Button key={d.value} variant={d.value === 'dismiss' ? 'secondary' : d.danger ? 'dangerSoft' : 'primary'}
                            onClick={() => setPending({ report: r, decision: d.value, label: d.label, danger: d.danger })}>{d.label}</Button>
                        ))}
                    </div>
                  )}
                </li>
              ))}
            </ul>
            <Pagination skip={skip} take={TAKE} total={q.data.totalCount} onChange={setSkip} />
          </>
        )}
      </Card>

      <ConfirmDialog open={!!pending} title={pending?.label ?? ''} danger={pending?.danger} busy={resolve.isPending} confirmLabel="Confirmer"
        message={pending ? `${TARGET_LABELS[pending.report.targetType]} : « ${pending.report.targetPreview} »` : ''}
        reason={{ label: pending?.decision === 'suspend' ? 'Motif de la suspension' : 'Note (facultative)', required: pending?.decision === 'suspend',
          placeholder: pending?.decision === 'suspend' ? 'Ex. : spam répété malgré avertissement' : 'Visible dans le journal d’audit' }}
        onClose={() => setPending(null)}
        onConfirm={(note) => pending && resolve.mutate({ id: pending.report.id, decision: pending.decision, note })} />
    </>
  )
}
