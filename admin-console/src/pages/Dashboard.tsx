import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useAuth } from '@/auth'
import { Card, ErrorBox, Loading, PageHeader, Stat } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmtDay, fmtNumber } from '@/lib/format'
import { ROLE_LABELS } from '@/lib/permissions'
import type { AdminStats, DailyPoint, LabelCount } from '@/lib/types'

function DailyChart({ title, data, total }: { title: string; data: DailyPoint[]; total: number }) {
  return (
    <Card className="p-5">
      <div className="mb-4 flex items-baseline justify-between">
        <h2 className="font-semibold">{title}</h2>
        <span className="text-sm text-muted">{fmtNumber(total)} sur 30 jours</span>
      </div>
      <div className="h-56">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} margin={{ left: -24, right: 4, top: 4 }}>
            <CartesianGrid vertical={false} stroke="var(--line)" />
            <XAxis dataKey="day" tickFormatter={fmtDay} tick={{ fontSize: 11, fill: 'var(--muted)' }} tickLine={false} axisLine={false} interval={6} />
            <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: 'var(--muted)' }} tickLine={false} axisLine={false} />
            <Tooltip cursor={{ fill: 'var(--surface-2)' }} labelFormatter={(d) => fmtDay(String(d))} formatter={(v) => [v, 'Total']}
              contentStyle={{ background: 'var(--surface)', border: '1px solid var(--line)', borderRadius: 8, fontSize: 12 }} />
            <Bar dataKey="count" fill="var(--brand)" radius={[4, 4, 0, 0]} maxBarSize={18} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </Card>
  )
}

function Ranking({ title, rows, unit }: { title: string; rows: LabelCount[]; unit: string }) {
  const max = Math.max(1, ...rows.map((r) => r.count))
  return (
    <Card className="p-5">
      <h2 className="mb-4 font-semibold">{title}</h2>
      <ul className="space-y-3">
        {rows.map((r) => (
          <li key={r.label}>
            <div className="mb-1 flex justify-between gap-4 text-sm">
              <span className="truncate">{r.label}</span>
              <span className="shrink-0 text-muted">{fmtNumber(r.count)} {unit}</span>
            </div>
            <div className="h-1.5 rounded-full bg-surface-2"><div className="h-1.5 rounded-full bg-brand" style={{ width: `${(100 * r.count) / max}%` }} /></div>
          </li>
        ))}
      </ul>
    </Card>
  )
}

export function DashboardPage() {
  const { me, can } = useAuth()
  const q = useQuery({ queryKey: ['stats'], queryFn: () => api<AdminStats>('api/admin/stats') })
  const s = q.data

  return (
    <>
      <PageHeader title={`Bonjour ${me?.displayName.split(' ')[0] ?? ''}`} description={`${me ? ROLE_LABELS[me.role] : ''} · vue d'ensemble de la plateforme`} />
      {q.isPending ? <Loading /> : q.error || !s ? <ErrorBox message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : (
        <div className="space-y-6">
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <Stat label="Membres" value={fmtNumber(s.members)} hint={`+${s.newMembers7Days} cette semaine · +${s.newMembers30Days} en 30 j`} />
            <Stat label="Publications" value={fmtNumber(s.posts)} hint={`${fmtNumber(s.comments)} commentaires · ${s.hiddenPosts} masquées`} />
            <Stat label="Communautés" value={fmtNumber(s.communities)} hint={`dont ${s.privateCommunities} privées`} />
            <Stat label="Événements à venir" value={fmtNumber(s.upcomingEvents)} hint={`${fmtNumber(s.eventParticipations)} inscriptions`} />
          </div>
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <Stat label="Signalements ouverts" value={s.openReports} tone={s.openReports > 0 ? 'warning' : undefined}
              hint={can('ReportsManage') && s.openReports > 0 ? <Link className="text-brand hover:underline" to="/reports">Traiter la file →</Link> : 'Aucune action requise'} />
            <Stat label="Membres suspendus" value={s.suspendedMembers} tone={s.suspendedMembers > 0 ? 'danger' : undefined} />
            <Stat label="Emails vérifiés" value={`${Math.round((100 * s.verifiedMembers) / Math.max(1, s.members))} %`} hint={`${fmtNumber(s.verifiedMembers)} membres`} />
            <Stat label="Messages échangés" value={fmtNumber(s.messages)} />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <DailyChart title="Inscriptions" data={s.signups} total={s.newMembers30Days} />
            <DailyChart title="Publications" data={s.postsPerDay} total={s.postsPerDay.reduce((n, d) => n + d.count, 0)} />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <Ranking title="Communautés les plus actives" rows={s.topCommunities} unit="membres" />
            <Ranking title="Membres par pays" rows={s.membersByCountry} unit="membres" />
          </div>
        </div>
      )}
    </>
  )
}
