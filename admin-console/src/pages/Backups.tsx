import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle, Archive, CalendarClock, Check, Cloud, CloudOff, CloudUpload, Copy, DatabaseBackup, Download, HardDrive, History, Image, Loader2,
  RotateCcw, Trash2,
} from 'lucide-react'
import { motion } from 'motion/react'
import { useState } from 'react'
import { Stagger } from '@/components/motion'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, cx, Empty, ErrorBox, Loading, Modal, PageHeader, Stat, Table } from '@/components/ui'
import { api, download, errorMessage } from '@/lib/api'
import { fmtBytes, fmtDateTime, fmtRelative } from '@/lib/format'
import type { BackupFile, BackupKind, BackupOverview, OffsiteStatus, RemoteBackup } from '@/lib/types'

const KIND_LABELS: Record<BackupKind, string> = {
  Manual: 'Manuelle', Daily: 'Quotidienne', Weekly: 'Hebdomadaire', Monthly: 'Mensuelle', Media: 'Photos',
}
const KIND_TONES: Record<BackupKind, 'brand' | 'info' | 'warning' | 'neutral'> = {
  Manual: 'brand', Daily: 'info', Weekly: 'info', Monthly: 'warning', Media: 'neutral',
}
const FOLDERS: Record<BackupKind, string> = { Manual: 'manual', Daily: 'daily', Weekly: 'weekly', Monthly: 'monthly', Media: 'media' }

type Filter = 'All' | BackupKind

/** Au-delà de 26 h sans sauvegarde de la base, la sauvegarde automatique est considérée en retard. */
const LATE_HOURS = 26

export function BackupsPage() {
  const toast = useToast()
  const qc = useQueryClient()
  const [filter, setFilter] = useState<Filter>('All')
  const [toDelete, setToDelete] = useState<BackupFile | null>(null)
  const [toRestore, setToRestore] = useState<BackupFile | null>(null)
  const [downloading, setDownloading] = useState<string | null>(null)
  const q = useQuery({ queryKey: ['backups'], queryFn: () => api<BackupOverview>('api/admin/backups'), refetchInterval: 60_000 })

  const create = useMutation({
    mutationFn: () => api<BackupFile>('api/admin/backups', { method: 'POST' }),
    onSuccess: (f) => {
      toast(`Sauvegarde créée : ${f.name} (${fmtBytes(f.size)}).`)
      qc.invalidateQueries({ queryKey: ['backups'] })
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const remove = useMutation({
    mutationFn: (f: BackupFile) => api(`api/admin/backups/Manual/${encodeURIComponent(f.name)}`, { method: 'DELETE' }),
    onSuccess: () => {
      toast('Sauvegarde supprimée.')
      setToDelete(null)
      qc.invalidateQueries({ queryKey: ['backups'] })
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  const sync = useMutation({
    mutationFn: () => api<{ uploaded: number; deleted: number }>('api/admin/backups/offsite/sync', { method: 'POST' }),
    onSuccess: (r) => {
      toast(r.uploaded || r.deleted ? `Copie distante à jour : ${r.uploaded} envoyée(s), ${r.deleted} supprimée(s).` : 'Copie distante déjà à jour.')
      qc.invalidateQueries({ queryKey: ['backups'] })
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  const getRemote = async (r: RemoteBackup) => {
    setDownloading(`remote:${r.name}`)
    try {
      await download(`api/admin/backups/offsite/${r.kind}/${encodeURIComponent(r.name)}`, r.name)
    } catch (e) {
      toast(errorMessage(e), 'error')
    } finally {
      setDownloading(null)
    }
  }

  const get = async (f: BackupFile) => {
    setDownloading(f.name)
    try {
      await download(`api/admin/backups/${f.kind}/${encodeURIComponent(f.name)}`, f.name)
    } catch (e) {
      toast(errorMessage(e), 'error')
    } finally {
      setDownloading(null)
    }
  }

  const o = q.data
  const files = o?.files ?? []
  const shown = filter === 'All' ? files : files.filter((f) => f.kind === filter)
  const hoursSince = o?.lastDatabaseBackupAt ? (q.dataUpdatedAt - new Date(o.lastDatabaseBackupAt).getTime()) / 3_600_000 : null
  const late = hoursSince === null || hoursSince > LATE_HOURS
  const counts = files.reduce<Record<string, number>>((acc, f) => ({ ...acc, [f.kind]: (acc[f.kind] ?? 0) + 1 }), {})

  return (
    <>
      <PageHeader title="Sauvegardes" description="Copies de la base de données et des photos conservées sur le serveur."
        actions={
          <Button variant="primary" loading={create.isPending} disabled={!o?.canCreate} onClick={() => create.mutate()}
            icon={<DatabaseBackup className="size-4" />}>Sauvegarder maintenant</Button>
        } />

      {q.isPending ? <Loading /> : q.error || !o ? <ErrorBox message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : (
        <div className="space-y-6">
          {!o.directoryAvailable && (
            <Warning title="Dossier des sauvegardes absent">
              L'API ne voit pas le dossier <code>/backups</code>. En production, il est monté par <code>docker-compose.prod.yml</code> (volume <code>./backups</code>).
            </Warning>
          )}
          {o.directoryAvailable && !o.canCreate && (
            <Warning title="Sauvegarde manuelle indisponible">
              <code>pg_dump</code> n'est pas installé dans l'image de l'API. Les sauvegardes automatiques restent listées et téléchargeables.
            </Warning>
          )}

          <Stagger className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat label="Dernière sauvegarde" icon={<History />} accent={late ? 'danger' : 'brand'} tone={late ? 'danger' : undefined}
              value={o.lastDatabaseBackupAt ? fmtRelative(o.lastDatabaseBackupAt) : 'Aucune'}
              hint={o.lastDatabaseBackupAt ? (late ? `En retard · ${fmtDateTime(o.lastDatabaseBackupAt)}` : fmtDateTime(o.lastDatabaseBackupAt)) : 'Lancez une sauvegarde manuelle'} />
            <Stat label="Sauvegardes" count={files.length} icon={<Archive />} accent="info"
              hint={`${counts.Manual ?? 0} manuelles · ${(counts.Daily ?? 0) + (counts.Weekly ?? 0) + (counts.Monthly ?? 0)} automatiques`} />
            <Stat label="Espace utilisé" value={fmtBytes(o.totalBytes)} icon={<HardDrive />} accent="violet" hint="Base et photos" />
            <Stat label="Espace libre" value={o.freeBytes !== null ? fmtBytes(o.freeBytes) : '—'} icon={<HardDrive />}
              accent={o.freeBytes !== null && o.freeBytes < 2 * 1024 ** 3 ? 'warning' : 'brand'} hint="Sur le disque du serveur" />
          </Stagger>

          <div className="grid gap-6 lg:grid-cols-3">
            <Card className="lg:col-span-2">
              <div className="flex flex-wrap gap-1.5 border-b border-line p-3">
                {(['All', 'Manual', 'Daily', 'Weekly', 'Monthly', 'Media'] as Filter[]).map((k) => {
                  const n = k === 'All' ? files.length : counts[k] ?? 0
                  if (k !== 'All' && n === 0) return null
                  return (
                    <button key={k} onClick={() => setFilter(k)}
                      className={cx('relative rounded-full px-3 py-1.5 text-sm font-medium transition-colors', filter === k ? 'text-white' : 'text-muted hover:text-fg')}>
                      {filter === k && <motion.span layoutId="backup-filter" className="absolute inset-0 rounded-full bg-brand" transition={{ type: 'spring', stiffness: 400, damping: 32 }} />}
                      <span className="relative">{k === 'All' ? 'Toutes' : KIND_LABELS[k]} · {n}</span>
                    </button>
                  )
                })}
              </div>
              {shown.length === 0 ? (
                <Empty title="Aucune sauvegarde" description={o.directoryAvailable ? 'La première sauvegarde automatique a lieu chaque nuit.' : undefined} />
              ) : (
                <Table head={['Fichier', 'Type', 'Date', 'Taille', ...(o.offsite ? ['Hors serveur'] : []), '']}>
                  {shown.map((f, i) => (
                    <motion.tr key={`${f.kind}/${f.name}`} initial={{ opacity: 0, x: -8 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: Math.min(i, 12) * 0.03 }}
                      className="hover:bg-surface-2">
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          <span className={cx('grid size-8 shrink-0 place-items-center rounded-lg', f.kind === 'Media' ? 'bg-surface-2 text-muted' : 'bg-brand-soft text-brand')}>
                            {f.kind === 'Media' ? <Image className="size-4" /> : <DatabaseBackup className="size-4" />}
                          </span>
                          <span className="truncate font-mono text-xs">{f.name}</span>
                        </div>
                      </td>
                      <td className="px-4 py-3"><Badge tone={KIND_TONES[f.kind]}>{KIND_LABELS[f.kind]}</Badge></td>
                      <td className="whitespace-nowrap px-4 py-3 text-muted"><span title={fmtDateTime(f.createdAt)}>{fmtRelative(f.createdAt)}</span></td>
                      <td className="whitespace-nowrap px-4 py-3 text-muted">{fmtBytes(f.size)}</td>
                      {o.offsite && (
                        <td className="whitespace-nowrap px-4 py-3">
                          {f.offsite
                            ? <span className="inline-flex items-center gap-1.5 text-xs font-medium text-brand"><Cloud className="size-4" />Copiée</span>
                            : <span className="inline-flex items-center gap-1.5 text-xs text-muted"><CloudOff className="size-4" />En attente</span>}
                        </td>
                      )}
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-1">
                          <IconAction label="Télécharger" onClick={() => get(f)}>
                            {downloading === f.name ? <Loader2 className="size-4 animate-spin" /> : <Download className="size-4" />}
                          </IconAction>
                          {f.kind !== 'Media' && <IconAction label="Restaurer" onClick={() => setToRestore(f)}><RotateCcw className="size-4" /></IconAction>}
                          {f.canDelete && <IconAction label="Supprimer" danger onClick={() => setToDelete(f)}><Trash2 className="size-4" /></IconAction>}
                        </div>
                      </td>
                    </motion.tr>
                  ))}
                </Table>
              )}
            </Card>

            <div className="space-y-6">
              <OffsiteCard offsite={o.offsite} syncing={sync.isPending} onSync={() => sync.mutate()} />
              <Card className="p-5">
                <h2 className="mb-3 flex items-center gap-2 font-semibold"><CalendarClock className="size-4 text-brand" />Planification</h2>
                <ul className="space-y-3 text-sm">
                  <Plan title="Base de données" text="Chaque nuit, conservée 7 jours, 4 semaines et 6 mois (conteneur backup)." />
                  <Plan title="Photos" text="Chaque nuit à 3 h, conservées 7 jours (volume local ; les médias sur R2 ne sont pas copiés ici)." />
                  <Plan title="Hors du serveur" text="Si un bucket est configuré : copie automatique de chaque nouvelle sauvegarde, et dès la création d'une sauvegarde manuelle." />
                  <Plan title="Manuelles" text="Créées depuis cette page, conservées jusqu'à leur suppression." />
                </ul>
                {o.pgDumpVersion && <p className="mt-4 text-xs text-muted">{o.pgDumpVersion}</p>}
              </Card>
              <Card className="p-5 text-sm">
                <h2 className="mb-2 flex items-center gap-2 font-semibold"><AlertTriangle className="size-4 text-warning" />Bonnes pratiques</h2>
                <ul className="list-disc space-y-1.5 pl-5 text-muted">
                  <li>Sauvegardez manuellement avant une migration ou une opération risquée.</li>
                  <li>Téléchargez régulièrement une copie hors du serveur.</li>
                  <li>Les fichiers contiennent toutes les données des membres : à stocker de façon sécurisée.</li>
                </ul>
              </Card>
            </div>
          </div>
        </div>
      )}

      {o?.offsite && o.offsite.remote.some((r) => !r.isLocal) && (
        <Card className="mt-6">
          <div className="flex items-center justify-between px-5 py-4">
            <h2 className="flex items-center gap-2 font-semibold"><Cloud className="size-4 text-info" />Copies présentes uniquement hors du serveur</h2>
            <span className="text-sm text-muted">Conservées {o.offsite.retentionDays} jours après leur rotation sur le serveur</span>
          </div>
          <Table head={['Fichier', 'Type', 'Copiée', 'Taille', '']}>
            {o.offsite.remote.filter((r) => !r.isLocal).map((r) => (
              <tr key={`${r.kind}/${r.name}`} className="hover:bg-surface-2">
                <td className="px-4 py-3 font-mono text-xs">{r.name}</td>
                <td className="px-4 py-3"><Badge tone={KIND_TONES[r.kind]}>{KIND_LABELS[r.kind]}</Badge></td>
                <td className="whitespace-nowrap px-4 py-3 text-muted"><span title={fmtDateTime(r.uploadedAt)}>{fmtRelative(r.uploadedAt)}</span></td>
                <td className="whitespace-nowrap px-4 py-3 text-muted">{fmtBytes(r.size)}</td>
                <td className="px-4 py-3">
                  <div className="flex justify-end">
                    <IconAction label="Télécharger" onClick={() => getRemote(r)}>
                      {downloading === `remote:${r.name}` ? <Loader2 className="size-4 animate-spin" /> : <Download className="size-4" />}
                    </IconAction>
                  </div>
                </td>
              </tr>
            ))}
          </Table>
        </Card>
      )}

      <ConfirmDialog open={!!toDelete} danger busy={remove.isPending} title="Supprimer cette sauvegarde ?" confirmLabel="Supprimer"
        message={`${toDelete?.name ?? ''} sera supprimée définitivement du serveur.`}
        onClose={() => setToDelete(null)} onConfirm={() => toDelete && remove.mutate(toDelete)} />
      <RestoreGuide file={toRestore} onClose={() => setToRestore(null)} />
    </>
  )
}

/** Procédure de restauration : volontairement manuelle (opération destructive, faite sur le serveur). */
function RestoreGuide({ file, onClose }: { file: BackupFile | null; onClose: () => void }) {
  const db = file?.name.replace(/-\d{6,8}.*$/, '') || 'beninconnect'
  const path = file ? `backups/${FOLDERS[file.kind]}/${file.name}` : ''
  const steps = [
    { title: 'Se placer dans le dossier de l\'application', cmd: 'cd /opt/beninconnect/server' },
    { title: "Arrêter l'API", cmd: 'docker compose -f docker-compose.prod.yml stop api' },
    { title: 'Recréer une base vide (remplacer UTILISATEUR par DB_USER du .env)', cmd: `sudo -u postgres psql -c 'DROP DATABASE "${db}" WITH (FORCE)' -c 'CREATE DATABASE "${db}" OWNER "UTILISATEUR"'` },
    { title: 'Charger la sauvegarde (mot de passe : DB_PASSWORD)', cmd: `gunzip -c ${path} | psql "host=localhost dbname=${db} user=UTILISATEUR"` },
    { title: "Redémarrer l'API", cmd: 'docker compose -f docker-compose.prod.yml start api' },
  ]
  return (
    <Modal open={!!file} wide title="Restaurer une sauvegarde" onClose={onClose} footer={<Button onClick={onClose}>Fermer</Button>}>
      <div className="mb-4 flex gap-3 rounded-lg bg-danger-soft px-4 py-3 text-sm text-danger">
        <AlertTriangle className="mt-0.5 size-4 shrink-0" />
        <p>La restauration <b>remplace toutes les données actuelles</b> par celles du {file ? fmtDateTime(file.createdAt) : ''}.
          Faites d'abord une sauvegarde manuelle de l'état actuel. Elle se fait sur le serveur, en SSH, pour éviter toute erreur de manipulation.</p>
      </div>
      <ol className="space-y-3">
        {steps.map((s, i) => (
          <li key={i}>
            <p className="mb-1 text-sm font-medium"><span className="mr-2 inline-grid size-5 place-items-center rounded-full bg-brand text-xs text-white">{i + 1}</span>{s.title}</p>
            <Command text={s.cmd} />
          </li>
        ))}
      </ol>
      <p className="mt-4 text-xs text-muted">Base hébergée ailleurs que sur le VPS : remplacer <code>localhost</code> par DB_HOST et recréer la base depuis la console du fournisseur.</p>
    </Modal>
  )
}

function Command({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)
  return (
    <div className="group relative rounded-lg bg-[#0f1a17] px-3 py-2.5 pr-11 font-mono text-xs text-[#d9efe7]">
      <code className="block overflow-x-auto whitespace-pre">{text}</code>
      <button aria-label="Copier" onClick={async () => {
        try {
          await navigator.clipboard.writeText(text)
          setCopied(true)
          setTimeout(() => setCopied(false), 1500)
        } catch { /* presse-papiers indisponible */ }
      }} className="absolute right-2 top-2 rounded-md p-1 text-[#7fa89a] hover:bg-white/10 hover:text-white">
        {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
      </button>
    </div>
  )
}

function IconAction({ label, onClick, danger, children }: { label: string; onClick: () => void; danger?: boolean; children: React.ReactNode }) {
  return (
    <button aria-label={label} title={label} onClick={onClick}
      className={cx('rounded-lg p-2 transition-colors', danger ? 'text-muted hover:bg-danger-soft hover:text-danger' : 'text-muted hover:bg-brand-soft hover:text-brand')}>
      {children}
    </button>
  )
}

function Plan({ title, text }: { title: string; text: string }) {
  return (
    <li className="flex gap-3">
      <span className="mt-1.5 size-2 shrink-0 rounded-full bg-brand shadow-[0_0_0_4px_var(--brand-soft)]" />
      <div><p className="font-medium">{title}</p><p className="text-muted">{text}</p></div>
    </li>
  )
}

function Warning({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-3 rounded-xl border border-warning/30 bg-warning-soft px-4 py-3 text-sm text-warning">
      <AlertTriangle className="mt-0.5 size-5 shrink-0" />
      <div><p className="font-semibold">{title}</p><p className="[&_code]:rounded [&_code]:bg-black/5 [&_code]:px-1">{children}</p></div>
    </div>
  )
}

/** Copie hors du serveur (bucket S3 / Cloudflare R2). */
function OffsiteCard({ offsite, syncing, onSync }: { offsite: OffsiteStatus | null; syncing: boolean; onSync: () => void }) {
  if (!offsite) {
    return (
      <Card className="p-5 text-sm">
        <h2 className="mb-2 flex items-center gap-2 font-semibold"><CloudOff className="size-4 text-muted" />Copie hors du serveur</h2>
        <p className="text-muted">Non configurée : les sauvegardes ne sont que sur le VPS. Renseignez <code className="rounded bg-surface-2 px-1">S3_BACKUP_BUCKET</code> dans le <code className="rounded bg-surface-2 px-1">.env</code> du serveur.</p>
      </Card>
    )
  }
  const ok = !offsite.lastError
  return (
    <Card className="relative overflow-hidden p-5 text-sm">
      <div aria-hidden className="pointer-events-none absolute -right-10 -top-12 size-32 rounded-full bg-info/20 blur-2xl" />
      <h2 className="relative mb-3 flex items-center gap-2 font-semibold">
        <motion.span animate={syncing ? { y: [0, -3, 0] } : { y: 0 }} transition={{ duration: 0.8, repeat: syncing ? Infinity : 0 }}>
          <Cloud className="size-4 text-info" />
        </motion.span>
        Copie hors du serveur
      </h2>
      <dl className="relative space-y-2">
        <div className="flex justify-between gap-3"><dt className="text-muted">Bucket</dt><dd className="truncate font-mono text-xs">{offsite.bucket}</dd></div>
        <div className="flex justify-between gap-3"><dt className="text-muted">Dernière copie</dt><dd className="font-medium">{offsite.lastSyncAt ? fmtRelative(offsite.lastSyncAt) : 'Pas encore'}</dd></div>
        <div className="flex justify-between gap-3"><dt className="text-muted">Copies distantes</dt><dd className="font-medium">{offsite.remote.length} · {fmtBytes(offsite.remoteBytes)}</dd></div>
        <div className="flex justify-between gap-3"><dt className="text-muted">Fréquence</dt><dd>toutes les {offsite.intervalMinutes} min</dd></div>
        <div className="flex justify-between gap-3"><dt className="text-muted">Conservation</dt><dd>{offsite.retentionDays} jours</dd></div>
      </dl>
      {!ok && <p className="relative mt-3 rounded-lg bg-danger-soft px-3 py-2 text-xs text-danger">{offsite.lastError}</p>}
      <Button className="relative mt-4 w-full" loading={syncing} icon={<CloudUpload className="size-4" />} onClick={onSync}>Copier maintenant</Button>
    </Card>
  )
}
