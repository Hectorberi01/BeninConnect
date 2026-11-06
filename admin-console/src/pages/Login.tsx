import { ShieldCheck } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth'
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
    <div className="grid min-h-full place-items-center px-4 py-10">
      <div className="w-full max-w-md">
        <div className="mb-8 flex items-center gap-3">
          <span className="grid size-11 place-items-center rounded-2xl bg-brand text-xl font-bold text-white">B</span>
          <div>
            <h1 className="text-xl font-semibold">BéninConnect</h1>
            <p className="text-sm text-muted">Console d'administration</p>
          </div>
        </div>

        <form onSubmit={submit} className="rounded-2xl border border-line bg-surface p-6 shadow-sm">
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
      </div>
    </div>
  )
}
