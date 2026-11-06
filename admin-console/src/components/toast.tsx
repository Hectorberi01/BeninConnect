import { CheckCircle2, AlertCircle } from 'lucide-react'
import { createContext, type ReactNode, useCallback, useContext, useState } from 'react'

type Toast = { id: number; message: string; kind: 'success' | 'error' }
const ToastContext = createContext<(message: string, kind?: Toast['kind']) => void>(() => {})

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const push = useCallback((message: string, kind: Toast['kind'] = 'success') => {
    const id = Date.now() + Math.random()
    setToasts((t) => [...t, { id, message, kind }])
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 4000)
  }, [])
  return (
    <ToastContext.Provider value={push}>
      {children}
      <div className="pointer-events-none fixed bottom-4 right-4 z-[60] flex flex-col gap-2" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className="flex items-center gap-2 rounded-lg bg-fg px-4 py-3 text-sm text-bg shadow-lg">
            {t.kind === 'success' ? <CheckCircle2 className="size-4 text-brand" /> : <AlertCircle className="size-4 text-danger" />}
            {t.message}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export const useToast = () => useContext(ToastContext)
