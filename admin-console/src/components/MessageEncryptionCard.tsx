import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LockKeyhole } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/auth'
import { useToast } from '@/components/toast'
import { Button, Card, ConfirmDialog } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmtNumber, fmtRelative } from '@/lib/format'

interface EncryptionStatus {
  totalMessages: number
  plainMessages: number
  running: boolean
  processed: number
  startedAt: string | null
  finishedAt: string | null
  error: string | null
  startedBy: string | null
}

const ROLES = ['SuperAdmin', 'Admin', 'Developer']

/** Chiffrement des messages enregistrés avant l'activation : lancé à la main, suivi en direct. */
export function MessageEncryptionCard() {
  const { me } = useAuth()
  const toast = useToast()
  const qc = useQueryClient()
  const [confirming, setConfirming] = useState(false)
  const allowed = !!me && ROLES.includes(me.role)
  const status = useQuery({
    queryKey: ['message-encryption'],
    queryFn: () => api<EncryptionStatus>('api/admin/messaging/encryption'),
    enabled: allowed,
    refetchInterval: (q) => (q.state.data?.running ? 2000 : false),
  })
  const run = useMutation({
    mutationFn: () => api<EncryptionStatus>('api/admin/messaging/encryption/run', { method: 'POST' }),
    onSuccess: (s) => {
      qc.setQueryData(['message-encryption'], s)
      setConfirming(false)
      toast('Chiffrement lancé : vous pouvez quitter la page, il continue en arrière-plan.', 'success')
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const s = status.data
  if (!allowed || !s) return null
  const done = s.plainMessages === 0
  return (
    <Card className="flex flex-wrap items-center gap-4 px-5 py-4">
      <LockKeyhole className={done ? 'size-5 text-brand' : 'size-5 text-warning'} />
      <div className="min-w-0 flex-1">
        <p className="font-semibold">Chiffrement des messages</p>
        <p className="text-sm text-muted">
          {s.running
            ? `En cours : ${fmtNumber(s.processed)} message(s) chiffré(s)…`
            : done
              ? `Les ${fmtNumber(s.totalMessages)} messages sont chiffrés en base.`
              : `${fmtNumber(s.plainMessages)} message(s) enregistré(s) avant le chiffrement sont encore lisibles en base.`}
          {s.error && ` ${s.error}`}
          {!s.running && s.finishedAt && ` Dernière exécution ${fmtRelative(s.finishedAt)}${s.startedBy ? ` par ${s.startedBy}` : ''}.`}
        </p>
      </div>
      {!done && <Button variant="primary" loading={s.running || run.isPending} onClick={() => setConfirming(true)}>Chiffrer les messages existants</Button>}
      <ConfirmDialog open={confirming} busy={run.isPending} title="Chiffrer les messages existants ?" confirmLabel="Lancer"
        message="La tâche chiffre les anciens messages par lots, en arrière-plan. Les membres continuent d'utiliser la messagerie normalement."
        onClose={() => setConfirming(false)} onConfirm={() => run.mutate()} />
    </Card>
  )
}
