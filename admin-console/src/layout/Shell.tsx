import { useQuery } from '@tanstack/react-query'
import {
  BriefcaseBusiness, CalendarDays, Contact, Handshake, ListChecks, ShieldAlert, Smartphone, Tags, DatabaseBackup, Download, FileText, Flag, KeyRound, LayoutDashboard, LogOut, Menu, Newspaper, ScrollText, Server, ShieldCheck, Users, UsersRound, X,
} from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { type ComponentType, useState } from 'react'
import { NavLink, useLocation, useNavigate, useOutlet } from 'react-router-dom'
import { useAuth } from '@/auth'
import { Badge, cx } from '@/components/ui'
import { api } from '@/lib/api'
import { ROLE_LABELS } from '@/lib/permissions'
import type { Paged, Permission } from '@/lib/types'

type Item = { to: string; label: string; icon: ComponentType<{ className?: string }>; permission: Permission }

export const NAV: { section: string; items: Item[] }[] = [
  { section: 'Pilotage', items: [{ to: '/', label: 'Tableau de bord', icon: LayoutDashboard, permission: 'DashboardView' }] },
  {
    section: 'Modération',
    items: [
      { to: '/reports', label: 'Signalements', icon: Flag, permission: 'ReportsManage' },
      { to: '/members', label: 'Membres', icon: Users, permission: 'MembersView' },
      { to: '/posts', label: 'Publications', icon: FileText, permission: 'ContentModerate' },
    ],
  },
  {
    section: 'Communauté',
    items: [
      { to: '/communities', label: 'Communautés', icon: UsersRound, permission: 'CommunitiesManage' },
      { to: '/events', label: 'Événements', icon: CalendarDays, permission: 'EventsManage' },
      { to: '/editorial', label: 'Compte officiel', icon: Newspaper, permission: 'EditorialManage' },
    ],
  },
  {
    section: 'Emploi',
    items: [
      { to: '/jobs', label: 'Sources et réglages', icon: BriefcaseBusiness, permission: 'JobsManage' },
      { to: '/jobs/offers', label: 'Offres importées', icon: ListChecks, permission: 'JobsManage' },
      { to: '/jobs/preview', label: 'Aperçu membres', icon: Smartphone, permission: 'JobsManage' },
    ],
  },
  {
    section: 'Services',
    items: [
      { to: '/services', label: 'Tableau de bord', icon: Handshake, permission: 'ServicesManage' },
      { to: '/services/moderation', label: 'Modération', icon: ShieldAlert, permission: 'ServicesManage' },
      { to: '/services/providers', label: 'Prestataires', icon: Contact, permission: 'ServicesManage' },
      { to: '/services/categories', label: 'Catégories et réglages', icon: Tags, permission: 'ServicesManage' },
    ],
  },
  { section: 'Finance', items: [{ to: '/exports', label: 'Exports', icon: Download, permission: 'ExportsDownload' }] },
  {
    section: 'Plateforme',
    items: [
      { to: '/admins', label: 'Administrateurs', icon: ShieldCheck, permission: 'AdminsManage' },
      { to: '/audit', label: "Journal d'audit", icon: ScrollText, permission: 'AuditView' },
      { to: '/system', label: 'Système', icon: Server, permission: 'SystemView' },
      { to: '/backups', label: 'Sauvegardes', icon: DatabaseBackup, permission: 'BackupsManage' },
    ],
  },
]

function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const { me, can, logout } = useAuth()
  const navigate = useNavigate()
  const openReports = useQuery({
    queryKey: ['reports-open-count'],
    queryFn: () => api<Paged<unknown>>('api/admin/reports?status=Open&take=1').then((p) => p.totalCount),
    enabled: can('ReportsManage'),
    refetchInterval: 60_000,
  }).data ?? 0

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center gap-3 px-5 py-5">
        <Logo />
        <div className="leading-tight">
          <p className="font-semibold">BéninConnect</p>
          <p className="text-xs text-muted">Administration</p>
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto px-3 pb-4" aria-label="Navigation principale">
        {NAV.map((group) => {
          const items = group.items.filter((i) => can(i.permission))
          if (items.length === 0) return null
          return (
            <div key={group.section} className="mt-4 first:mt-0">
              <p className="px-3 pb-1.5 text-[11px] font-semibold uppercase tracking-wider text-muted">{group.section}</p>
              {items.map(({ to, label, icon: Icon }) => (
                <NavLink key={to} to={to} end={to === '/' || to === '/jobs' || to === '/services'} onClick={onNavigate}
                  className={({ isActive }) => cx('group relative flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                    isActive ? 'text-brand' : 'text-muted hover:bg-surface-2 hover:text-fg')}>
                  {({ isActive }) => (
                    <>
                      {isActive && (
                        <motion.span layoutId="nav-active" className="absolute inset-0 rounded-lg bg-brand-soft shadow-[inset_3px_0_0_var(--brand)]"
                          transition={{ type: 'spring', stiffness: 420, damping: 34 }} />
                      )}
                      <Icon className="relative size-4.5 transition-transform group-hover:scale-110" />
                      <span className="relative flex-1">{label}</span>
                      {to === '/reports' && openReports > 0 && <span className="relative"><Badge tone="danger">{openReports}</Badge></span>}
                    </>
                  )}
                </NavLink>
              ))}
            </div>
          )
        })}
      </nav>

      <div className="border-t border-line p-3">
        <div className="px-3 py-2">
          <p className="truncate text-sm font-medium">{me?.displayName}</p>
          <p className="truncate text-xs text-muted">{me?.email}</p>
          {me && <div className="mt-2"><Badge tone="brand">{ROLE_LABELS[me.role]}</Badge></div>}
        </div>
        <NavLink to="/account" onClick={onNavigate} className="flex items-center gap-3 rounded-lg px-3 py-2 text-sm text-muted hover:bg-surface-2 hover:text-fg">
          <KeyRound className="size-4" /> Mon mot de passe
        </NavLink>
        <button onClick={() => { logout(); navigate('/login') }}
          className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm text-muted hover:bg-surface-2 hover:text-danger">
          <LogOut className="size-4" /> Se déconnecter
        </button>
      </div>
    </div>
  )
}

/** Logo en relief (trois pastilles aux couleurs du Bénin). */
function Logo() {
  return (
    <motion.span className="relative grid size-10 place-items-center rounded-xl bg-gradient-to-br from-[#12b886] to-[#044a35] text-lg font-bold text-white shadow-[0_8px_18px_-8px_#087f5b]"
      whileHover={{ rotateY: 180 }} transition={{ type: 'spring', stiffness: 200, damping: 16 }} style={{ transformPerspective: 400 }}>
      B
      <span className="absolute -bottom-1 -right-1 flex gap-0.5 rounded-full bg-surface p-0.5 shadow">
        <span className="size-1.5 rounded-full bg-[#008751]" /><span className="size-1.5 rounded-full bg-[#fcd116]" /><span className="size-1.5 rounded-full bg-[#e8112d]" />
      </span>
    </motion.span>
  )
}

/** Fond animé discret : halos de couleur qui dérivent lentement. */
function Backdrop() {
  return (
    <div aria-hidden className="pointer-events-none absolute inset-0 overflow-hidden">
      <motion.div className="absolute -left-32 -top-40 size-[520px] rounded-full bg-brand/10 blur-3xl"
        animate={{ x: [0, 60, 0], y: [0, 40, 0] }} transition={{ duration: 22, repeat: Infinity, ease: 'easeInOut' }} />
      <motion.div className="absolute -right-40 top-1/3 size-[460px] rounded-full bg-info/10 blur-3xl"
        animate={{ x: [0, -50, 0], y: [0, -30, 0] }} transition={{ duration: 26, repeat: Infinity, ease: 'easeInOut' }} />
      <motion.div className="absolute bottom-[-180px] left-1/3 size-[420px] rounded-full bg-warning/10 blur-3xl"
        animate={{ x: [0, 40, 0], y: [0, -20, 0] }} transition={{ duration: 30, repeat: Infinity, ease: 'easeInOut' }} />
    </div>
  )
}

/** Transition entre les pages : l'ancienne page s'efface, la nouvelle glisse en place. */
function AnimatedOutlet() {
  const location = useLocation()
  const outlet = useOutlet()
  return (
    <AnimatePresence mode="wait" initial={false}>
      <motion.div key={location.pathname}
        initial={{ opacity: 0, y: 14, filter: 'blur(4px)' }} animate={{ opacity: 1, y: 0, filter: 'blur(0px)' }}
        exit={{ opacity: 0, y: -8, filter: 'blur(4px)' }} transition={{ duration: 0.22, ease: [0.22, 1, 0.36, 1] }}>
        {outlet}
      </motion.div>
    </AnimatePresence>
  )
}

export function Shell() {
  const [open, setOpen] = useState(false)
  return (
    <div className="flex h-full">
      <aside className="relative z-10 hidden w-64 shrink-0 border-r border-line bg-surface/80 backdrop-blur-xl lg:block"><Sidebar /></aside>

      <AnimatePresence>
        {open && (
          <div className="fixed inset-0 z-40 lg:hidden">
            <motion.div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setOpen(false)}
              initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} />
            <motion.aside className="absolute inset-y-0 left-0 w-72 bg-surface shadow-xl"
              initial={{ x: '-100%' }} animate={{ x: 0 }} exit={{ x: '-100%' }} transition={{ type: 'spring', stiffness: 360, damping: 36 }}>
              <button aria-label="Fermer le menu" onClick={() => setOpen(false)} className="absolute right-3 top-5 rounded-md p-1 text-muted"><X className="size-5" /></button>
              <Sidebar onNavigate={() => setOpen(false)} />
            </motion.aside>
          </div>
        )}
      </AnimatePresence>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center gap-3 border-b border-line bg-surface px-4 py-3 lg:hidden">
          <button aria-label="Ouvrir le menu" onClick={() => setOpen(true)} className="rounded-md p-1.5 hover:bg-surface-2"><Menu className="size-5" /></button>
          <span className="font-semibold">BéninConnect · Admin</span>
        </header>
        <main className="relative flex-1 overflow-y-auto">
          <Backdrop />
          <div className="relative mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8"><AnimatedOutlet /></div>
        </main>
      </div>
    </div>
  )
}
