import { useQuery } from '@tanstack/react-query'
import { BadgeCheck, ClipboardCheck, FileText, Flag, Handshake, MessageSquareWarning, Star, Users } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Card, ErrorBox, Loading, PageHeader, Stat } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { countryName, fmtNumber } from '@/lib/format'
import type { ServicesOverview, ServicesStats } from '@/lib/types'

const weekLabel = (iso: string) => new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short' }).format(new Date(`${iso}T00:00:00`))

export function ServicesDashboardPage() {
  const overview = useQuery({ queryKey: ['services'], queryFn: () => api<ServicesOverview>('api/admin/services') })
  const stats = useQuery({ queryKey: ['services-stats'], queryFn: () => api<ServicesStats>('api/admin/services/stats') })
  const o = overview.data
  const s = stats.data

  const queue = o ? [
    { label: 'Profils à valider', count: o.pendingProfiles, tab: 'profiles', icon: <ClipboardCheck />, accent: 'warning' as const },
    { label: 'Offres masquées à revoir', count: o.offeringsToReview, tab: 'offerings', icon: <MessageSquareWarning />, accent: 'danger' as const },
    { label: 'Signalements ouverts', count: o.openReports, tab: 'reports', icon: <Flag />, accent: 'danger' as const },
    { label: 'Justificatifs à vérifier', count: o.pendingDocuments, tab: 'documents', icon: <FileText />, accent: 'info' as const },
  ] : []
  const maxCategory = Math.max(1, ...(s?.byCategory.map((c) => c.providers) ?? [1]))

  return (
    <>
      <PageHeader title="Services entre membres"
        description="Mise en relation uniquement : aucun paiement ne passe par BéninConnect. Suivi de l'activité et des files de modération." />
      {overview.isPending ? <Loading /> : overview.error ? <ErrorBox message={errorMessage(overview.error)} onRetry={() => overview.refetch()} /> : o && (
        <>
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">À traiter</h2>
          <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {queue.map((q) => (
              <Link key={q.tab} to={`/services/moderation?tab=${q.tab}`} className="block">
                <Stat label={q.label} count={q.count} icon={q.icon} accent={q.count ? q.accent : 'brand'} hint={q.count ? 'Ouvrir la file' : 'Rien en attente'} />
              </Link>
            ))}
          </div>

          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">Activité</h2>
          <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <Stat label="Prestataires en ligne" count={o.activeProviders} icon={<Users />} hint={`${o.totalProviders} profil(s) au total, ${o.hiddenProfiles} masqué(s)`} />
            <Stat label="Demandes (7 jours)" count={o.requestsLast7Days} icon={<Handshake />} accent="info" />
            <Stat label="Prestations terminées (30 jours)" count={o.completedLast30Days} icon={<BadgeCheck />} accent="violet" />
            <Stat label="Avis vérifiés" value={o.reviews ? `${o.averageRating.toFixed(1)} / 5` : '—'} icon={<Star />} accent="warning"
              hint={`${fmtNumber(o.reviews)} avis publiés${o.reviewsToReview ? `, ${o.reviewsToReview} à revoir` : ''}`} />
          </div>
        </>
      )}

      {stats.isPending ? <Loading /> : stats.error ? <ErrorBox message={errorMessage(stats.error)} /> : s && (
        <div className="grid gap-4 xl:grid-cols-3">
          <Card className="p-5 xl:col-span-2">
            <h3 className="font-semibold">Demandes par semaine</h3>
            <p className="mb-3 text-sm text-muted">12 dernières semaines, selon la date d'envoi de la demande.</p>
            <div className="h-72">
              <ResponsiveContainer>
                <BarChart data={s.requestsByWeek.map((w) => ({ ...w, label: weekLabel(w.weekStart) }))}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--line)" vertical={false} />
                  <XAxis dataKey="label" tick={{ fontSize: 12, fill: 'var(--muted)' }} tickLine={false} axisLine={false} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 12, fill: 'var(--muted)' }} tickLine={false} axisLine={false} width={32} />
                  <Tooltip contentStyle={{ background: 'var(--surface)', border: '1px solid var(--line)', borderRadius: 8 }} />
                  <Legend wrapperStyle={{ fontSize: 12 }} />
                  <Bar dataKey="requests" name="Envoyées" fill="var(--info)" radius={[4, 4, 0, 0]} />
                  <Bar dataKey="accepted" name="Acceptées" fill="var(--brand)" radius={[4, 4, 0, 0]} />
                  <Bar dataKey="completed" name="Terminées" fill="#8b5cf6" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </Card>

          <Card className="p-5">
            <h3 className="font-semibold">Prestataires par ville</h3>
            <p className="mb-3 text-sm text-muted">Profils en ligne.</p>
            {s.byCity.length === 0 ? <p className="text-sm text-muted">Aucun prestataire en ligne.</p> : (
              <ol className="space-y-1.5 text-sm">
                {s.byCity.map((c) => (
                  <li key={`${c.city}-${c.country}`} className="flex justify-between gap-2">
                    <span className="truncate">{c.city} <span className="text-muted">· {countryName(c.country)}</span></span>
                    <span className="font-medium">{c.providers}</span>
                  </li>
                ))}
              </ol>
            )}
          </Card>

          <Card className="p-5 xl:col-span-3">
            <h3 className="font-semibold">Prestataires par catégorie</h3>
            <p className="mb-3 text-sm text-muted">Un prestataire compte dans chaque catégorie où il a une offre en ligne.</p>
            {s.byCategory.length === 0 ? <p className="text-sm text-muted">Aucune offre en ligne.</p> : (
              <ul className="grid gap-x-8 gap-y-2 md:grid-cols-2">
                {s.byCategory.map((c) => (
                  <li key={c.id} className="text-sm">
                    <div className="flex justify-between"><span>{c.name} <span className="text-muted">· {c.parentName}</span></span><span className="font-medium">{c.providers}</span></div>
                    <div className="mt-1 h-1.5 rounded-full bg-surface-2"><div className="h-full rounded-full bg-brand" style={{ width: `${(c.providers / maxCategory) * 100}%` }} /></div>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>
      )}
    </>
  )
}
