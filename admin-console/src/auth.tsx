import { useQueryClient } from '@tanstack/react-query'
import { createContext, type ReactNode, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { api, login as apiLogin, session, setUnauthorizedHandler } from '@/lib/api'
import { has } from '@/lib/permissions'
import type { AdminMe, Permission } from '@/lib/types'

interface AuthState {
  me: AdminMe | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  can: (p: Permission) => boolean
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const [me, setMe] = useState<AdminMe | null>(null)
  const [loading, setLoading] = useState(!!session.token)

  const logout = useCallback(() => {
    session.set(null)
    setMe(null)
    qc.clear()
  }, [qc])

  const refresh = useCallback(async () => {
    if (!session.token) return
    try {
      setMe(await api<AdminMe>('api/admin/auth/me'))
    } catch {
      logout()
    }
  }, [logout])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    refresh().finally(() => setLoading(false))
  }, [logout, refresh])

  const value = useMemo<AuthState>(() => ({
    me,
    loading,
    login: async (email, password) => setMe(await apiLogin(email, password)),
    logout,
    can: (p) => has(me?.permissions, p),
    refresh,
  }), [me, loading, logout, refresh])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('AuthProvider manquant')
  return ctx
}

/** Accès réservé : sans session, retour à la connexion ; sans le droit, page « accès refusé ». */
export function Guard({ permission, children }: { permission?: Permission; children: ReactNode }) {
  const { me, loading, can } = useAuth()
  const location = useLocation()
  if (loading) return <div className="grid h-full place-items-center text-muted">Chargement…</div>
  if (!me) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (permission && !can(permission)) return <Navigate to="/forbidden" replace />
  return <>{children}</>
}
