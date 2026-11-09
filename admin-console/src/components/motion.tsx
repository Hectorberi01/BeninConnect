import {
  animate, motion, useInView, useMotionTemplate, useMotionValue, useSpring, useTransform, type Variants,
} from 'motion/react'
import { type ReactNode, useEffect, useId, useRef } from 'react'
import { fmtNumber } from '@/lib/format'

const ease = [0.22, 1, 0.36, 1] as const

/** Élément révélé par un conteneur <Stagger> (apparition décalée). */
export const revealItem: Variants = {
  hidden: { opacity: 0, y: 16, scale: 0.98 },
  show: { opacity: 1, y: 0, scale: 1, transition: { duration: 0.45, ease } },
}

/** Fait apparaître ses enfants l'un après l'autre (Card, Stat). */
export function Stagger({ children, className, delay = 0 }: { children: ReactNode; className?: string; delay?: number }) {
  return (
    <motion.div className={className} initial="hidden" animate="show"
      variants={{ hidden: {}, show: { transition: { staggerChildren: 0.06, delayChildren: delay } } }}>
      {children}
    </motion.div>
  )
}

/** Inclinaison 3D qui suit le pointeur, avec reflet lumineux. */
export function Tilt({ children, className, max = 7 }: { children: ReactNode; className?: string; max?: number }) {
  const ref = useRef<HTMLDivElement>(null)
  const x = useMotionValue(0.5)
  const y = useMotionValue(0.5)
  const spring = { stiffness: 220, damping: 22, mass: 0.6 }
  const rotateX = useSpring(useTransform(y, [0, 1], [max, -max]), spring)
  const rotateY = useSpring(useTransform(x, [0, 1], [-max, max]), spring)
  const gx = useTransform(x, (v) => `${v * 100}%`)
  const gy = useTransform(y, (v) => `${v * 100}%`)
  const glare = useMotionTemplate`radial-gradient(circle at ${gx} ${gy}, rgba(255,255,255,0.22), transparent 55%)`
  const glow = useMotionValue(0)

  return (
    <motion.div variants={revealItem} className={className} style={{ perspective: 900 }}>
      <motion.div ref={ref} className="relative h-full rounded-xl"
        style={{ rotateX, rotateY, transformStyle: 'preserve-3d' }}
        whileHover={{ scale: 1.025, z: 20 }} transition={{ type: 'spring', stiffness: 260, damping: 20 }}
        onPointerMove={(e) => {
          const r = ref.current?.getBoundingClientRect()
          if (!r || e.pointerType === 'touch') return
          x.set((e.clientX - r.left) / r.width)
          y.set((e.clientY - r.top) / r.height)
          glow.set(1)
        }}
        onPointerLeave={() => { x.set(0.5); y.set(0.5); glow.set(0) }}>
        {children}
        <motion.div aria-hidden className="pointer-events-none absolute inset-0 rounded-xl mix-blend-soft-light"
          style={{ background: glare, opacity: useSpring(glow, { stiffness: 120, damping: 20 }) }} />
      </motion.div>
    </motion.div>
  )
}

/** Nombre qui défile jusqu'à sa valeur quand il devient visible. */
export function AnimatedNumber({ value, suffix = '' }: { value: number; suffix?: string }) {
  const ref = useRef<HTMLSpanElement>(null)
  const inView = useInView(ref, { once: true })
  const mv = useMotionValue(0)
  const text = useTransform(mv, (v) => `${fmtNumber(Math.round(v))}${suffix}`)
  useEffect(() => {
    if (!inView) return
    const controls = animate(mv, value, { duration: 1.1, ease })
    return () => controls.stop()
  }, [inView, value, mv])
  return <motion.span ref={ref}>{text}</motion.span>
}

/** Courbe miniature tracée à l'apparition. */
export function Sparkline({ data, className }: { data: number[]; className?: string }) {
  const id = useId()
  if (data.length < 2) return null
  const w = 120
  const h = 32
  const max = Math.max(1, ...data)
  const pts = data.map((v, i) => [(i / (data.length - 1)) * w, h - 2 - (v / max) * (h - 4)] as const)
  const line = pts.map(([px, py], i) => `${i ? 'L' : 'M'}${px.toFixed(1)},${py.toFixed(1)}`).join(' ')
  const area = `${line} L${w},${h} L0,${h} Z`
  return (
    <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" className={className} aria-hidden>
      <defs>
        <linearGradient id={id} x1="0" x2="0" y1="0" y2="1">
          <stop offset="0%" stopColor="currentColor" stopOpacity="0.28" />
          <stop offset="100%" stopColor="currentColor" stopOpacity="0" />
        </linearGradient>
      </defs>
      <motion.path d={area} fill={`url(#${id})`} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: 0.5, duration: 0.6 }} />
      <motion.path d={line} fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round"
        initial={{ pathLength: 0 }} animate={{ pathLength: 1 }} transition={{ duration: 1.2, ease }} />
    </svg>
  )
}

/** Anneau de progression (0 à 1). */
export function ProgressRing({ value, size = 52, stroke = 6, className }: { value: number; size?: number; stroke?: number; className?: string }) {
  const r = (size - stroke) / 2
  const c = 2 * Math.PI * r
  return (
    <svg width={size} height={size} className={className} aria-hidden>
      <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--line)" strokeWidth={stroke} />
      <motion.circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="currentColor" strokeWidth={stroke} strokeLinecap="round"
        strokeDasharray={c} initial={{ strokeDashoffset: c }} animate={{ strokeDashoffset: c * (1 - Math.min(1, Math.max(0, value))) }}
        transition={{ duration: 1.2, ease, delay: 0.2 }} transform={`rotate(-90 ${size / 2} ${size / 2})`} />
    </svg>
  )
}

/** Cube 3D décoratif en rotation lente. */
export function Cube({ size = 64, className, duration = 18 }: { size?: number; className?: string; duration?: number }) {
  const half = size / 2
  const faces = [
    `rotateY(0deg) translateZ(${half}px)`, `rotateY(90deg) translateZ(${half}px)`, `rotateY(180deg) translateZ(${half}px)`,
    `rotateY(-90deg) translateZ(${half}px)`, `rotateX(90deg) translateZ(${half}px)`, `rotateX(-90deg) translateZ(${half}px)`,
  ]
  return (
    <div aria-hidden className={className} style={{ width: size, height: size, perspective: 600 }}>
      <motion.div className="relative size-full" style={{ transformStyle: 'preserve-3d' }}
        animate={{ rotateX: [20, 380], rotateY: [-30, 330] }} transition={{ duration, repeat: Infinity, ease: 'linear' }}>
        {faces.map((t) => (
          <div key={t} className="absolute inset-0 rounded-lg border border-white/35 bg-white/10 backdrop-blur-[2px]" style={{ transform: t }} />
        ))}
      </motion.div>
    </div>
  )
}

/** Objet flottant (montée et descente douce). */
export function Float({ children, className, delay = 0, distance = 10 }: { children: ReactNode; className?: string; delay?: number; distance?: number }) {
  return (
    <motion.div aria-hidden className={className} animate={{ y: [0, -distance, 0] }}
      transition={{ duration: 6, repeat: Infinity, ease: 'easeInOut', delay }}>
      {children}
    </motion.div>
  )
}
