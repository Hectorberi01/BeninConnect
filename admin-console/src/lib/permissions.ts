import type { AdminRole, Permission, ReportDecision, ReportReason, ReportTarget } from './types'

export const ROLE_LABELS: Record<AdminRole, string> = {
  SuperAdmin: 'Super administrateur',
  Admin: 'Administrateur',
  Moderator: 'Modérateur',
  Accountant: 'Comptable',
  Developer: 'Développeur',
}

export const ROLE_DESCRIPTIONS: Record<AdminRole, string> = {
  SuperAdmin: 'Tous les droits, dont la gestion des comptes administrateurs.',
  Admin: 'Membres, contenus, signalements, communautés, événements, emploi, exports et journal.',
  Moderator: 'Signalements, contenus (masquer, supprimer), offres d’emploi et suspension des membres.',
  Accountant: 'Tableau de bord et exports CSV, en lecture seule.',
  Developer: "État de l'API, sauvegardes de la base, journal d'audit et outils de développement.",
}

export const ROLES = Object.keys(ROLE_LABELS) as AdminRole[]

export const has = (permissions: readonly Permission[] | undefined, p: Permission) => !!permissions?.includes(p)

export const REASON_LABELS: Record<ReportReason, string> = {
  Spam: 'Spam',
  Harassment: 'Harcèlement',
  Inappropriate: 'Contenu inapproprié',
  FalseInformation: 'Fausse information',
  Other: 'Autre',
}

export const TARGET_LABELS: Record<ReportTarget, string> = {
  Post: 'Publication',
  Comment: 'Commentaire',
  User: 'Membre',
  Community: 'Communauté',
}

/** Décisions possibles selon la cible (mêmes règles que l'API). */
export function decisionsFor(target: ReportTarget, canManageCommunities: boolean): { value: ReportDecision; label: string; danger?: boolean }[] {
  const dismiss = { value: 'dismiss' as const, label: 'Classer sans suite' }
  switch (target) {
    case 'Post':
      return [dismiss, { value: 'hide', label: 'Masquer la publication' }, { value: 'delete', label: 'Supprimer la publication', danger: true },
        { value: 'suspend', label: "Suspendre l'auteur", danger: true }]
    case 'Comment':
      return [dismiss, { value: 'delete', label: 'Supprimer le commentaire', danger: true }, { value: 'suspend', label: "Suspendre l'auteur", danger: true }]
    case 'User':
      return [dismiss, { value: 'suspend', label: 'Suspendre le membre', danger: true }]
    default:
      return canManageCommunities ? [dismiss, { value: 'delete', label: 'Supprimer la communauté', danger: true }] : [dismiss]
  }
}

export const REPORT_STATUS_LABELS: Record<'Open' | 'Resolved' | 'Dismissed', string> = {
  Open: 'À traiter',
  Resolved: 'Traité',
  Dismissed: 'Classé',
}

export const COMMUNITY_ROLE_LABELS: Record<'Member' | 'Moderator' | 'Owner', string> = {
  Member: 'Membre',
  Moderator: 'Modérateur',
  Owner: 'Propriétaire',
}
