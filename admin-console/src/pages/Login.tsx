import { ShieldCheck } from 'lucide-react'
import { motion } from 'motion/react'
import { type FormEvent, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth'
import { Cube, Float } from '@/components/motion'
import { Button, Input } from '@/components/ui'
import { errorMessage } from '@/lib/api'
import { ROLE_LABELS } from '@/lib/permissions'
import type { AdminRole } from '@/lib/types'

// Comptes du jeu de démonstration (affichés en développement uniquement).
const DEMO: { email: string; role: AdminRole }[] = [
  { email: 'superadmin@beninconnect.test', role: 'SuperAdmin' },
  { email: 'admin@beninconnect.test', role: 'Admin' },
  { email: 'moderation@beninconnect.test', role: 'Moderator' },
  { email: 'compta@beninconnect.test', role: 'Accountant' },
  { email: 'dev@beninconnect.test', role: 'Developer' },
]
const DEMO_PASSWORD = 'Admin2026!'
const SHOW_DEMO = import.meta.env.DEV || import.meta.env.VITE_SHOW_DEMO_ACCOUNTS === 'true'

export function LoginPage() {
  const { me, login } = useAuth()
  const navigate = useNavigate()
  const from = (useLocation().state as { from?: string } | null)?.from ?? '/'
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (me) return <Navigate to={from} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await login(email, password)
      navigate(from, { replace: true })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="relative grid min-h-full place-items-center overflow-hidden px-4 py-10">
      <div aria-hidden className="pointer-events-none absolute inset-0">
        <motion.div className="absolute -left-24 -top-24 size-[480px] rounded-full bg-brand/20 blur-3xl"
          animate={{ x: [0, 80, 0], y: [0, 50, 0] }} transition={{ duration: 18, repeat: Infinity, ease: 'easeInOut' }} />
        <motion.div className="absolute -bottom-32 -right-24 size-[420px] rounded-full bg-[#fcd116]/20 blur-3xl"
          animate={{ x: [0, -60, 0], y: [0, -40, 0] }} transition={{ duration: 22, repeat: Infinity, ease: 'easeInOut' }} />
        <Float className="absolute right-[12%] top-[14%] hidden md:block"><Cube size={64} className="[&_div]:border-brand/40 [&_div]:bg-brand/10" /></Float>
        <Float className="absolute bottom-[16%] left-[10%] hidden md:block" delay={1.2}><Cube size={40} duration={13} className="[&_div]:border-[#e8112d]/30 [&_div]:bg-[#e8112d]/5" /></Float>
      </div>
      <motion.div className="relative w-full max-w-md" initial={{ opacity: 0, y: 24, rotateX: 10 }} animate={{ opacity: 1, y: 0, rotateX: 0 }}
        transition={{ type: 'spring', stiffness: 160, damping: 20 }} style={{ transformPerspective: 1000 }}>
        <div className="mb-8 flex items-center gap-3">
          <motion.span className="grid size-12 place-items-center rounded-2xl bg-gradient-to-br from-[#12b886] to-[#044a35] text-xl font-bold text-white shadow-[0_10px_24px_-10px_#087f5b]"
            animate={{ rotateY: [0, 360] }} transition={{ duration: 1.2, delay: 0.3, ease: [0.22, 1, 0.36, 1] }} style={{ transformPerspective: 400 }}>B</motion.span>
          <div>
            <h1 className="text-xl font-semibold">BéninConnect</h1>
            <p className="text-sm text-muted">Console d'administration</p>
          </div>
        </div>

        <form onSubmit={submit} className="rounded-2xl border border-line bg-surface/85 p-6 shadow-[0_24px_60px_-28px_rgba(16,24,40,0.45)] backdrop-blur-xl">
          <h2 className="mb-1 text-lg font-semibold">Connexion</h2>
          <p className="mb-5 text-sm text-muted">Réservé aux comptes administrateurs : les identifiants des membres de l'application ne fonctionnent pas ici.</p>
          {error && <p role="alert" className="mb-4 rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{error}</p>}
          <div className="space-y-4">
            <Input label="Adresse email" type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
            <Input label="Mot de passe" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
          </div>
          <Button type="submit" variant="primary" loading={busy} className="mt-6 w-full" icon={<ShieldCheck className="size-4" />}>Se connecter</Button>
        </form>

        {SHOW_DEMO && (
          <div className="mt-5 rounded-2xl border border-dashed border-line p-4">
            <p className="mb-3 text-sm font-medium">Comptes de démonstration <span className="font-normal text-muted">· mot de passe {DEMO_PASSWORD}</span></p>
            <div className="grid gap-2 sm:grid-cols-2">
              {DEMO.map((d) => (
                <button key={d.email} type="button" onClick={() => { setEmail(d.email); setPassword(DEMO_PASSWORD) }}
                  className="rounded-lg border border-line bg-surface px-3 py-2 text-left text-sm hover:border-brand">
                  <span className="block font-medium">{ROLE_LABELS[d.role]}</span>
                  <span className="block truncate text-xs text-muted">{d.email}</span>
                </button>
              ))}
            </div>
          </div>
        )}
      </motion.div>
    </div>
  )
}
