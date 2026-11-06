const dateTime = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
const dateOnly = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short', year: 'numeric' })
const dayShort = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short' })
const number = new Intl.NumberFormat('fr-FR')

export const fmtDateTime = (iso: string | null | undefined) => (iso ? dateTime.format(new Date(iso)) : '—')
export const fmtDate = (iso: string | null | undefined) => (iso ? dateOnly.format(new Date(iso)) : '—')
export const fmtDay = (day: string) => dayShort.format(new Date(`${day}T00:00:00`))
export const fmtNumber = (n: number) => number.format(n)

export function fmtBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} o`
  const units = ['Ko', 'Mo', 'Go']
  let value = bytes / 1024
  let i = 0
  while (value >= 1024 && i < units.length - 1) {
    value /= 1024
    i++
  }
  return `${value.toLocaleString('fr-FR', { maximumFractionDigits: 1 })} ${units[i]}`
}

export function fmtRelative(iso: string, now = new Date()) {
  const minutes = Math.round((now.getTime() - new Date(iso).getTime()) / 60_000)
  if (minutes < 1) return "à l'instant"
  if (minutes < 60) return `il y a ${minutes} min`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `il y a ${hours} h`
  const days = Math.round(hours / 24)
  if (days < 30) return `il y a ${days} j`
  return fmtDate(iso)
}

export const initials = (first: string, last: string) => `${first[0] ?? ''}${last[0] ?? ''}`.toUpperCase()
