import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Pencil, UserPlus } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useAuth } from '@/auth'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ErrorBox, Input, Loading, Modal, PageHeader, Select, Table } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmtDateTime } from '@/lib/format'
import { ROLE_DESCRIPTIONS, ROLE_LABELS, ROLES } from '@/lib/permissions'
import type { AdminAccount, AdminRole } from '@/lib/types'

type Dialog = { kind: 'create' } | { kind: 'edit'; account: AdminAccount } | { kind: 'password'; account: AdminAccount } | null

export function AdminsPage() {
  const { me } = useAuth()
  const toast = useToast()
  const qc = useQueryClient()
  const [dialog, setDialog] = useState<Dialog>(null)
  const list = useQuery({ queryKey: ['admins'], queryFn: () => api<AdminAccount[]>('api/admin/admins') })
  const done = (message: string) => {
    toast(message)
    setDialog(null)
    qc.invalidateQueries({ queryKey: ['admins'] })
  }

  return (
    <>
      <PageHeader title="Administrateurs" description="Comptes de la console, distincts des membres de l'application."
        actions={<Button variant="primary" icon={<UserPlus className="size-4" />} onClick={() => setDialog({ kind: 'create' })}>Nouveau compte</Button>} />

      <div className="mb-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        {ROLES.map((r) => (
          <Card key={r} className="p-4">
            <Badge tone="brand">{ROLE_LABELS[r]}</Badge>
            <p className="mt-2 text-xs text-muted">{ROLE_DESCRIPTIONS[r]}</p>
          </Card>
        ))}
      </div>

      <Card>
        {list.isPending ? <Loading /> : list.error ? <ErrorBox message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : (
          <Table head={['Compte', 'Rôle', 'État', 'Dernière connexion', '']}>
            {list.data.map((a) => (
              <tr key={a.id}>
                <td className="px-4 py-3">
                  <p className="font-medium">{a.displayName}{a.id === me?.id && <span className="ml-2 text-xs text-muted">(vous)</span>}</p>
                  <p className="text-xs text-muted">{a.email}</p>
                </td>
                <td className="px-4 py-3"><Badge tone="brand">{ROLE_LABELS[a.role]}</Badge></td>
                <td className="px-4 py-3">{a.isActive ? <Badge tone="brand">Actif</Badge> : <Badge tone="danger">Désactivé</Badge>}</td>
                <td className="px-4 py-3 text-muted">{a.lastLoginAt ? fmtDateTime(a.lastLoginAt) : 'Jamais'}</td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-1">
                    <Button variant="ghost" aria-label="Modifier" title="Modifier" onClick={() => setDialog({ kind: 'edit', account: a })} icon={<Pencil className="size-4" />} />
                    <Button variant="ghost" aria-label="Réinitialiser le mot de passe" title="Réinitialiser le mot de passe" onClick={() => setDialog({ kind: 'password', account: a })} icon={<KeyRound className="size-4" />} />
                  </div>
                </td>
              </tr>
            ))}
          </Table>
        )}
      </Card>

      {dialog?.kind === 'create' && <CreateDialog onClose={() => setDialog(null)} onDone={() => done('Compte créé : transmettez le mot de passe temporaire de façon sûre.')} />}
      {dialog?.kind === 'edit' && <EditDialog account={dialog.account} self={dialog.account.id === me?.id} onClose={() => setDialog(null)} onDone={() => done('Compte mis à jour.')} />}
      {dialog?.kind === 'password' && <PasswordDialog account={dialog.account} onClose={() => setDialog(null)} onDone={() => done('Mot de passe réinitialisé.')} />}
    </>
  )
}

function RoleSelect({ value, onChange, disabled }: { value: AdminRole; onChange: (r: AdminRole) => void; disabled?: boolean }) {
  return (
    <div>
      <Select label="Rôle" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value as AdminRole)}>
        {ROLES.map((r) => <option key={r} value={r}>{ROLE_LABELS[r]}</option>)}
      </Select>
      <p className="mt-1 text-xs text-muted">{ROLE_DESCRIPTIONS[value]}</p>
    </div>
  )
}

function useSubmit<T>(fn: (body: T) => Promise<unknown>, onDone: () => void) {
  const [error, setError] = useState<string | null>(null)
  const m = useMutation({ mutationFn: fn, onSuccess: onDone, onError: (e) => setError(errorMessage(e)) })
  return { error, busy: m.isPending, submit: (body: T) => { setError(null); m.mutate(body) } }
}

const PASSWORD_HINT = '10 caractères minimum, avec lettres et chiffres.'

function CreateDialog({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const [form, setForm] = useState({ email: '', displayName: '', role: 'Moderator' as AdminRole, password: '' })
  const s = useSubmit((b: typeof form) => api('api/admin/admins', { method: 'POST', body: b }), onDone)
  const onSubmit = (e: FormEvent) => { e.preventDefault(); s.submit(form) }
  return (
    <Modal open title="Nouveau compte administrateur" onClose={onClose} footer={<><Button onClick={onClose}>Annuler</Button><Button variant="primary" loading={s.busy} type="submit" form="create-admin">Créer</Button></>}>
      <form id="create-admin" onSubmit={onSubmit} className="space-y-4">
        {s.error && <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{s.error}</p>}
        <Input label="Nom affiché" required value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
        <Input label="Email" type="email" required value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
        <RoleSelect value={form.role} onChange={(role) => setForm({ ...form, role })} />
        <div>
          <Input label="Mot de passe temporaire" type="text" autoComplete="new-password" required value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
          <p className="mt-1 text-xs text-muted">{PASSWORD_HINT} La personne pourra le changer depuis « Mon mot de passe ».</p>
        </div>
      </form>
    </Modal>
  )
}

function EditDialog({ account, self, onClose, onDone }: { account: AdminAccount; self: boolean; onClose: () => void; onDone: () => void }) {
  const [form, setForm] = useState({ displayName: account.displayName, role: account.role, isActive: account.isActive })
  const s = useSubmit((b: typeof form) => api(`api/admin/admins/${account.id}`, { method: 'PUT', body: b }), onDone)
  return (
    <Modal open title={`Modifier ${account.displayName}`} onClose={onClose} footer={<><Button onClick={onClose}>Annuler</Button><Button variant="primary" loading={s.busy} onClick={() => s.submit(form)}>Enregistrer</Button></>}>
      <div className="space-y-4">
        {s.error && <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{s.error}</p>}
        <Input label="Nom affiché" value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
        <RoleSelect value={form.role} disabled={self} onChange={(role) => setForm({ ...form, role })} />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" disabled={self} checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} className="size-4 accent-[var(--brand)]" />
          Compte actif <span className="text-muted">(désactiver ferme immédiatement ses sessions)</span>
        </label>
        {self && <p className="text-xs text-muted">Vous ne pouvez modifier ni votre propre rôle ni l'état de votre compte.</p>}
      </div>
    </Modal>
  )
}

function PasswordDialog({ account, onClose, onDone }: { account: AdminAccount; onClose: () => void; onDone: () => void }) {
  const [password, setPassword] = useState('')
  const s = useSubmit((b: { password: string }) => api(`api/admin/admins/${account.id}/password`, { method: 'POST', body: b }), onDone)
  return (
    <Modal open title={`Nouveau mot de passe · ${account.displayName}`} onClose={onClose} footer={<><Button onClick={onClose}>Annuler</Button><Button variant="primary" loading={s.busy} onClick={() => s.submit({ password })}>Réinitialiser</Button></>}>
      {s.error && <p role="alert" className="mb-4 rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{s.error}</p>}
      <Input label="Mot de passe temporaire" type="text" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      <p className="mt-1 text-xs text-muted">{PASSWORD_HINT}</p>
    </Modal>
  )
}
