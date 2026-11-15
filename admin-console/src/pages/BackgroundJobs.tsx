import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, History, Loader2, Play } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, Empty, ErrorBox, Loading, Modal, PageHeader, Table } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmtDateTime, fmtRelative } from '@/lib/format'
import type { BackgroundJob, BackgroundJobsOverview, JobRun } from '@/lib/types'

const KEY = ['background-jobs']

const duration = (ms: number) => (ms < 1000 ? `${ms} ms` : ms < 60_000 ? `${(ms / 1000).toFixed(1)} s` : `${Math.round(ms / 60_000)} min`)

function RunBadge({ run }: { run: JobRun }) {
  return run.status === 'Succeeded' ? <Badge tone="brand">Réussie</Badge> : <Badge tone="danger">Échec</Badge>
}

function StateBadge({ job }: { job: BackgroundJob }) {
  if (job.running) return <Badge tone="info"><Loader2 className="size-3 animate-spin" /> En cours</Badge>
  if (!job.enabled) return <Badge>Désactivée</Badge>
  if (!job.scheduled) return <Badge tone="warning">Non planifiée</Badge>
  return null
}

/** Tâches de fond de l'API : horaire, dernières exécutions, lancement manuel (journalisé). */
export function BackgroundJobsPage() {
  const toast = useToast()
  const qc = useQueryClient()
  const [toRun, setToRun] = useState<BackgroundJob | null>(null)
  const [detail, setDetail] = useState<string | null>(null)
  const q = useQuery({
    queryKey: KEY,
    queryFn: () => api<BackgroundJobsOverview>('api/admin/background-jobs'),
    refetchInterval: (query) => (query.state.data?.jobs.some((j) => j.running) ? 2000 : 30_000),
  })
  const run = useMutation({
    mutationFn: (job: BackgroundJob) => api<BackgroundJob>(`api/admin/background-jobs/${encodeURIComponent(job.key)}/run`, { method: 'POST' }),
    onSuccess: (_, job) => {
      toast(`« ${job.name} » lancée : elle continue en arrière-plan.`)
      setToRun(null)
      // L'exécution démarre juste après la réponse
      setTimeout(() => qc.invalidateQueries({ queryKey: KEY }), 400)
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  if (q.isLoading) return <Loading />
  if (q.error) return <ErrorBox message={errorMessage(q.error)} onRetry={() => q.refetch()} />
  const data = q.data!
  const failed = data.jobs.filter((j) => j.lastRun?.status === 'Failed')
  const selected = data.jobs.find((j) => j.key === detail) ?? null

  return (
    <div className="space-y-5">
      <PageHeader title="Tâches planifiées"
        description="Sauvegardes, imports, rappels et nettoyages exécutés automatiquement par l'API. Les horaires sont en UTC ; l'historique repart à zéro au redémarrage de l'API." />

      {!data.schedulerEnabled && (
        <Card className="flex items-center gap-3 px-5 py-4">
          <AlertTriangle className="size-5 text-warning" />
          <p className="text-sm">Planificateur désactivé (BackgroundJobs__Enabled=false) : les tâches ne tournent que si elles sont lancées d'ici.</p>
        </Card>
      )}
      {failed.length > 0 && (
        <Card className="flex items-center gap-3 px-5 py-4">
          <AlertTriangle className="size-5 text-danger" />
          <p className="text-sm">Dernière exécution en échec : {failed.map((j) => j.name).join(', ')}.</p>
        </Card>
      )}

      {data.jobs.length === 0 ? <Empty title="Aucune tâche planifiée" /> : (
        <Table head={['Tâche', 'Horaire', 'Dernière exécution', 'Prochaine', '']}>
          {data.jobs.map((job) => (
            <tr key={job.key} className="align-top">
              <td className="max-w-md px-4 py-3">
                <button type="button" className="text-left" onClick={() => setDetail(job.key)}>
                  <span className="flex flex-wrap items-center gap-2 font-medium">{job.name} <Badge>{job.module}</Badge> <StateBadge job={job} /></span>
                  <span className="mt-1 block text-xs text-muted">{job.description}</span>
                </button>
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm">{job.schedule}</td>
              <td className="px-4 py-3 text-sm">
                {job.lastRun ? (
                  <div className="space-y-1">
                    <span className="flex items-center gap-2"><RunBadge run={job.lastRun} /> <span className="text-muted">{fmtRelative(job.lastRun.finishedAt)}</span></span>
                    <p className="line-clamp-2 text-xs text-muted">{job.lastRun.summary}</p>
                  </div>
                ) : <span className="text-muted">—</span>}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm">{job.nextRunAt ? fmtDateTime(job.nextRunAt) : '—'}</td>
              <td className="px-4 py-3 text-right">
                <Button icon={<Play className="size-4" />} disabled={!job.enabled} loading={job.running} onClick={() => setToRun(job)}>Lancer</Button>
              </td>
            </tr>
          ))}
        </Table>
      )}

      <ConfirmDialog open={!!toRun} busy={run.isPending} title={`Lancer « ${toRun?.name ?? ''} » maintenant ?`} confirmLabel="Lancer"
        message={`${toRun?.description ?? ''} Le lancement est enregistré dans le journal d'audit.`}
        onClose={() => setToRun(null)} onConfirm={() => toRun && run.mutate(toRun)} />

      <Modal open={!!selected} title={selected?.name ?? ''} onClose={() => setDetail(null)} wide>
        {selected && (
          <div className="space-y-4">
            <p className="text-sm text-muted">{selected.description}</p>
            <p className="text-sm"><span className="text-muted">Clé :</span> <code>{selected.key}</code> · {selected.schedule}</p>
            {selected.history.length === 0 ? <Empty title="Aucune exécution depuis le démarrage de l'API" /> : (
              <Table head={['Début', 'Durée', 'Résultat', 'Lancement']}>
                {selected.history.map((r) => (
                  <tr key={r.startedAt} className="align-top">
                    <td className="whitespace-nowrap px-4 py-2 text-sm">{fmtDateTime(r.startedAt)}</td>
                    <td className="whitespace-nowrap px-4 py-2 text-sm">{duration(r.durationMs)}</td>
                    <td className="px-4 py-2 text-sm"><span className="flex items-start gap-2"><RunBadge run={r} /> <span className="text-xs text-muted">{r.summary}</span></span></td>
                    <td className="whitespace-nowrap px-4 py-2 text-xs text-muted"><History className="mr-1 inline size-3" />{r.trigger}</td>
                  </tr>
                ))}
              </Table>
            )}
          </div>
        )}
      </Modal>
    </div>
  )
}
