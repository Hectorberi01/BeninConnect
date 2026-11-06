import { useMutation } from '@tanstack/react-query'
import { type FormEvent, useState } from 'react'
import { useAuth } from '@/auth'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, Input, PageHeader } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { ROLE_DESCRIPTIONS, ROLE_LABELS } from '@/lib/permissions'

export function AccountPage() {
  const { me } = useAuth()
  const toast = useToast()
  const [form, setForm] = useState({ currentPassword: '', newPassword: '', confirm: '' })
  const [error, setError] = useState<string | null>(null)
  const change = useMutation({
    mutationFn: () => api('api/admin/auth/password', { method: 'POST', body: { currentPassword: form.currentPassword, newPassword: form.newPassword } }),
    onSuccess: () => {
      toast('Mot de passe modifié.')
      setForm({ currentPassword: '', newPassword: '', confirm: '' })
    },
    onError: (e) => setError(errorMessage(e)),
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setError(form.newPassword !== form.confirm ? 'Les deux mots de passe ne correspondent pas.' : null)
    if (form.newPassword === form.confirm) change.mutate()
  }

  return (
    <>
      <PageHeader title="Mon compte" />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="p-5">
          <p className="text-lg font-semibold">{me?.displayName}</p>
          <p className="text-sm text-muted">{me?.email}</p>
          {me && (
            <>
              <div className="mt-3"><Badge tone="brand">{ROLE_LABELS[me.role]}</Badge></div>
              <p className="mt-2 text-sm text-muted">{ROLE_DESCRIPTIONS[me.role]}</p>
            </>
          )}
        </Card>
        <Card className="p-5">
          <h2 className="mb-4 font-semibold">Changer de mot de passe</h2>
          <form onSubmit={submit} className="space-y-4">
            {error && <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{error}</p>}
            <Input label="Mot de passe actuel" type="password" autoComplete="current-password" required value={form.currentPassword} onChange={(e) => setForm({ ...form, currentPassword: e.target.value })} />
            <Input label="Nouveau mot de passe" type="password" autoComplete="new-password" required value={form.newPassword} onChange={(e) => setForm({ ...form, newPassword: e.target.value })} />
            <Input label="Confirmer" type="password" autoComplete="new-password" required value={form.confirm} onChange={(e) => setForm({ ...form, confirm: e.target.value })} />
            <p className="text-xs text-muted">10 caractères minimum, avec lettres et chiffres.</p>
            <Button type="submit" variant="primary" loading={change.isPending}>Enregistrer</Button>
          </form>
        </Card>
      </div>
    </>
  )
}
