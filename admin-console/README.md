# BéninConnect – console d'administration

Console web (React 19 + TypeScript + Vite) réservée à l'équipe BéninConnect. Les comptes administrateurs sont **distincts des membres** de l'application : table `admin_users`, connexion `/api/admin/auth/login`, jetons à audience `beninconnect-admin` refusés par l'API membres (et inversement).

## Lancer

```bash
cd admin-console
npm install
npm run dev            # http://localhost:5173
```

L'API doit tourner (`server/`, port 5080). En développement, Vite relaie `/api` et `/uploads` vers `http://localhost:5080` : pas de CORS à configurer. Autre adresse (ngrok…) :

```bash
API_PROXY_TARGET=https://xxxx.ngrok-free.dev npm run dev
```

Comptes de démonstration (mot de passe `Admin2026!`, affichés sur l'écran de connexion en développement) :

| Rôle | Email |
|---|---|
| SuperAdmin | superadmin@beninconnect.test |
| Admin | admin@beninconnect.test |
| Modérateur | moderation@beninconnect.test |
| Comptable | compta@beninconnect.test |
| Développeur | dev@beninconnect.test |

## Rôles et droits

Matrice définie côté API (`Application/Features/Admin/Permissions.cs`) ; la console reçoit la liste des droits dans `/api/admin/auth/me` et adapte menus et routes. L'API reste la seule autorité : chaque route exige sa permission.

| Écran | SuperAdmin | Admin | Modérateur | Comptable | Dev |
|---|:-:|:-:|:-:|:-:|:-:|
| Tableau de bord | ✓ | ✓ | ✓ | ✓ | ✓ |
| Signalements | ✓ | ✓ | ✓ | | |
| Membres (consulter, suspendre) | ✓ | ✓ | ✓ | | |
| Publications (masquer, supprimer) | ✓ | ✓ | ✓ | | |
| Communautés, événements | ✓ | ✓ | | | |
| Exports CSV | ✓ | ✓ | | ✓ | |
| Administrateurs | ✓ | | | | |
| Journal d'audit | ✓ | ✓ | | | ✓ |
| Système (+ réinitialisation démo) | ✓ | | | | ✓ |

Règles appliquées par l'API :
- un membre suspendu ne peut plus se connecter et ses jetons en cours sont refusés immédiatement ;
- un compte admin désactivé ou dont le rôle change est pris en compte à la requête suivante ;
- personne ne peut modifier son propre rôle ni se désactiver ; il reste toujours au moins un SuperAdmin actif ;
- mot de passe admin : 10 caractères minimum, lettres et chiffres ; session de 12 h, fermée avec l'onglet ;
- chaque action (connexion, suspension, masquage, suppression, export, gestion des comptes) est tracée dans le journal d'audit.

## Production

- `npm run build` produit `dist/` (site statique). Définir `VITE_API_URL=https://api.exemple.com` au build si la console n'est pas servie derrière le même domaine que l'API.
- Premier SuperAdmin : variables `Admin__BootstrapEmail` et `Admin__BootstrapPassword` de l'API (créé seulement s'il n'existe aucun compte admin).
- Ne pas exposer `/api/admin` sans HTTPS ; idéalement, restreindre l'accès (VPN, liste d'IP).

## Commandes

```bash
npm test            # Vitest
npm run typecheck   # TypeScript
npm run build
```

## Structure

```
src/
  main.tsx          routes (pages chargées à la demande) et garde par permission
  auth.tsx          session admin, droits, Guard
  layout/Shell.tsx  menu latéral filtré par rôle, version mobile
  pages/            Dashboard, Reports, Members, Posts, Communities, Events, Exports, Admins, Audit, System, Account, Login
  components/       ui.tsx (boutons, tableaux, modales, confirmations), toast.tsx
  lib/              api.ts (client + erreurs), types.ts (contrat), permissions.ts, format.ts
```
