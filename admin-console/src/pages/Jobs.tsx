import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BriefcaseBusiness, Eye, EyeOff, KeyRound, Play, Plus, Settings2, ShieldAlert, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, cx, Empty, ErrorBox, Input, Loading, Modal, PageHeader, Pagination, Select, Textarea } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { fmtDateTime, fmtRelative } from '@/lib/format'
import type { AdminJobOffer, AdminOfferView, JobImportReport, JobSearch, JobSource, JobSourceConfig, JobsOverview, Paged, SecretState } from '@/lib/types'

type Tab = 'sources' | 'moderation' | 'settings'
type SearchField = 'keywords' | 'region' | 'department' | 'contractType' | 'country' | 'category' | 'location'

const TAKE = 20

/** Champs de recherche utiles et aide, par source. */
const SOURCE_INFO: Record<JobSource, { clientLabel: string; secretLabel: string; help: string; fields: { key: SearchField; label: string; placeholder: string }[] }> = {
  FranceTravail: {
    clientLabel: 'Identifiant client',
    secretLabel: 'Clé secrète',
    help: 'Compte sur francetravail.io, application abonnée à l’API « Offres d’emploi v2 ». Offres de France uniquement.',
    fields: [
      { key: 'keywords', label: 'Mots-clés', placeholder: 'développeur' },
      { key: 'region', label: 'Région (code)', placeholder: '11 = Île-de-France' },
      { key: 'department', label: 'Départements', placeholder: '75,92,93 (prioritaire sur la région)' },
      { key: 'contractType', label: 'Contrats', placeholder: 'CDI,CDD,MIS' },
    ],
  },
  ReliefWeb: {
    clientLabel: 'Appname approuvé',
    secretLabel: '',
    help: 'ONG, ONU et humanitaire. L’appname doit être demandé et approuvé par ReliefWeb avant le premier import.',
    fields: [
      { key: 'country', label: 'Pays (en anglais)', placeholder: 'Benin' },
      { key: 'keywords', label: 'Mots dans le titre', placeholder: 'facultatif' },
    ],
  },
  Adzuna: {
    clientLabel: 'App ID',
    secretLabel: 'App Key',
    help: 'Palier gratuit limité (environ 1 000 appels par mois) : 1 ou 2 recherches, toutes les 6 heures au plus.',
    fields: [
      { key: 'keywords', label: 'Mots-clés', placeholder: 'développeur' },
      { key: 'location', label: 'Lieu', placeholder: 'Paris' },
    ],
  },
  Remotive: {
    clientLabel: '',
    secretLabel: '',
    help: 'Les conditions de Remotive interdisent d’afficher ses offres derrière une inscription : activation impossible tant que BéninConnect exige un compte.',
    fields: [
      { key: 'category', label: 'Catégorie', placeholder: 'software-dev' },
      { key: 'keywords', label: 'Mots-clés', placeholder: 'facultatif' },
    ],
  },
}

const SECRET_LABELS: Record<SecretState, { label: string; tone: 'brand' | 'neutral' | 'warning' | 'danger' | 'info' }> = {
  None: { label: 'Secret manquant', tone: 'warning' },
  Console: { label: 'Secret enregistré', tone: 'brand' },
  Environment: { label: 'Secret fourni par le serveur', tone: 'info' },
  Unreadable: { label: 'Secret illisible : à ressaisir', tone: 'danger' },
}

const VIEWS: { key: AdminOfferView; label: string; hint: string }[] = [
  { key: 'Review', label: 'À revoir', hint: 'Masquées automatiquement (mot-clé d’alerte ou signalements) : décidez si elles reviennent.' },
  { key: 'Reported', label: 'Signalées', hint: 'Offres signalées au moins une fois par un membre.' },
  { key: 'Hidden', label: 'Masquées', hint: 'Jamais réaffichées par un nouvel import.' },
  { key: 'All', label: 'Toutes', hint: 'Toutes les offres importées, les plus récentes d’abord.' },
]

const hours = (minutes: number) => (minutes % 60 === 0 ? `${minutes / 60} h` : `${Math.floor(minutes / 60)} h ${minutes % 60}`)

export function JobsPage() {
  const qc = useQueryClient()
  const [tab, setTab] = useState<Tab>('sources')
  const overview = useQuery({ queryKey: ['jobs'], queryFn: () => api<JobsOverview>('api/admin/jobs') })
  const refresh = () => qc.invalidateQueries({ queryKey: ['jobs'] })
  const o = overview.data
  const count = (label: string) => o?.counts.find((c) => c.label === label)?.count ?? 0

  return (
    <>
      <PageHeader title="Emploi"
        description="Offres importées automatiquement depuis des API publiques. Les membres postulent directement chez la source." />

      {o && (
        <div className="mb-4 flex flex-wrap items-center gap-2 text-sm">
          <Badge tone={o.workerEnabled ? 'brand' : 'neutral'}>{o.workerEnabled ? 'Imports automatiques actifs' : 'Imports automatiques désactivés sur ce serveur'}</Badge>
          <Badge tone="info">{count('Actives')} offre(s) visibles</Badge>
          {count('À revoir') > 0 && <Badge tone="warning">{count('À revoir')} à revoir</Badge>}
          <Badge tone="neutral">{count('Doublons')} doublon(s) · {count('Expirées')} expirée(s)</Badge>
        </div>
      )}

      <div className="mb-4 flex flex-wrap gap-2" role="tablist">
        {([['sources', 'Sources'], ['moderation', `Modération${count('À revoir') ? ` · ${count('À revoir')}` : ''}`], ['settings', 'Réglages']] as const).map(([key, label]) => (
          <button key={key} role="tab" aria-selected={tab === key} onClick={() => setTab(key)}
            className={cx('rounded-full px-3.5 py-1.5 text-sm font-medium', tab === key ? 'bg-brand text-white' : 'border border-line bg-surface text-muted hover:text-fg')}>
            {label}
          </button>
        ))}
      </div>

      {overview.isPending ? <Loading /> : overview.error ? <ErrorBox message={errorMessage(overview.error)} onRetry={() => overview.refetch()} /> : (
        <>
          {tab === 'sources' && <Sources overview={overview.data} onChanged={refresh} />}
          {tab === 'moderation' && <Moderation onChanged={refresh} />}
          {tab === 'settings' && <SettingsCard overview={overview.data} onChanged={refresh} />}
        </>
      )}
    </>
  )
}

function Sources({ overview, onChanged }: { overview: JobsOverview; onChanged: () => void }) {
  const toast = useToast()
  const [editing, setEditing] = useState<JobSourceConfig | null>(null)
  const run = useMutation({
    mutationFn: (source: JobSource) => api<JobImportReport>(`api/admin/jobs/sources/${source}/run`, { method: 'POST' }),
    onSuccess: (r) => {
      const summary = `${r.fetched} lue(s), ${r.added} ajoutée(s), ${r.updated} mise(s) à jour, ${r.duplicates} doublon(s), ${r.hidden} masquée(s), ${r.expired} expirée(s).`
      toast(r.errors.length ? `${summary} Erreurs : ${r.errors.join(' | ')}` : summary, r.errors.length ? 'error' : 'success')
      onChanged()
    },
    onError: (e) => { toast(errorMessage(e), 'error'); onChanged() },
  })

  return (
    <>
      <div className="grid gap-4 lg:grid-cols-2">
        {overview.sources.map((s) => {
          const info = SOURCE_INFO[s.source]
          return (
            <Card key={s.source} className="flex flex-col p-5">
              <div className="flex items-start gap-3">
                <div className="grid size-10 shrink-0 place-items-center rounded-full bg-brand-soft text-brand"><BriefcaseBusiness className="size-4.5" /></div>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-semibold">{s.label}</h2>
                    <Badge tone={s.isEnabled ? 'brand' : 'neutral'}>{s.isEnabled ? `Active · toutes les ${hours(s.importIntervalMinutes)}` : 'Désactivée'}</Badge>
                    {s.needsSecret && <Badge tone={SECRET_LABELS[s.secretState].tone}>{SECRET_LABELS[s.secretState].label}</Badge>}
                  </div>
                  <p className="mt-1 text-sm text-muted">{info.help}</p>
                </div>
              </div>

              <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
                <dt className="text-muted">Offres visibles</dt><dd className="font-medium">{s.activeOffers}</dd>
                <dt className="text-muted">Recherches</dt><dd>{s.searches.length ? s.searches.map((x) => x.key).join(', ') : '—'}</dd>
                <dt className="text-muted">Dernier import réussi</dt>
                <dd title={s.lastSuccessAt ? fmtDateTime(s.lastSuccessAt) : undefined}>{s.lastSuccessAt ? fmtRelative(s.lastSuccessAt) : 'Jamais'}</dd>
                {s.lastSuccessAt && <>
                  <dt className="text-muted">Résultat</dt>
                  <dd>+{s.lastAdded} · {s.lastUpdated} maj · {s.lastDuplicates} doublons · {s.lastHidden} masquées · {s.lastExpired} expirées</dd>
                </>}
              </dl>
              {s.lastError && <p className="mt-3 rounded-lg bg-danger-soft px-3 py-2 text-xs text-danger">Dernière erreur : {s.lastError}</p>}

              <div className="mt-4 flex flex-wrap gap-2 pt-1">
                <Button icon={<Settings2 className="size-4" />} onClick={() => setEditing(s)}>Configurer</Button>
                <Button icon={<Play className="size-4" />} disabled={s.source === 'Remotive'} loading={run.isPending && run.variables === s.source}
                  onClick={() => run.mutate(s.source)}>Importer maintenant</Button>
              </div>
            </Card>
          )
        })}
      </div>
      <p className="mt-4 text-xs text-muted">
        Une source en erreur ne bloque jamais les autres. Premier import : 31 derniers jours, puis 3 derniers jours. Offres absentes de la source : expirées, puis supprimées après 90 jours.
      </p>
      {editing && <SourceModal source={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); onChanged() }} />}
    </>
  )
}

function SourceModal({ source, onClose, onSaved }: { source: JobSourceConfig; onClose: () => void; onSaved: () => void }) {
  const toast = useToast()
  const info = SOURCE_INFO[source.source]
  const [enabled, setEnabled] = useState(source.isEnabled)
  const [interval, setInterval] = useState(source.importIntervalMinutes)
  const [clientId, setClientId] = useState(source.clientId ?? '')
  const [searches, setSearches] = useState<JobSearch[]>(source.searches)
  const [secret, setSecret] = useState('')
  const [secretState, setSecretState] = useState(source.secretState)

  const save = useMutation({
    mutationFn: () => api<JobSourceConfig>(`api/admin/jobs/sources/${source.source}`, {
      method: 'PUT', body: { isEnabled: enabled, importIntervalMinutes: interval, clientId, searches },
    }),
    onSuccess: () => { toast('Source enregistrée.', 'success'); onSaved() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const saveSecret = useMutation({
    mutationFn: (value: string) => api<JobSourceConfig>(`api/admin/jobs/sources/${source.source}/secret`, { method: 'PUT', body: { secret: value } }),
    onSuccess: (s) => { setSecret(''); setSecretState(s.secretState); toast(s.secretState === 'None' ? 'Secret supprimé.' : 'Secret enregistré (chiffré).', 'success') },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  const update = (i: number, key: keyof JobSearch, value: string) =>
    setSearches((old) => old.map((s, j) => (j === i ? { ...s, [key]: value } : s)))

  return (
    <Modal open title={`Configurer ${source.label}`} onClose={onClose} wide
      footer={<>
        <Button variant="ghost" onClick={onClose}>Annuler</Button>
        <Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>Enregistrer</Button>
      </>}>
      <div className="space-y-4">
        <p className="text-sm text-muted">{info.help}</p>
        <label className="flex items-center gap-2 text-sm font-medium">
          <input type="checkbox" className="size-4 accent-[var(--brand)]" checked={enabled} disabled={source.source === 'Remotive'}
            onChange={(e) => setEnabled(e.target.checked)} />
          Imports automatiques activés
        </label>

        <div className="grid gap-3 sm:grid-cols-2">
          <Input label={`Intervalle (minutes, ${source.minIntervalMinutes} au moins)`} type="number" min={source.minIntervalMinutes} step={30}
            value={interval} onChange={(e) => setInterval(Number(e.target.value))} />
          {source.needsClientId && <Input label={info.clientLabel} value={clientId} onChange={(e) => setClientId(e.target.value)} autoComplete="off" />}
        </div>

        {source.needsSecret && (
          <div className="rounded-lg border border-line p-3">
            <div className="mb-2 flex flex-wrap items-center gap-2 text-sm">
              <KeyRound className="size-4 text-muted" />
              <span className="font-medium">{info.secretLabel}</span>
              <Badge tone={SECRET_LABELS[secretState].tone}>{SECRET_LABELS[secretState].label}</Badge>
            </div>
            {secretState === 'Environment'
              ? <p className="text-xs text-muted">Fourni par une variable d’environnement du serveur : prioritaire sur la console.</p>
              : (
                <div className="flex flex-wrap items-end gap-2">
                  <Input className="min-w-56 flex-1" type="password" autoComplete="new-password" placeholder="Jamais réaffiché après enregistrement"
                    value={secret} onChange={(e) => setSecret(e.target.value)} />
                  <Button disabled={!secret.trim()} loading={saveSecret.isPending && !!saveSecret.variables} onClick={() => saveSecret.mutate(secret)}>Enregistrer le secret</Button>
                  {secretState !== 'None' && <Button variant="dangerSoft" onClick={() => saveSecret.mutate('')}>Supprimer</Button>}
                </div>
              )}
          </div>
        )}

        <div>
          <div className="mb-2 flex items-center justify-between">
            <span className="text-sm font-medium">Recherches ({searches.length} / 20)</span>
            <Button variant="ghost" icon={<Plus className="size-4" />} disabled={searches.length >= 20}
              onClick={() => setSearches((old) => [...old, { key: `recherche-${old.length + 1}` }])}>Ajouter</Button>
          </div>
          {searches.length === 0 && <p className="text-sm text-muted">Aucune recherche : rien ne sera importé.</p>}
          <div className="space-y-2">
            {searches.map((s, i) => (
              <div key={i} className="grid gap-2 rounded-lg border border-line p-3 sm:grid-cols-[10rem_1fr_auto]">
                <Input label="Clé" value={s.key} onChange={(e) => update(i, 'key', e.target.value)} placeholder="dev-idf" />
                <div className="grid gap-2 sm:grid-cols-2">
                  {info.fields.map((f) => (
                    <Input key={f.key} label={f.label} placeholder={f.placeholder} value={s[f.key] ?? ''} onChange={(e) => update(i, f.key, e.target.value)} />
                  ))}
                </div>
                <Button variant="ghost" className="self-end" aria-label="Supprimer la recherche" icon={<Trash2 className="size-4" />}
                  onClick={() => setSearches((old) => old.filter((_, j) => j !== i))} />
              </div>
            ))}
          </div>
        </div>
      </div>
    </Modal>
  )
}

function Moderation({ onChanged }: { onChanged: () => void }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [view, setView] = useState<AdminOfferView>('Review')
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [skip, setSkip] = useState(0)

  useEffect(() => {
    const t = setTimeout(() => { setSearch(q); setSkip(0) }, 300)
    return () => clearTimeout(t)
  }, [q])

  const list = useQuery({
    queryKey: ['jobs-offers', view, search, skip],
    queryFn: () => api<Paged<AdminJobOffer>>(`api/admin/jobs/offers${qs({ view, q: search, skip, take: TAKE })}`),
  })
  const decide = useMutation({
    mutationFn: ({ id, verb }: { id: string; verb: 'show' | 'hide' }) => api<AdminJobOffer>(`api/admin/jobs/offers/${id}/${verb}`, { method: 'POST' }),
    onSuccess: (_, { verb }) => {
      toast(verb === 'show' ? 'Offre réaffichée.' : 'Offre masquée définitivement.', 'success')
      qc.invalidateQueries({ queryKey: ['jobs-offers'] })
      onChanged()
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const current = VIEWS.find((v) => v.key === view)!

  return (
    <Card>
      <div className="flex flex-wrap items-end gap-3 border-b border-line px-5 py-3">
        <Select label="Afficher" value={view} onChange={(e) => { setView(e.target.value as AdminOfferView); setSkip(0) }} className="w-44">
          {VIEWS.map((v) => <option key={v.key} value={v.key}>{v.label}</option>)}
        </Select>
        <Input label="Rechercher" placeholder="Titre ou entreprise" value={q} onChange={(e) => setQ(e.target.value)} className="min-w-56 flex-1" />
      </div>
      <p className="border-b border-line px-5 py-3 text-sm text-muted">{current.hint}</p>
      {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data.items.length === 0 ? (
        <Empty title="Rien ici" description={view === 'Review' ? 'Aucune offre en attente de revue.' : undefined} />
      ) : (
        <>
          <ul className="divide-y divide-line">
            {list.data.items.map((o) => (
              <li key={o.id} className="flex flex-col gap-4 p-4 sm:p-5 lg:flex-row lg:items-start">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge tone="neutral">{o.sourceLabel}</Badge>
                    <Badge tone={o.status === 'Active' ? 'brand' : o.status === 'Hidden' ? 'danger' : 'neutral'}>
                      {o.status === 'Active' ? 'Visible' : o.status === 'Hidden' ? 'Masquée' : 'Expirée'}
                    </Badge>
                    {o.alertKeyword && <Badge tone="warning"><ShieldAlert className="mr-1 inline size-3.5" />« {o.alertKeyword} »</Badge>}
                    {o.reportCount > 0 && <Badge tone="warning">{o.reportCount} signalement(s)</Badge>}
                    {o.isDuplicate && <Badge tone="info">Doublon</Badge>}
                    <span className="text-xs text-muted" title={fmtDateTime(o.importedAt)}>importée {fmtRelative(o.importedAt)}</span>
                  </div>
                  <a href={o.applyUrl} target="_blank" rel="noreferrer" className="mt-2 block font-medium hover:underline">{o.title}</a>
                  <p className="text-sm text-muted">{[o.companyName, o.locationLabel].filter(Boolean).join(' · ')}</p>
                  {o.excerpt && <p className="mt-2 line-clamp-3 text-sm">{o.excerpt}</p>}
                  {o.reports.length > 0 && (
                    <ul className="mt-2 space-y-1 text-xs text-muted">
                      {o.reports.map((r, i) => <li key={i}>{fmtDateTime(r.createdAt)} — {r.reason}{r.handled ? ' (traité)' : ''}</li>)}
                    </ul>
                  )}
                </div>
                <div className="flex flex-wrap gap-2 lg:w-56 lg:flex-col">
                  {o.status === 'Hidden' && <Button variant="primary" icon={<Eye className="size-4" />} loading={decide.isPending && decide.variables?.id === o.id}
                    onClick={() => decide.mutate({ id: o.id, verb: 'show' })}>Réafficher</Button>}
                  {(o.status !== 'Hidden' || o.needsReview) && <Button variant="dangerSoft" icon={<EyeOff className="size-4" />}
                    onClick={() => decide.mutate({ id: o.id, verb: 'hide' })}>Masquer définitivement</Button>}
                </div>
              </li>
            ))}
          </ul>
          <Pagination skip={skip} take={TAKE} total={list.data.totalCount} onChange={setSkip} />
        </>
      )}
    </Card>
  )
}

function SettingsCard({ overview, onChanged }: { overview: JobsOverview; onChanged: () => void }) {
  const toast = useToast()
  const [keywords, setKeywords] = useState(overview.alertKeywords.join('\n'))
  const [threshold, setThreshold] = useState(overview.reportThreshold)
  const save = useMutation({
    mutationFn: () => api<JobsOverview>('api/admin/jobs/settings', {
      method: 'PUT', body: { alertKeywords: keywords.split('\n').map((k) => k.trim()).filter(Boolean), reportThreshold: threshold },
    }),
    onSuccess: () => { toast('Réglages enregistrés.', 'success'); onChanged() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })

  return (
    <Card className="max-w-2xl space-y-4 p-5">
      <Textarea label="Mots-clés d’alerte (un par ligne)" rows={8} value={keywords} onChange={(e) => setKeywords(e.target.value)} />
      <p className="-mt-2 text-xs text-muted">
        Une offre qui en contient un (titre, entreprise ou description, sans tenir compte des accents) est importée masquée et placée dans « À revoir ».
        S’applique aux nouveaux imports.
      </p>
      <Input label="Signalements avant masquage automatique" type="number" min={1} max={50} value={threshold}
        onChange={(e) => setThreshold(Number(e.target.value))} className="max-w-xs" />
      <Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>Enregistrer</Button>
    </Card>
  )
}
