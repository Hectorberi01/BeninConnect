import { Link } from 'react-router-dom'
import { Card } from '@/components/ui'

export function ForbiddenPage() {
  return (
    <Card className="mx-auto max-w-lg p-8 text-center">
      <h1 className="text-xl font-semibold">Accès refusé</h1>
      <p className="mt-2 text-sm text-muted">Votre rôle ne donne pas accès à cette page. Demandez à un super administrateur si vous en avez besoin.</p>
      <Link to="/" className="mt-6 inline-block text-sm font-medium text-brand hover:underline">Retour au tableau de bord</Link>
    </Card>
  )
}

export function NotFoundPage() {
  return (
    <Card className="mx-auto max-w-lg p-8 text-center">
      <h1 className="text-xl font-semibold">Page introuvable</h1>
      <Link to="/" className="mt-6 inline-block text-sm font-medium text-brand hover:underline">Retour au tableau de bord</Link>
    </Card>
  )
}
