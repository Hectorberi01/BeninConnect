import { CalendarDays, Download, LineChart, Users, UsersRound } from 'lucide-react'
import { type ComponentType, useState } from 'react'
import { useToast } from '@/components/toast'
import { Button, Card, PageHeader } from '@/components/ui'
import { download, errorMessage } from '@/lib/api'

const EXPORTS: { kind: string; title: string; description: string; icon: ComponentType<{ className?: string }> }[] = [
  { kind: 'members', title: 'Membres', description: 'Identité, localisation, date d’inscription, statut, publications et communautés.', icon: Users },
  { kind: 'communities', title: 'Communautés', description: 'Catégorie, lieu, visibilité, nombre de membres, publications et événements.', icon: UsersRound },
  { kind: 'events', title: 'Événements', description: 'Dates, lieu, capacité, participants et taux de remplissage.', icon: CalendarDays },
  { kind: 'activity', title: 'Activité quotidienne', description: 'Inscriptions, publications, commentaires et messages par jour (90 jours).', icon: LineChart },
]

export function ExportsPage() {
  const toast = useToast()
  const [busy, setBusy] = useState<string | null>(null)

  const run = async (kind: string) => {
    setBusy(kind)
    try {
      const name = await download(`api/admin/exports/${kind}`, `beninconnect-${kind}.csv`)
      toast(`${name} téléchargé.`)
    } catch (e) {
      toast(errorMessage(e), 'error')
    } finally {
      setBusy(null)
    }
  }

  return (
    <>
      <PageHeader title="Exports" description="Fichiers CSV (séparateur « ; », UTF-8) prêts pour Excel ou LibreOffice. Chaque export est tracé dans le journal d'audit." />
      <div className="grid gap-4 md:grid-cols-2">
        {EXPORTS.map(({ kind, title, description, icon: Icon }) => (
          <Card key={kind} className="flex items-start gap-4 p-5">
            <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-brand-soft text-brand"><Icon className="size-5" /></span>
            <div className="min-w-0 flex-1">
              <h2 className="font-semibold">{title}</h2>
              <p className="mt-1 text-sm text-muted">{description}</p>
              <Button className="mt-4" variant="primary" loading={busy === kind} disabled={!!busy && busy !== kind}
                icon={<Download className="size-4" />} onClick={() => run(kind)}>Télécharger le CSV</Button>
            </div>
          </Card>
        ))}
      </div>
      <p className="mt-6 text-sm text-muted">L'application ne gère pas encore de paiements : ces exports couvrent l'activité de la plateforme, pas de données financières.</p>
    </>
  )
}
