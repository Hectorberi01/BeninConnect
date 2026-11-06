import { useQuery } from '@tanstack/react-query'
import {
  CalendarDays, Download, FileText, Flag, KeyRound, LayoutDashboard, LogOut, Menu, ScrollText, Server, ShieldCheck, Users, UsersRound, X,
} from 'lucide-react'
import { type ComponentType, useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
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
    ],
  },
  { section: 'Finance', items: [{ to: '/exports', label: 'Exports', icon: Download, permission: 'ExportsDownload' }] },
  {
    section: 'Plateforme',
    items: [
      { to: '/admins', label: 'Administrateurs', icon: ShieldCheck, permission: 'AdminsManage' },
      { to: '/audit', label: "Journal d'audit", icon: ScrollText, permission: 'AuditView' },
      { to: '/system', label: 'Système', icon: Server, permission: 'SystemView' },
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
        <span className="grid size-9 place-items-center rounded-xl bg-brand text-lg font-bold text-white">B</span>
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
                <NavLink key={to} to={to} end={to === '/'} onClick={onNavigate}
                  className={({ isActive }) => cx('flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition',
                    isActive ? 'bg-brand-soft text-brand' : 'text-muted hover:bg-surface-2 hover:text-fg')}>
                  <Icon className="size-4.5" />
                  <span className="flex-1">{label}</span>
                  {to === '/reports' && openReports > 0 && <Badge tone="danger">{openReports}</Badge>}
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

export function Shell() {
  const [open, setOpen] = useState(false)
  return (
    <div className="flex h-full">
      <aside className="hidden w-64 shrink-0 border-r border-line bg-surface lg:block"><Sidebar /></aside>

      {open && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-black/40" onClick={() => setOpen(false)} />
          <aside className="absolute inset-y-0 left-0 w-72 bg-surface shadow-xl">
            <button aria-label="Fermer le menu" onClick={() => setOpen(false)} className="absolute right-3 top-5 rounded-md p-1 text-muted"><X className="size-5" /></button>
            <Sidebar onNavigate={() => setOpen(false)} />
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center gap-3 border-b border-line bg-surface px-4 py-3 lg:hidden">
          <button aria-label="Ouvrir le menu" onClick={() => setOpen(true)} className="rounded-md p-1.5 hover:bg-surface-2"><Menu className="size-5" /></button>
          <span className="font-semibold">BéninConnect · Admin</span>
        </header>
        <main className="flex-1 overflow-y-auto">
          <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8"><Outlet /></div>
        </main>
      </div>
    </div>
  )
}
