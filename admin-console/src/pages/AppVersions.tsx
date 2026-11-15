import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, BellRing, CheckCircle2, CircleHelp, Mail, Radar, Save, ShieldAlert, Smartphone } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Stagger } from '@/components/motion'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ConfirmDialog, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Pagination, Select, Stat, Table } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDateTime, fmtNumber, fmtRelative } from '@/lib/format'
import type {
  AppInstallation, AppPlatform, AppRelease, AppVersionsOverview, CampaignRecipient, CampaignStatus, Paged, UpdateCampaign, UpdateStatus,
} from '@/lib/types'

const KEY = ['app-versions']

const PLATFORM_LABELS: Record<AppPlatform, string> = { ios: 'iOS', android: 'Android' }

const STATUS: Record<UpdateStatus, { label: string; tone: 'brand' | 'warning' | 'danger' | 'neutral' }> = {
  UpToDate: { label: 'À jour', tone: 'brand' },
  UpdateAvailable: { label: 'Ancienne version', tone: 'warning' },
  UpdateRequired: { label: 'Mise à jour obligatoire', tone: 'danger' },
  Unknown: { label: 'Inconnu', tone: 'neutral' },
}

const CAMPAIGN: Record<CampaignStatus, { label: string; tone: 'brand' | 'warning' | 'danger' | 'neutral' | 'info' }> = {
  PendingApproval: { label: 'À valider', tone: 'warning' },
  Approved: { label: 'Envoi en cours', tone: 'info' },
  Sent: { label: 'Envoyée', tone: 'brand' },
  Rejected: { label: 'Refusée', tone: 'neutral' },
  Superseded: { label: 'Remplacée', tone: 'neutral' },
}

const RECIPIENT: Record<CampaignRecipient['status'], string> = { Pending: 'En attente', Sent: 'Relancé', Skipped: 'Déjà à jour', Failed: 'Échec' }

const StatusBadge = ({ status }: { status: UpdateStatus }) => <Badge tone={STATUS[status].tone}>{STATUS[status].label}</Badge>

/** Version de référence d'une plateforme : dernière publiée, minimum accepté, lien du store. */
function ReleaseCard({ release }: { release: AppRelease }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [form, setForm] = useState({ latest: '', minimum: '', storeUrl: '' })
  useEffect(() => {
    setForm({ latest: release.latestVersion ?? '', minimum: release.minimumSupportedVersion ?? '', storeUrl: release.storeUrl })
  }, [release])
  const save = useMutation({
    mutationFn: () => api<AppRelease>(`api/admin/app-versions/releases/${release.platform}`, {
      method: 'PUT', body: { latestVersion: form.latest || null, minimumSupportedVersion: form.minimum || null, storeUrl: form.storeUrl },
    }),
    onSuccess: () => {
      toast(`Versions ${PLATFORM_LABELS[release.platform]} enregistrées.`)
      qc.invalidateQueries({ queryKey: KEY })
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Card className="space-y-3 p-5">
      <div className="flex items-center justify-between">
        <h3 className="flex items-center gap-2 font-semibold"><Smartphone className="size-4" /> {PLATFORM_LABELS[release.platform]}</h3>
        {release.updatedAt && <span className="text-xs text-muted">Modifié {fmtRelative(release.updatedAt)}{release.updatedBy ? ` par ${release.updatedBy}` : ''}</span>}
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <Input label="Dernière version publiée" placeholder="1.3.0" value={form.latest} onChange={(e) => setForm({ ...form, latest: e.target.value })} />
        <Input label="Version minimale acceptée" placeholder="Aucune" value={form.minimum} onChange={(e) => setForm({ ...form, minimum: e.target.value })} />
      </div>
      <Input label="Lien du store" value={form.storeUrl} onChange={(e) => setForm({ ...form, storeUrl: e.target.value })} />
      <p className="text-xs text-muted">Sous la version minimale, l'application affiche un écran de mise à jour obligatoire. Renseignez la dernière version une fois la publication validée par le store.</p>
      <div className="flex justify-end">
        <Button variant="primary" icon={<Save className="size-4" />} loading={save.isPending} onClick={() => save.mutate()}>Enregistrer</Button>
      </div>
    </Card>
  )
}

function CampaignDetailModal({ id, onClose }: { id: string | null; onClose: () => void }) {
  const q = useQuery({
    queryKey: [...KEY, 'campaign', id],
    queryFn: () => api<{ campaign: UpdateCampaign; recipients: CampaignRecipient[] }>(`api/admin/app-versions/campaigns/${id}`),
    enabled: !!id,
  })
  return (
    <Modal open={!!id} title="Membres à relancer" onClose={onClose} wide>
      {q.isLoading ? <Loading /> : q.error ? <ErrorBox message={errorMessage(q.error)} /> : q.data && (
        <div className="space-y-3">
          <p className="text-sm text-muted">
            {fmtNumber(q.data.campaign.recipientCount)} membre(s), dont {fmtNumber(q.data.campaign.requiredCount)} avec une mise à jour obligatoire.
            {q.data.campaign.recipientCount > q.data.recipients.length && ` Les ${q.data.recipients.length} premiers sont affichés.`}
          </p>
          <Table head={['Membre', 'Plateforme', 'Version', 'Cible', 'Statut']}>
            {q.data.recipients.map((r) => (
              <tr key={r.userId}>
                <td className="px-4 py-2">{r.name}</td>
                <td className="px-4 py-2">{PLATFORM_LABELS[r.platform]}</td>
                <td className="px-4 py-2">{r.currentVersion} {r.required && <Badge tone="danger">Obligatoire</Badge>}</td>
                <td className="px-4 py-2">{r.targetVersion}</td>
                <td className="px-4 py-2 text-muted">{RECIPIENT[r.status]}{r.notifiedAt ? ` · ${fmtRelative(r.notifiedAt)}` : ''}</td>
              </tr>
            ))}
          </Table>
        </div>
      )}
    </Modal>
  )
}

/** Relance préparée par la détection : rien n'est envoyé sans validation. */
function PendingCampaign({ campaign, onShow }: { campaign: UpdateCampaign; onShow: () => void }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [push, setPush] = useState(true)
  const [email, setEmail] = useState(true)
  const [confirm, setConfirm] = useState<'approve' | 'reject' | null>(null)
  const done = () => {
    setConfirm(null)
    qc.invalidateQueries({ queryKey: KEY })
  }
  const approve = useMutation({
    mutationFn: () => api(`api/admin/app-versions/campaigns/${campaign.id}/approve`, { method: 'POST', body: { sendPush: push, sendEmail: email } }),
    onSuccess: () => { toast('Relance validée : l’envoi démarre dans les prochaines minutes.'); done() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const reject = useMutation({
    mutationFn: (reason: string) => api(`api/admin/app-versions/campaigns/${campaign.id}/reject`, { method: 'POST', body: { reason } }),
    onSuccess: () => { toast('Relance refusée.'); done() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const channels = [push && 'une notification push', email && 'un email'].filter(Boolean).join(' et ')
  return (
    <Card className="space-y-4 border-warning/40 p-5">
      <div className="flex flex-wrap items-start gap-3">
        <BellRing className="mt-0.5 size-5 text-warning" />
        <div className="min-w-0 flex-1">
          <p className="font-semibold">Relance de mise à jour à valider</p>
          <p className="text-sm text-muted">
            {fmtNumber(campaign.recipientCount)} membre(s) n'ont pas la dernière version, dont {fmtNumber(campaign.requiredCount)} sous la version minimale.
            Préparée {fmtRelative(campaign.createdAt)}.
          </p>
        </div>
        <Button onClick={onShow}>Voir les membres</Button>
      </div>
      <div className="flex flex-wrap items-center gap-5 text-sm">
        <label className="flex items-center gap-2"><input type="checkbox" checked={push} onChange={(e) => setPush(e.target.checked)} /> <BellRing className="size-4" /> Notification push</label>
        <label className="flex items-center gap-2"><input type="checkbox" checked={email} onChange={(e) => setEmail(e.target.checked)} /> <Mail className="size-4" /> Email</label>
        <div className="ml-auto flex gap-2">
          <Button variant="ghost" onClick={() => setConfirm('reject')}>Refuser</Button>
          <Button variant="primary" disabled={!push && !email} onClick={() => setConfirm('approve')}>Valider l'envoi</Button>
        </div>
      </div>
      <ConfirmDialog open={confirm === 'approve'} busy={approve.isPending} title="Envoyer la relance ?" confirmLabel="Envoyer"
        message={`${fmtNumber(campaign.recipientCount)} membre(s) recevront ${channels} les invitant à mettre à jour l'application. Les membres passés à jour d'ici l'envoi sont ignorés. Décision enregistrée dans le journal d'audit.`}
        onClose={() => setConfirm(null)} onConfirm={() => approve.mutate()} />
      <ConfirmDialog open={confirm === 'reject'} busy={reject.isPending} title="Refuser la relance ?" confirmLabel="Refuser" danger
        message="Aucun message ne sera envoyé. La prochaine détection préparera une nouvelle relance."
        reason={{ label: 'Motif (facultatif)' }} onClose={() => setConfirm(null)} onConfirm={(r) => reject.mutate(r)} />
    </Card>
  )
}

function Installations({ activeDays }: { activeDays: number }) {
  const [status, setStatus] = useState<UpdateStatus | ''>('')
  const [platform, setPlatform] = useState<AppPlatform | ''>('')
  const [skip, setSkip] = useState(0)
  const take = 25
  const q = useQuery({
    queryKey: [...KEY, 'installations', status, platform, skip],
    queryFn: () => api<Paged<AppInstallation>>(`api/admin/app-versions/installations${qs({ status: status || null, platform: platform || null, skip, take })}`),
  })
  return (
    <Card className="overflow-hidden">
      <div className="flex flex-wrap items-end gap-3 border-b border-line p-4">
        <h3 className="mr-auto font-semibold">Téléphones <span className="text-sm font-normal text-muted">(vus depuis {activeDays} jours)</span></h3>
        <Select value={status} onChange={(e) => { setStatus(e.target.value as UpdateStatus | ''); setSkip(0) }} className="w-56">
          <option value="">Tous les statuts</option>
          {(Object.keys(STATUS) as UpdateStatus[]).map((s) => <option key={s} value={s}>{STATUS[s].label}</option>)}
        </Select>
        <Select value={platform} onChange={(e) => { setPlatform(e.target.value as AppPlatform | ''); setSkip(0) }} className="w-40">
          <option value="">iOS et Android</option>
          <option value="ios">iOS</option>
          <option value="android">Android</option>
        </Select>
      </div>
      {q.isLoading ? <Loading /> : q.error ? <ErrorBox message={errorMessage(q.error)} /> : q.data!.items.length === 0 ? <Empty title="Aucun téléphone" /> : (
        <>
          <Table head={['Membre', 'Plateforme', 'Version', 'Statut', 'Dernière activité']}>
            {q.data!.items.map((i) => (
              <tr key={i.id}>
                <td className="px-4 py-2">{i.memberName ?? <span className="text-muted">Non connecté</span>}</td>
                <td className="px-4 py-2">{PLATFORM_LABELS[i.platform]}{i.osVersion && <span className="text-muted"> {i.osVersion}</span>}</td>
                <td className="px-4 py-2">{i.version} <span className="text-xs text-muted">({i.buildNumber})</span></td>
                <td className="px-4 py-2"><StatusBadge status={i.status} /></td>
                <td className="px-4 py-2 text-muted">{fmtRelative(i.lastSeenAt)}</td>
              </tr>
            ))}
          </Table>
          <Pagination skip={skip} take={take} total={q.data!.totalCount} onChange={setSkip} />
        </>
      )}
    </Card>
  )
}

/** Versions installées par les membres, mises à jour conseillées ou obligatoires, relances validées par l'équipe. */
export function AppVersionsPage() {
  const toast = useToast()
  const qc = useQueryClient()
  const [detail, setDetail] = useState<string | null>(null)
  const q = useQuery({
    queryKey: KEY,
    queryFn: () => api<AppVersionsOverview>('api/admin/app-versions'),
    refetchInterval: (query) => (query.state.data?.sending ? 5000 : 60_000),
  })
  const campaigns = useQuery({ queryKey: [...KEY, 'campaigns'], queryFn: () => api<Paged<UpdateCampaign>>('api/admin/app-versions/campaigns?take=10') })
  const detect = useMutation({
    mutationFn: () => api<{ campaign: UpdateCampaign | null }>('api/admin/app-versions/detect', { method: 'POST' }),
    onSuccess: ({ campaign }) => {
      toast(campaign ? `${campaign.recipientCount} membre(s) à relancer : relance prête à valider.` : 'Aucun membre à relancer.')
      qc.invalidateQueries({ queryKey: KEY })
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  if (q.isLoading) return <Loading />
  if (q.error) return <ErrorBox message={errorMessage(q.error)} onRetry={() => q.refetch()} />
  const d = q.data!
  const m = d.members

  return (
    <div className="space-y-5">
      <PageHeader title="Versions de l'application"
        description={`Version installée sur chaque téléphone (vu depuis ${d.activeDays} jours), membres à jour ou non, et relances de mise à jour validées par l'équipe.`}
        actions={<Button icon={<Radar className="size-4" />} loading={detect.isPending} onClick={() => detect.mutate()}>Détecter maintenant</Button>} />

      {d.pending && <PendingCampaign campaign={d.pending} onShow={() => setDetail(d.pending!.id)} />}
      {d.sending && (
        <Card className="flex items-center gap-3 px-5 py-4">
          <BellRing className="size-5 text-info" />
          <p className="text-sm">Envoi en cours : {fmtNumber(d.sending.sentCount)} relancé(s), {fmtNumber(d.sending.skippedCount)} déjà à jour sur {fmtNumber(d.sending.recipientCount)}.</p>
        </Card>
      )}

      <Stagger className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Membres à jour" count={m.upToDate} icon={<CheckCircle2 />} accent="brand" hint={`${fmtNumber(d.installations.total)} téléphone(s) actif(s)`} />
        <Stat label="Ancienne version" count={m.updateAvailable} icon={<AlertTriangle />} accent="warning" hint="Mise à jour conseillée" />
        <Stat label="Mise à jour obligatoire" count={m.updateRequired} icon={<ShieldAlert />} accent="danger" tone={m.updateRequired ? 'danger' : undefined} hint="Bloqués à l'ouverture de l'app" />
        <Stat label="Statut inconnu" count={m.unknown} icon={<CircleHelp />} accent="info" hint="Aucune version de référence" />
      </Stagger>

      <div className="grid gap-4 lg:grid-cols-2">
        {d.releases.map((r) => <ReleaseCard key={r.platform} release={r} />)}
      </div>

      <Card className="overflow-hidden">
        <h3 className="border-b border-line p-4 font-semibold">Répartition des versions</h3>
        {d.versions.length === 0 ? <Empty title="Aucune installation" description="Les téléphones apparaissent au premier lancement de l'application à jour." /> : (
          <Table head={['Plateforme', 'Version', 'Statut', 'Téléphones', 'Membres', 'Dernière activité']}>
            {d.versions.map((v) => (
              <tr key={`${v.platform}-${v.version}`}>
                <td className="px-4 py-2">{PLATFORM_LABELS[v.platform]}</td>
                <td className="px-4 py-2 font-medium">{v.version}</td>
                <td className="px-4 py-2"><StatusBadge status={v.status} /></td>
                <td className="px-4 py-2">{fmtNumber(v.installations)}</td>
                <td className="px-4 py-2">{fmtNumber(v.members)}</td>
                <td className="px-4 py-2 text-muted">{fmtRelative(v.lastSeenAt)}</td>
              </tr>
            ))}
          </Table>
        )}
      </Card>

      <Installations activeDays={d.activeDays} />

      <Card className="overflow-hidden">
        <h3 className="border-b border-line p-4 font-semibold">Relances</h3>
        {!campaigns.data || campaigns.data.items.length === 0 ? <Empty title="Aucune relance" description="La détection quotidienne prépare une relance quand des membres ne sont pas à jour." /> : (
          <Table head={['Préparée', 'Statut', 'Membres', 'Canaux', 'Résultat', 'Décision']}>
            {campaigns.data.items.map((c) => (
              <tr key={c.id} className="cursor-pointer hover:bg-surface-2" onClick={() => setDetail(c.id)}>
                <td className="px-4 py-2">{fmtDateTime(c.createdAt)}</td>
                <td className="px-4 py-2"><Badge tone={CAMPAIGN[c.status].tone}>{CAMPAIGN[c.status].label}</Badge></td>
                <td className="px-4 py-2">{fmtNumber(c.recipientCount)} <span className="text-xs text-muted">({c.requiredCount} oblig.)</span></td>
                <td className="px-4 py-2">{[c.sendPush && 'Push', c.sendEmail && 'Email'].filter(Boolean).join(' + ') || '—'}</td>
                <td className="px-4 py-2 text-muted">{c.status === 'Sent' || c.status === 'Approved' ? `${c.sentCount} relancé(s), ${c.skippedCount} à jour, ${c.failedCount} échec(s)` : '—'}</td>
                <td className="px-4 py-2 text-muted">{c.decidedBy ? `${c.decidedBy}, ${fmtRelative(c.decidedAt!)}` : '—'}{c.rejectReason ? ` : ${c.rejectReason}` : ''}</td>
              </tr>
            ))}
          </Table>
        )}
      </Card>

      <CampaignDetailModal id={detail} onClose={() => setDetail(null)} />
    </div>
  )
}
