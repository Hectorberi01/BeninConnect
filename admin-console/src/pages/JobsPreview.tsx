import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { ArrowLeft, Bookmark, Briefcase, Building2, CalendarX2, Clock, Coins, ExternalLink, Flag, Info, MapPin, Search } from 'lucide-react'
import { type ReactNode, useEffect, useMemo, useState } from 'react'
import { Badge, Button, Card, cx, ErrorBox, Loading, PageHeader } from '@/components/ui'
import { api, errorMessage, qs } from '@/lib/api'
import { CONTRACT_LABELS, contractText, countryName, fmtDate, fmtRelative } from '@/lib/format'
import type { JobContract, JobFilters, MemberJob, MemberJobSummary } from '@/lib/types'

type Page = { items: MemberJobSummary[]; nextCursor: string | null }

const SOURCE_LABELS: Record<string, string> = { FranceTravail: 'France Travail', ReliefWeb: 'ReliefWeb', Adzuna: 'Adzuna', Remotive: 'Remotive' }

/** Ce que voit un membre dans l'application, avec les mêmes règles que /api/jobs (favoris exclus). */
export function JobsPreviewPage() {
  const [search, setSearch] = useState('')
  const [q, setQ] = useState('')
  const [country, setCountry] = useState('')
  const [contract, setContract] = useState<JobContract | ''>('')
  const [remote, setRemote] = useState(false)
  const [openId, setOpenId] = useState<string | null>(null)

  useEffect(() => {
    const t = setTimeout(() => setQ(search.trim()), 300)
    return () => clearTimeout(t)
  }, [search])

  const filters = useQuery({ queryKey: ['jobs-preview-filters'], queryFn: () => api<JobFilters>('api/admin/jobs/preview/filters') })
  const list = useInfiniteQuery({
    queryKey: ['jobs-preview', q, country, contract, remote],
    queryFn: ({ pageParam }) => api<Page>(`api/admin/jobs/preview${qs({
      q, country: country || undefined, contract: contract || undefined, remote: remote || undefined, cursor: pageParam ?? undefined, take: 20,
    })}`),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor,
  })
  const jobs = useMemo(() => (list.data?.pages ?? []).flatMap((p) => p.items), [list.data])
  const f = filters.data

  return (
    <>
      <PageHeader title="Aperçu membres"
        description="La rubrique Emploi telle qu'elle s'affiche dans l'application : mêmes offres, même ordre, mêmes filtres." />
      <div className="flex flex-col gap-6 xl:flex-row xl:items-start">
        <Phone>
          {openId ? <Detail id={openId} onBack={() => setOpenId(null)} /> : (
            <>
              <div className="px-4 pb-2 pt-3">
                <p className="text-xl font-bold">Emploi</p>
                <p className="text-xs text-muted">Offres de France Travail, ReliefWeb…</p>
              </div>
              <div className="space-y-2 px-4 pb-2">
                <label className="flex h-10 items-center gap-2 rounded-xl border border-line bg-surface px-3">
                  <Search className="size-4 text-muted" />
                  <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Métier, entreprise…"
                    className="w-full bg-transparent text-sm outline-none placeholder:text-muted" />
                </label>
                <div className="grid grid-cols-2 gap-2">
                  <select value={country} onChange={(e) => setCountry(e.target.value)} aria-label="Pays"
                    className="h-9 rounded-xl border border-line bg-surface px-2 text-sm">
                    <option value="">Tous les pays</option>
                    {f?.countries.map((c) => <option key={c.label} value={c.label}>{countryName(c.label)} ({c.count})</option>)}
                  </select>
                  <select value={contract} onChange={(e) => setContract(e.target.value as JobContract | '')} aria-label="Contrat"
                    className="h-9 rounded-xl border border-line bg-surface px-2 text-sm">
                    <option value="">Tous les contrats</option>
                    {f?.contracts.map((c) => <option key={c.label} value={c.label}>{CONTRACT_LABELS[c.label] ?? c.label} ({c.count})</option>)}
                  </select>
                </div>
                <button onClick={() => setRemote((r) => !r)}
                  className={cx('rounded-full px-3 py-1 text-xs font-medium', remote ? 'bg-brand text-white' : 'border border-line bg-surface text-fg')}>
                  Télétravail
                </button>
              </div>

              <div className="flex-1 space-y-3 overflow-y-auto px-4 pb-4">
                {list.isPending ? <Loading label="Chargement des offres…" />
                  : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} />
                    : jobs.length === 0 ? (
                      <div className="py-10 text-center">
                        <p className="font-medium">Aucune offre trouvée</p>
                        <p className="text-sm text-muted">Modifiez votre recherche ou vos filtres.</p>
                      </div>
                    ) : (
                      <>
                        {jobs.map((j) => <JobCard key={j.id} job={j} onOpen={() => setOpenId(j.id)} />)}
                        {list.hasNextPage
                          ? <Button className="w-full" loading={list.isFetchingNextPage} onClick={() => list.fetchNextPage()}>Charger la suite</Button>
                          : <p className="py-2 text-center text-xs text-muted">La candidature se fait sur le site de la source de chaque offre.</p>}
                      </>
                    )}
              </div>
            </>
          )}
        </Phone>

        <Card className="space-y-3 p-5 text-sm xl:max-w-md">
          <h2 className="flex items-center gap-2 font-semibold"><Info className="size-4 text-brand" />Ce que voient les membres</h2>
          <ul className="list-disc space-y-1 pl-5 text-muted">
            <li>Seulement les offres <b>actives</b> : ni expirées, ni masquées, ni doublons d'une source prioritaire.</li>
            <li>Les plus récentes d'abord (date de publication chez la source), 20 par page.</li>
            <li>La recherche porte sur le titre et l'entreprise.</li>
            <li>« Postuler » ouvre le site de la source : BéninConnect ne reçoit aucune candidature.</li>
          </ul>
          {f && (
            <div className="flex flex-wrap gap-2 pt-1">
              <Badge tone="brand">{f.total} offre(s) visibles</Badge>
              <Badge tone="neutral">{f.remote} en télétravail</Badge>
              {f.sources.map((s) => <Badge key={s.label} tone="info">{SOURCE_LABELS[s.label] ?? s.label} : {s.count}</Badge>)}
            </div>
          )}
          <p className="text-xs text-muted">Les favoris et le signalement sont désactivés dans l'aperçu.</p>
        </Card>
      </div>
    </>
  )
}

function Phone({ children }: { children: ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-[390px] shrink-0 rounded-[2.5rem] border-[10px] border-neutral-900 bg-neutral-900 shadow-xl">
      <div className="flex h-[760px] flex-col overflow-hidden rounded-[1.9rem] bg-bg text-fg">
        <div className="flex h-7 shrink-0 items-center justify-center"><div className="h-1.5 w-20 rounded-full bg-neutral-300" /></div>
        {children}
      </div>
    </div>
  )
}

function Logo({ job, size = 44 }: { job: Pick<MemberJobSummary, 'companyLogoUrl'>; size?: number }) {
  return (
    <div className="grid shrink-0 place-items-center overflow-hidden bg-brand-soft text-brand" style={{ width: size, height: size, borderRadius: size / 4 }}>
      {job.companyLogoUrl ? <img src={job.companyLogoUrl} alt="" className="size-full object-contain" /> : <Briefcase className="size-1/2" />}
    </div>
  )
}

function Tag({ children, tone = 'brand' }: { children: ReactNode; tone?: 'brand' | 'neutral' | 'warning' }) {
  const styles = { brand: 'bg-brand-soft text-brand', neutral: 'bg-surface-2 text-muted', warning: 'bg-warning-soft text-warning' }
  return <span className={cx('rounded-md px-2 py-0.5 text-xs font-medium', styles[tone])}>{children}</span>
}

function JobCard({ job, onOpen }: { job: MemberJobSummary; onOpen: () => void }) {
  return (
    <button onClick={onOpen} className="block w-full rounded-2xl border border-line bg-surface p-3.5 text-left shadow-sm transition hover:border-brand/40">
      <div className="flex items-start gap-3">
        <Logo job={job} />
        <div className="min-w-0 flex-1">
          <p className="line-clamp-2 font-semibold">{job.title}</p>
          {job.companyName && <p className="truncate text-sm">{job.companyName}</p>}
          <p className="truncate text-sm text-muted">{job.locationLabel}</p>
        </div>
        <Bookmark className="size-5 shrink-0 text-muted" />
      </div>
      <div className="mt-2.5 flex flex-wrap gap-1.5">
        <Tag>{contractText(job)}</Tag>
        {job.isRemote && <Tag tone="neutral">Télétravail</Tag>}
      </div>
      <div className="mt-2.5 flex items-center gap-2 border-t border-line pt-2.5 text-xs text-muted">
        <span className="flex-1 truncate">{job.sourceLabel}</span>
        <span>{fmtRelative(job.publishedAt)}</span>
      </div>
    </button>
  )
}

function Detail({ id, onBack }: { id: string; onBack: () => void }) {
  const query = useQuery({ queryKey: ['jobs-preview-offer', id], queryFn: () => api<MemberJob>(`api/admin/jobs/preview/${id}`) })
  const job = query.data
  const Line = ({ icon, text }: { icon: ReactNode; text: string | null | undefined }) => !text ? null : (
    <div className="flex items-center gap-3 py-1">
      <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-brand-soft text-brand">{icon}</span>
      <span className="text-sm">{text}</span>
    </div>
  )

  return (
    <>
      <div className="flex items-center gap-2 border-b border-line px-2 py-2">
        <button onClick={onBack} aria-label="Retour" className="grid size-9 place-items-center rounded-full hover:bg-surface-2"><ArrowLeft className="size-5" /></button>
        <p className="flex-1 font-semibold">Offre d'emploi</p>
        <Bookmark className="size-5 text-muted" />
        <Flag className="mr-2 size-5 text-muted" />
      </div>
      {query.isPending ? <Loading /> : query.error ? <ErrorBox message={errorMessage(query.error)} /> : job && (
        <>
          <div className="flex-1 overflow-y-auto p-4">
            <div className="flex items-center gap-3">
              <Logo job={job} size={56} />
              <div>
                {job.companyName && <p className="font-semibold">{job.companyName}</p>}
                <p className="text-sm text-muted">Publiée {fmtRelative(job.publishedAt)}</p>
              </div>
            </div>
            <h2 className="mt-3 text-xl font-bold leading-tight">{job.title}</h2>
            <div className="mt-2 flex flex-wrap gap-1.5">
              <Tag>{contractText(job)}</Tag>
              {job.isRemote && <Tag tone="neutral">Télétravail</Tag>}
              {job.status === 'Expired' && <Tag tone="warning">Offre expirée</Tag>}
            </div>
            <div className="mt-3">
              <Line icon={<MapPin className="size-4" />} text={job.locationLabel || countryName(job.country)} />
              <Line icon={<Clock className="size-4" />} text={job.workingTime} />
              <Line icon={<Coins className="size-4" />} text={job.salaryLabel} />
              <Line icon={<Building2 className="size-4" />} text={job.sectorLabel} />
              <Line icon={<CalendarX2 className="size-4" />} text={job.closingAt ? `Candidatures jusqu’au ${fmtDate(job.closingAt)}` : null} />
            </div>
            <p className="mt-4 font-semibold">Description</p>
            <p className="mt-1 whitespace-pre-wrap text-sm">{job.description || 'Consultez le détail de l’offre sur le site de la source.'}</p>
            <p className="mt-4 rounded-xl bg-surface-2 p-3 text-xs text-muted">
              Offre publiée par {job.sourceLabel}. BéninConnect l’affiche telle quelle : la candidature se fait sur le site de la source.
              Ne payez jamais pour obtenir un emploi.
            </p>
          </div>
          <div className="border-t border-line p-3">
            <a href={job.applyUrl} target="_blank" rel="noreferrer"
              className="flex h-11 items-center justify-center gap-2 rounded-xl bg-brand text-sm font-semibold text-white">
              Postuler sur {job.sourceLabel.split(' · ')[0]} <ExternalLink className="size-4" />
            </a>
          </div>
        </>
      )}
    </>
  )
}
