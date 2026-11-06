import type { AdminMe } from './types'

export class ApiError extends Error {
  readonly status: number
  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

const BASE = (import.meta.env.VITE_API_URL ?? '').replace(/\/+$/, '')
const TOKEN_KEY = 'beninconnect.admin.token'

// Jeton en sessionStorage : la session se ferme avec l'onglet (poste partagé).
export const session = {
  get token() {
    try {
      return sessionStorage.getItem(TOKEN_KEY)
    } catch {
      return null
    }
  },
  set(token: string | null) {
    try {
      if (token) sessionStorage.setItem(TOKEN_KEY, token)
      else sessionStorage.removeItem(TOKEN_KEY)
    } catch {
      // stockage indisponible : session en mémoire seulement
    }
  },
}

/** Appelé quand l'API répond 401 (jeton expiré, compte désactivé). */
let onUnauthorized: () => void = () => {}
export const setUnauthorizedHandler = (fn: () => void) => { onUnauthorized = fn }

export const apiUrl = (path: string) => `${BASE}/${path.replace(/^\//, '')}`

export async function api<T>(path: string, init: { method?: string; body?: unknown; raw?: boolean } = {}): Promise<T> {
  let response: Response
  try {
    response = await fetch(apiUrl(path), {
      method: init.method ?? 'GET',
      headers: {
        Accept: 'application/json',
        'ngrok-skip-browser-warning': '1',
        ...(init.body !== undefined ? { 'Content-Type': 'application/json' } : {}),
        ...(session.token ? { Authorization: `Bearer ${session.token}` } : {}),
      },
      body: init.body !== undefined ? JSON.stringify(init.body) : undefined,
    })
  } catch {
    throw new ApiError("Impossible de joindre l'API. Vérifiez qu'elle est démarrée.", 0)
  }

  if (response.ok) {
    if (init.raw) return response as unknown as T
    if (response.status === 204) return undefined as T
    return (await response.json()) as T
  }

  if (response.status === 401) {
    onUnauthorized()
    throw new ApiError('Session expirée. Reconnectez-vous.', 401)
  }

  let detail: string | undefined
  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    detail = problem.detail || problem.title
  } catch {
    // corps vide
  }
  if (response.status === 403) throw new ApiError(detail ?? "Votre rôle ne permet pas cette action.", 403)
  if (response.status >= 500) throw new ApiError("L'API a rencontré une erreur. Réessayez.", response.status)
  throw new ApiError(detail ?? `Requête refusée (${response.status}).`, response.status)
}

export const errorMessage = (e: unknown) => (e instanceof Error ? e.message : 'Erreur inattendue.')

export async function login(email: string, password: string) {
  const result = await api<{ token: string; admin: AdminMe }>('api/admin/auth/login', { method: 'POST', body: { email, password } })
  session.set(result.token)
  return result.admin
}

/** Téléchargement d'un export CSV (le jeton doit accompagner la requête). */
export async function download(path: string, fallbackName: string) {
  const response = await api<Response>(path, { raw: true })
  const name = /filename="?([^";]+)"?/.exec(response.headers.get('content-disposition') ?? '')?.[1] ?? fallbackName
  const url = URL.createObjectURL(await response.blob())
  const a = document.createElement('a')
  a.href = url
  a.download = name
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
  return name
}

export const qs = (params: Record<string, string | number | boolean | null | undefined>) => {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== '')
  return entries.length ? `?${new URLSearchParams(entries.map(([k, v]) => [k, String(v)]))}` : ''
}
