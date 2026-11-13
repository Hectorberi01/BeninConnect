import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '@/components/toast'
import { Badge, Button, Card, ErrorBox, Input, Loading, Modal, PageHeader, Select, Textarea } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import type { ServiceCategory, ServicesOverview, ServicesSettings } from '@/lib/types'

type Form = { id: string | null; slug: string; name: string; icon: string; parentId: string; sortOrder: number; isActive: boolean; requiresQualification: boolean }

const empty: Form = { id: null, slug: '', name: '', icon: 'briefcase-outline', parentId: '', sortOrder: 0, isActive: true, requiresQualification: false }

export function ServiceCategoriesPage() {
  const categories = useQuery({ queryKey: ['services-categories'], queryFn: () => api<ServiceCategory[]>('api/admin/services/categories') })
  const overview = useQuery({ queryKey: ['services'], queryFn: () => api<ServicesOverview>('api/admin/services') })
  const [form, setForm] = useState<Form | null>(null)

  const edit = (c: ServiceCategory) => setForm({
    id: c.id, slug: c.slug, name: c.name, icon: c.icon, parentId: c.parentId ?? '', sortOrder: c.sortOrder, isActive: c.isActive,
    requiresQualification: c.requiresQualification,
  })

  return (
    <>
      <PageHeader title="Catégories et réglages" description="Référentiel des services proposés dans l'application et règles de modération automatique."
        actions={<Button variant="primary" icon={<Plus className="size-4" />} onClick={() => setForm(empty)}>Nouvelle catégorie</Button>} />
      <div className="grid gap-4 xl:grid-cols-3">
        <Card className="xl:col-span-2">
          {categories.isPending ? <Loading /> : categories.error ? <ErrorBox message={errorMessage(categories.error)} /> : (
            <ul className="divide-y divide-line">
              {categories.data.map((parent) => (
                <li key={parent.id} className="px-5 py-3">
                  <Row c={parent} onEdit={edit} strong />
                  <ul className="mt-1 space-y-1 border-l border-line pl-4">
                    {parent.children.map((c) => <li key={c.id}><Row c={c} onEdit={edit} /></li>)}
                  </ul>
                </li>
              ))}
            </ul>
          )}
        </Card>
        {overview.data && <SettingsCard settings={overview.data.settings} />}
      </div>
      {form && categories.data && <CategoryModal form={form} parents={categories.data} onClose={() => setForm(null)} />}
    </>
  )
}

function Row({ c, onEdit, strong }: { c: ServiceCategory; onEdit: (c: ServiceCategory) => void; strong?: boolean }) {
  return (
    <div className="flex flex-wrap items-center gap-2 py-1 text-sm">
      <span className={strong ? 'font-semibold' : ''}>{c.name}</span>
      <span className="text-xs text-muted">{c.slug} · ordre {c.sortOrder} · {c.icon}</span>
      {c.requiresQualification && <Badge tone="warning">Métier réglementé</Badge>}
      {!c.isActive && <Badge tone="neutral">Désactivée</Badge>}
      <span className="ml-auto text-xs text-muted">{c.activeProviders} prestataire(s)</span>
      <Button variant="ghost" aria-label={`Modifier ${c.name}`} icon={<Pencil className="size-4" />} onClick={() => onEdit(c)} />
    </div>
  )
}

function CategoryModal({ form: initial, parents, onClose }: { form: Form; parents: ServiceCategory[]; onClose: () => void }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [f, setF] = useState(initial)
  const set = <K extends keyof Form>(key: K, value: Form[K]) => setF((old) => ({ ...old, [key]: value }))
  const save = useMutation({
    mutationFn: () => api<ServiceCategory>(f.id ? `api/admin/services/categories/${f.id}` : 'api/admin/services/categories', {
      method: f.id ? 'PUT' : 'POST', body: { ...f, parentId: f.parentId || null },
    }),
    onSuccess: () => {
      toast('Catégorie enregistrée.', 'success')
      qc.invalidateQueries({ queryKey: ['services-categories'] })
      onClose()
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Modal open title={f.id ? `Modifier « ${initial.name} »` : 'Nouvelle catégorie'} onClose={onClose}
      footer={<><Button variant="ghost" onClick={onClose}>Annuler</Button><Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>Enregistrer</Button></>}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Input label="Nom" value={f.name} onChange={(e) => set('name', e.target.value)} />
        <Input label="Identifiant (slug)" value={f.slug} onChange={(e) => set('slug', e.target.value)} placeholder="coiffure" />
        <Select label="Catégorie parente" value={f.parentId} onChange={(e) => set('parentId', e.target.value)}>
          <option value="">Aucune (catégorie principale)</option>
          {parents.filter((p) => p.id !== f.id).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </Select>
        <Input label="Ordre d'affichage" type="number" value={f.sortOrder} onChange={(e) => set('sortOrder', Number(e.target.value))} />
        <Input label="Icône (MaterialCommunityIcons)" value={f.icon} onChange={(e) => set('icon', e.target.value)} className="sm:col-span-2" />
      </div>
      <div className="mt-4 space-y-2 text-sm">
        <label className="flex items-center gap-2"><input type="checkbox" className="size-4 accent-[var(--brand)]" checked={f.isActive}
          onChange={(e) => set('isActive', e.target.checked)} />Active (proposée aux prestataires et visible dans l'application)</label>
        <label className="flex items-start gap-2"><input type="checkbox" className="mt-0.5 size-4 accent-[var(--brand)]" checked={f.requiresQualification}
          onChange={(e) => set('requiresQualification', e.target.checked)} />
          <span>Métier réglementé : déclaration sur l'honneur obligatoire, justificatif pour le badge « Pro vérifié », avertissement affiché aux membres.</span></label>
      </div>
      <p className="mt-3 text-xs text-muted">Une catégorie n'est jamais supprimée : désactivez-la. Les offres existantes restent rattachées.</p>
    </Modal>
  )
}

function SettingsCard({ settings }: { settings: ServicesSettings }) {
  const toast = useToast()
  const qc = useQueryClient()
  const [keywords, setKeywords] = useState(settings.alertKeywords.join('\n'))
  const [threshold, setThreshold] = useState(settings.reportThreshold)
  const [limit, setLimit] = useState(settings.dailyRequestLimit)
  const save = useMutation({
    mutationFn: () => api<ServicesSettings>('api/admin/services/settings', {
      method: 'PUT', body: { alertKeywords: keywords.split('\n').map((k) => k.trim()).filter(Boolean), reportThreshold: threshold, dailyRequestLimit: limit },
    }),
    onSuccess: () => { toast('Réglages enregistrés.', 'success'); qc.invalidateQueries({ queryKey: ['services'] }) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <Card className="space-y-4 p-5">
      <h2 className="font-semibold">Modération automatique</h2>
      <Textarea label="Mots-clés d'alerte (un par ligne)" rows={10} value={keywords} onChange={(e) => setKeywords(e.target.value)} />
      <p className="-mt-2 text-xs text-muted">Une offre (ou la présentation d'un profil publié) qui en contient un est masquée et placée dans la file de modération.</p>
      <Input label="Signalements avant masquage automatique" type="number" min={1} max={50} value={threshold} onChange={(e) => setThreshold(Number(e.target.value))} />
      <Input label="Demandes par jour et par membre" type="number" min={1} max={100} value={limit} onChange={(e) => setLimit(Number(e.target.value))} />
      <Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>Enregistrer</Button>
    </Card>
  )
}
