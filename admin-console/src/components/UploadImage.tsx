import { type ImgHTMLAttributes, useEffect, useState } from 'react'
import { apiUrl } from '@/lib/api'

/** Photo envoyée par un iPhone au format HEIC : lisible par Safari seulement, convertie en JPEG pour les autres navigateurs. */
const isHeic = (path: string) => /\.(heic|heif)$/i.test(path)
const converted = new Map<string, Promise<string>>()

function toJpegUrl(url: string): Promise<string> {
  let pending = converted.get(url)
  if (!pending) {
    pending = (async () => {
      const response = await fetch(`${url}?relay=1`) // relayée par l'API : pas de redirection vers le stockage externe
      if (!response.ok) throw new Error(String(response.status))
      const blob = await response.blob()
      const { default: heic2any } = await import('heic2any') // bibliothèque chargée seulement si besoin
      const result = await heic2any({ blob, toType: 'image/jpeg', quality: 0.8 })
      return URL.createObjectURL(Array.isArray(result) ? result[0] : result)
    })()
    pending.catch(() => converted.delete(url))
    converted.set(url, pending)
  }
  return pending
}

/** Image publique (uploads/…) servie par l'API ; HEIC converti au besoin. */
export function UploadImage({ path, ...img }: { path: string } & Omit<ImgHTMLAttributes<HTMLImageElement>, 'src'>) {
  const url = apiUrl(path)
  const native = !isHeic(path) || /^((?!chrome|android).)*safari/i.test(navigator.userAgent)
  const [src, setSrc] = useState<string | null>(native ? url : null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    if (native) return
    let alive = true
    toJpegUrl(url).then((s) => alive && setSrc(s)).catch(() => alive && setFailed(true))
    return () => { alive = false }
  }, [url, native])

  if (!src) {
    return (
      <span className={`${img.className ?? ''} grid place-items-center bg-surface-2 text-[10px] text-muted`} style={img.style} title={failed ? 'Photo HEIC illisible' : 'Conversion…'}>
        {failed ? 'HEIC' : '…'}
      </span>
    )
  }
  return <img src={src} alt={img.alt ?? ''} {...img} />
}
