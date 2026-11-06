import { Loader2, X } from 'lucide-react'
import { type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, useEffect, useState } from 'react'
import { apiUrl } from '@/lib/api'
import { initials } from '@/lib/format'

const cx = (...c: (string | false | null | undefined)[]) => c.filter(Boolean).join(' ')
export { cx }

type Variant = 'primary' | 'secondary' | 'danger' | 'dangerSoft' | 'ghost'

export function Button({ variant = 'secondary', loading, icon, children, className, ...rest }:
  ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant; loading?: boolean; icon?: ReactNode }) {
  const styles: Record<Variant, string> = {
    primary: 'bg-brand text-white hover:bg-brand-strong shadow-sm',
    secondary: 'bg-surface text-fg border border-line hover:bg-surface-2',
    danger: 'bg-danger text-white hover:opacity-90 shadow-sm',
    dangerSoft: 'bg-danger-soft text-danger hover:bg-danger hover:text-white',
    ghost: 'text-muted hover:text-fg hover:bg-surface-2',
  }
  return (
    <button {...rest} disabled={rest.disabled || loading}
      className={cx('inline-flex h-9 items-center justify-center gap-2 rounded-lg px-3.5 text-sm font-medium transition disabled:cursor-not-allowed disabled:opacity-50',
        styles[variant], className)}>
      {loading ? <Loader2 className="size-4 animate-spin" /> : icon}
      {children}
    </button>
  )
}

export function Input({ label, error, className, ...rest }: InputHTMLAttributes<HTMLInputElement> & { label?: string; error?: string | null }) {
  return (
    <label className={cx('block', className)}>
      {label && <span className="mb-1.5 block text-sm font-medium">{label}</span>}
      <input {...rest} className={cx('h-10 w-full rounded-lg border bg-surface px-3 text-sm outline-none transition placeholder:text-muted focus:border-brand focus:ring-2 focus:ring-brand/20',
        error ? 'border-danger' : 'border-line')} />
      {error && <span className="mt-1 block text-xs text-danger">{error}</span>}
    </label>
  )
}

export function Textarea({ label, ...rest }: InputHTMLAttributes<HTMLTextAreaElement> & { label?: string }) {
  return (
    <label className="block">
      {label && <span className="mb-1.5 block text-sm font-medium">{label}</span>}
      <textarea {...rest} rows={3} className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm outline-none placeholder:text-muted focus:border-brand focus:ring-2 focus:ring-brand/20" />
    </label>
  )
}

export function Select({ label, children, className, ...rest }: SelectHTMLAttributes<HTMLSelectElement> & { label?: string }) {
  return (
    <label className={cx('block', className)}>
      {label && <span className="mb-1.5 block text-sm font-medium">{label}</span>}
      <select {...rest} className="h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm outline-none focus:border-brand focus:ring-2 focus:ring-brand/20">
        {children}
      </select>
    </label>
  )
}

type Tone = 'neutral' | 'brand' | 'danger' | 'warning' | 'info'

export function Badge({ tone = 'neutral', children }: { tone?: Tone; children: ReactNode }) {
  const tones: Record<Tone, string> = {
    neutral: 'bg-surface-2 text-muted border-line',
    brand: 'bg-brand-soft text-brand border-transparent',
    danger: 'bg-danger-soft text-danger border-transparent',
    warning: 'bg-warning-soft text-warning border-transparent',
    info: 'bg-info-soft text-info border-transparent',
  }
  return <span className={cx('inline-flex items-center gap-1 whitespace-nowrap rounded-full border px-2 py-0.5 text-xs font-medium', tones[tone])}>{children}</span>
}

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cx('rounded-xl border border-line bg-surface shadow-[0_1px_2px_rgba(16,24,40,0.04)]', className)}>{children}</div>
}

export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        {description && <p className="mt-1 text-sm text-muted">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}

export function Avatar({ first, last, color, image, size = 36 }: { first: string; last: string; color: string; image?: string | null; size?: number }) {
  const src = image?.startsWith('uploads/') ? apiUrl(image) : null
  return src ? (
    <img src={src} alt="" className="shrink-0 rounded-full object-cover" style={{ width: size, height: size }} />
  ) : (
    <span className="grid shrink-0 place-items-center rounded-full font-semibold text-white" style={{ width: size, height: size, background: color, fontSize: size * 0.38 }}>
      {initials(first, last)}
    </span>
  )
}

export function Empty({ title, description }: { title: string; description?: string }) {
  return (
    <div className="px-6 py-14 text-center">
      <p className="font-medium">{title}</p>
      {description && <p className="mt-1 text-sm text-muted">{description}</p>}
    </div>
  )
}

export function Loading({ label = 'Chargement…' }: { label?: string }) {
  return <div className="flex items-center justify-center gap-2 px-6 py-14 text-sm text-muted"><Loader2 className="size-4 animate-spin" />{label}</div>
}

export function ErrorBox({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="m-4 flex items-center justify-between gap-4 rounded-lg bg-danger-soft px-4 py-3 text-sm text-danger" role="alert">
      <span>{message}</span>
      {onRetry && <Button onClick={onRetry}>Réessayer</Button>}
    </div>
  )
}

/** Tableau responsive : défilement horizontal sur petit écran. */
export function Table({ head, children }: { head: ReactNode[]; children: ReactNode }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[720px] text-left text-sm">
        <thead className="border-b border-line bg-surface-2 text-xs uppercase tracking-wide text-muted">
          <tr>{head.map((h, i) => <th key={i} className="px-4 py-3 font-medium">{h}</th>)}</tr>
        </thead>
        <tbody className="divide-y divide-line">{children}</tbody>
      </table>
    </div>
  )
}

export function Pagination({ skip, take, total, onChange }: { skip: number; take: number; total: number; onChange: (skip: number) => void }) {
  if (total <= take) return null
  const page = Math.floor(skip / take) + 1
  const pages = Math.ceil(total / take)
  return (
    <div className="flex items-center justify-between border-t border-line px-4 py-3 text-sm text-muted">
      <span>{skip + 1}–{Math.min(skip + take, total)} sur {total}</span>
      <div className="flex gap-2">
        <Button disabled={page <= 1} onClick={() => onChange(skip - take)}>Précédent</Button>
        <Button disabled={page >= pages} onClick={() => onChange(skip + take)}>Suivant</Button>
      </div>
    </div>
  )
}

export function Modal({ open, title, onClose, children, footer, wide }: {
  open: boolean; title: string; onClose: () => void; children: ReactNode; footer?: ReactNode; wide?: boolean
}) {
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])
  if (!open) return null
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 p-0 sm:items-center sm:p-4" onMouseDown={onClose}>
      <div role="dialog" aria-modal="true" aria-label={title} onMouseDown={(e) => e.stopPropagation()}
        className={cx('max-h-[90vh] w-full overflow-hidden rounded-t-2xl bg-surface shadow-xl sm:rounded-2xl', wide ? 'sm:max-w-3xl' : 'sm:max-w-lg')}>
        <div className="flex items-center justify-between border-b border-line px-5 py-4">
          <h2 className="font-semibold">{title}</h2>
          <button aria-label="Fermer" onClick={onClose} className="rounded-md p-1 text-muted hover:bg-surface-2 hover:text-fg"><X className="size-5" /></button>
        </div>
        <div className="max-h-[65vh] overflow-y-auto px-5 py-4">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-line px-5 py-3">{footer}</div>}
      </div>
    </div>
  )
}

/** Confirmation d'action sensible, avec motif facultatif ou obligatoire. */
export function ConfirmDialog({ open, title, message, confirmLabel, danger, reason, onConfirm, onClose, busy }: {
  open: boolean; title: string; message: string; confirmLabel: string; danger?: boolean
  reason?: { label: string; required?: boolean; placeholder?: string }
  onConfirm: (reason: string) => void; onClose: () => void; busy?: boolean
}) {
  const [text, setText] = useState('')
  useEffect(() => { if (open) setText('') }, [open])
  const invalid = !!reason?.required && text.trim().length < 5
  return (
    <Modal open={open} title={title} onClose={onClose} footer={
      <>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant={danger ? 'danger' : 'primary'} loading={busy} disabled={invalid} onClick={() => onConfirm(text.trim())}>{confirmLabel}</Button>
      </>
    }>
      <p className="text-sm text-muted">{message}</p>
      {reason && (
        <div className="mt-4">
          <Textarea label={reason.label} placeholder={reason.placeholder} value={text} onChange={(e) => setText(e.target.value)} />
          {reason.required && <p className="mt-1 text-xs text-muted">5 caractères minimum, visible dans le journal d'audit.</p>}
        </div>
      )}
    </Modal>
  )
}

export function Stat({ label, value, hint, tone }: { label: string; value: ReactNode; hint?: ReactNode; tone?: 'danger' | 'warning' }) {
  return (
    <Card className="p-4">
      <p className="text-sm text-muted">{label}</p>
      <p className={cx('mt-1 text-2xl font-semibold tracking-tight', tone === 'danger' && 'text-danger', tone === 'warning' && 'text-warning')}>{value}</p>
      {hint && <p className="mt-1 text-xs text-muted">{hint}</p>}
    </Card>
  )
}
