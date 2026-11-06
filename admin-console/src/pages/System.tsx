import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, RotateCcw } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/auth'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, ErrorBox, Loading, PageHeader, Stat, Table } from '@/components/ui'
import { api, apiUrl, errorMessage } from '@/lib/api'
import { fmtBytes, fmtDateTime, fmtNumber } from '@/lib/format'
import type { SystemInfo } from '@/lib/types'

export function SystemPage() {
  const { can, logout } = useAuth()
  const toast = useToast()
  const qc = useQueryClient()
  const [confirm, setConfirm] = useState(false)
  const info = useQuery({ queryKey: ['system'], queryFn: () => api<SystemInfo>('api/admin/system'), refetchInterval: 30_000 })
  const health = useQuery({
    queryKey: ['health'],
    queryFn: async () => (await fetch(apiUrl('health'), { headers: { 'ngrok-skip-browser-warning': '1' } })).text(),
    refetchInterval: 30_000,
  })
  const reset = useMutation({
    mutationFn: () => api('api/admin/system/reset-demo', { method: 'POST' }),
    onSuccess: () => {
      toast('Base réinitialisée : reconnectez-vous.')
      qc.clear()
      logout()
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  const s = info.data
  return (
    <>
      <PageHeader title="Système" description="État de l'API et de la base de données."
        actions={<a href={apiUrl('openapi/v1.json')} target="_blank" rel="noreferrer"><Button icon={<ExternalLink className="size-4" />}>Schéma OpenAPI</Button></a>} />
      {info.isPending ? <Loading /> : info.error || !s ? <ErrorBox message={errorMessage(info.error)} onRetry={() => info.refetch()} /> : (
        <div className="space-y-6">
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <Stat label="API" value={health.data === 'Healthy' ? 'En ligne' : health.data ?? '…'} hint={`v${s.version} · ${s.environment}`} />
            <Stat label="Base de données" value={s.databaseOnline ? 'Connectée' : 'Injoignable'} tone={s.databaseOnline ? undefined : 'danger'} hint={s.database} />
            <Stat label="Démarrée" value={s.uptime} hint={fmtDateTime(s.startedAt)} />
            <Stat label="Photos envoyées" value={fmtNumber(s.mediaFiles)} hint={fmtBytes(s.mediaBytes)} />
          </div>
          <div className="grid gap-6 lg:grid-cols-3">
            <Card className="lg:col-span-2">
              <div className="flex items-center justify-between px-5 py-4"><h2 className="font-semibold">Volume par table</h2><Badge>{s.runtime}</Badge></div>
              <Table head={['Table', 'Lignes']}>
                {s.tables.map((t) => (
                  <tr key={t.label}><td className="px-4 py-2.5">{t.label}</td><td className="px-4 py-2.5 text-muted">{fmtNumber(t.count)}</td></tr>
                ))}
              </Table>
            </Card>
            <Card className="p-5">
              <h2 className="font-semibold">Données de démonstration</h2>
              {s.canResetDemo && can('SystemReset') ? (
                <>
                  <p className="mt-2 text-sm text-muted">Recrée la base avec le jeu de démonstration (membres, publications, comptes admin). Toutes les données actuelles sont effacées.</p>
                  <Button className="mt-4" variant="danger" icon={<RotateCcw className="size-4" />} onClick={() => setConfirm(true)}>Réinitialiser la base</Button>
                </>
              ) : (
                <p className="mt-2 text-sm text-muted">Disponible uniquement en environnement de développement, pour le rôle Développeur ou SuperAdmin.</p>
              )}
            </Card>
          </div>
        </div>
      )}
      <ConfirmDialog open={confirm} danger busy={reset.isPending} title="Réinitialiser toute la base ?" confirmLabel="Tout effacer et recharger"
        message="Action irréversible : toutes les données (membres, contenus, comptes admin, journal) sont remplacées par le jeu de démonstration."
        onClose={() => setConfirm(false)} onConfirm={() => reset.mutate()} />
    </>
  )
}
