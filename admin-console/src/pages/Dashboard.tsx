import { useQuery } from '@tanstack/react-query'
import {
  Ban, CalendarDays, FileText, Flag, Globe2, MailCheck, MessagesSquare, Sparkles, TrendingUp, Users, UsersRound,
} from 'lucide-react'
import { motion } from 'motion/react'
import { type ReactNode, useState } from 'react'
import { Link } from 'react-router-dom'
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useAuth } from '@/auth'
import { AnimatedNumber, Cube, Float, ProgressRing, revealItem, Stagger } from '@/components/motion'
import { Card, ErrorBox, Loading, Stat } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmtDay, fmtNumber } from '@/lib/format'
import { ROLE_LABELS } from '@/lib/permissions'
import type { AdminStats, DailyPoint, LabelCount } from '@/lib/types'

const today = new Intl.DateTimeFormat('fr-FR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

/** Bandeau d'accueil : dégradé aux couleurs de BéninConnect, formes 3D en mouvement. */
function Hero({ name, role, s }: { name: string; role: string; s: AdminStats }) {
  const figures = [
    { label: 'nouveaux membres (7 j)', value: s.newMembers7Days },
    { label: 'publications (30 j)', value: s.postsPerDay.reduce((n, d) => n + d.count, 0) },
    { label: 'messages échangés', value: s.messages },
  ]
  const [date] = useState(() => today.format(new Date()))
  return (
    <motion.section variants={revealItem}
      className="relative mb-6 overflow-hidden rounded-2xl bg-gradient-to-br from-[#0b9e71] via-[#087f5b] to-[#043d2c] p-6 text-white shadow-[0_24px_48px_-24px_#087f5b] sm:p-8">
      <div aria-hidden className="absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,rgba(252,209,22,0.28),transparent_45%),radial-gradient(circle_at_10%_90%,rgba(255,255,255,0.12),transparent_40%)]" />
      <div aria-hidden className="absolute inset-0 opacity-[0.07] [background-image:linear-gradient(rgba(255,255,255,.8)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,.8)_1px,transparent_1px)] [background-size:32px_32px]" />
      <Float className="absolute right-8 top-6 hidden md:block"><Cube size={70} /></Float>
      <Float className="absolute bottom-6 right-40 hidden lg:block" delay={1.5} distance={14}><Cube size={38} duration={12} /></Float>
      <Float className="absolute right-64 top-8 hidden lg:block" delay={0.8}>
        <span className="block size-10 rounded-full border-2 border-[#fcd116]/70 shadow-[0_0_24px_rgba(252,209,22,0.45)]" />
      </Float>

      <div className="relative max-w-2xl">
        <p className="flex items-center gap-2 text-sm text-white/75"><Sparkles className="size-4" />{date}</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight sm:text-4xl">Bonjour {name}</h1>
        <p className="mt-1 text-white/80">{role} · vue d'ensemble de la plateforme</p>
        <div className="mt-6 grid grid-cols-3 gap-3 sm:gap-6">
          {figures.map((f) => (
            <div key={f.label} className="rounded-xl bg-white/10 px-3 py-2.5 ring-1 ring-white/15 backdrop-blur-sm">
              <p className="text-2xl font-semibold"><AnimatedNumber value={f.value} /></p>
              <p className="text-xs leading-snug text-white/75">{f.label}</p>
            </div>
          ))}
        </div>
      </div>
    </motion.section>
  )
}

function DailyChart({ title, data, total, color, id }: { title: string; data: DailyPoint[]; total: number; color: string; id: string }) {
  return (
    <Card className="p-5">
      <div className="mb-4 flex items-baseline justify-between">
        <h2 className="flex items-center gap-2 font-semibold"><TrendingUp className="size-4" style={{ color }} />{title}</h2>
        <span className="text-sm text-muted"><b className="font-semibold text-fg">{fmtNumber(total)}</b> sur 30 jours</span>
      </div>
      <div className="h-60">
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={data} margin={{ left: -24, right: 4, top: 8 }}>
            <defs>
              <linearGradient id={id} x1="0" x2="0" y1="0" y2="1">
                <stop offset="0%" stopColor={color} stopOpacity={0.35} />
                <stop offset="100%" stopColor={color} stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid vertical={false} stroke="var(--line)" strokeDasharray="3 4" />
            <XAxis dataKey="day" tickFormatter={fmtDay} tick={{ fontSize: 11, fill: 'var(--muted)' }} tickLine={false} axisLine={false} interval={6} />
            <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: 'var(--muted)' }} tickLine={false} axisLine={false} />
            <Tooltip cursor={{ stroke: color, strokeDasharray: '4 4' }} labelFormatter={(d) => fmtDay(String(d))} formatter={(v) => [v, 'Total']}
              contentStyle={{ background: 'var(--surface)', border: '1px solid var(--line)', borderRadius: 10, fontSize: 12, boxShadow: '0 12px 24px -12px rgba(0,0,0,.25)' }} />
            <Area type="monotone" dataKey="count" stroke={color} strokeWidth={2.4} fill={`url(#${id})`}
              activeDot={{ r: 5, strokeWidth: 2, stroke: 'var(--surface)' }} animationDuration={1200} />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </Card>
  )
}

function Ranking({ title, rows, unit, icon }: { title: string; rows: LabelCount[]; unit: string; icon: ReactNode }) {
  const max = Math.max(1, ...rows.map((r) => r.count))
  return (
    <Card className="p-5">
      <h2 className="mb-4 flex items-center gap-2 font-semibold">{icon}{title}</h2>
      <ul className="space-y-3.5">
        {rows.map((r, i) => (
          <li key={r.label} className="flex items-center gap-3">
            <span className={`grid size-7 shrink-0 place-items-center rounded-lg text-xs font-semibold ${i === 0 ? 'bg-[#fcd116]/25 text-warning' : 'bg-surface-2 text-muted'}`}>{i + 1}</span>
            <div className="min-w-0 flex-1">
              <div className="mb-1 flex justify-between gap-4 text-sm">
                <span className="truncate font-medium">{r.label}</span>
                <span className="shrink-0 text-muted">{fmtNumber(r.count)} {unit}</span>
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-surface-2">
                <motion.div className="h-2 rounded-full bg-gradient-to-r from-brand to-[#38d9a9]"
                  initial={{ width: 0 }} animate={{ width: `${(100 * r.count) / max}%` }}
                  transition={{ duration: 0.9, delay: 0.25 + i * 0.07, ease: [0.22, 1, 0.36, 1] }} />
              </div>
            </div>
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
  if (q.isPending) return <Loading />
  if (q.error || !s) return <ErrorBox message={errorMessage(q.error)} onRetry={() => q.refetch()} />

  const verified = s.verifiedMembers / Math.max(1, s.members)
  return (
    <Stagger>
      <Hero name={me?.displayName.split(' ')[0] ?? ''} role={me ? ROLE_LABELS[me.role] : ''} s={s} />

      <div className="space-y-6">
        <Stagger className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4" delay={0.1}>
          <Stat label="Membres" count={s.members} icon={<Users />} accent="brand" spark={s.signups.map((d) => d.count)}
            hint={`+${s.newMembers7Days} cette semaine · +${s.newMembers30Days} en 30 j`} />
          <Stat label="Publications" count={s.posts} icon={<FileText />} accent="info" spark={s.postsPerDay.map((d) => d.count)}
            hint={`${fmtNumber(s.comments)} commentaires · ${s.hiddenPosts} masquées`} />
          <Stat label="Communautés" count={s.communities} icon={<UsersRound />} accent="violet" hint={`dont ${s.privateCommunities} privées`} />
          <Stat label="Événements à venir" count={s.upcomingEvents} icon={<CalendarDays />} accent="warning"
            hint={`${fmtNumber(s.eventParticipations)} inscriptions`} />
        </Stagger>

        <Stagger className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4" delay={0.2}>
          <Stat label="Signalements ouverts" count={s.openReports} icon={<Flag />} accent={s.openReports > 0 ? 'warning' : 'brand'}
            tone={s.openReports > 0 ? 'warning' : undefined}
            hint={can('ReportsManage') && s.openReports > 0 ? <Link className="font-medium text-brand hover:underline" to="/reports">Traiter la file →</Link> : 'Aucune action requise'} />
          <Stat label="Membres suspendus" count={s.suspendedMembers} icon={<Ban />} accent={s.suspendedMembers > 0 ? 'danger' : 'brand'}
            tone={s.suspendedMembers > 0 ? 'danger' : undefined} hint="Comptes bloqués par la modération" />
          <Stat label="Emails vérifiés" count={Math.round(verified * 100)} suffix=" %" icon={<MailCheck />} accent="brand"
            hint={`${fmtNumber(s.verifiedMembers)} membres`} aside={<ProgressRing value={verified} className="shrink-0 text-brand" />} />
          <Stat label="Messages échangés" count={s.messages} icon={<MessagesSquare />} accent="info" hint="Discussions privées" />
        </Stagger>

        <Stagger className="grid gap-4 lg:grid-cols-2" delay={0.3}>
          <DailyChart title="Inscriptions" data={s.signups} total={s.newMembers30Days} color="#0b9e71" id="g-signups" />
          <DailyChart title="Publications" data={s.postsPerDay} total={s.postsPerDay.reduce((n, d) => n + d.count, 0)} color="#2e90fa" id="g-posts" />
        </Stagger>

        <Stagger className="grid gap-4 lg:grid-cols-2" delay={0.4}>
          <Ranking title="Communautés les plus actives" rows={s.topCommunities} unit="membres" icon={<UsersRound className="size-4 text-brand" />} />
          <Ranking title="Membres par pays" rows={s.membersByCountry} unit="membres" icon={<Globe2 className="size-4 text-info" />} />
        </Stagger>
      </div>
    </Stagger>
  )
}
