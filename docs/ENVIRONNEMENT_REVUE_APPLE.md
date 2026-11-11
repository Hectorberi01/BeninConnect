# Environnement de revue Apple / Google — plan de mise en œuvre

Objectif : le compte de démonstration donné à Apple (et à Google) se connecte à une **API et une base séparées**,
remplies de données de démonstration, sans jamais toucher aux membres ni aux données de production.
Même application, même build, même image Docker que la production.

> Statut : **implémenté** (API, infrastructure, application). Mise en service : voir §9, dans l'ordre.

## 1. Vue d'ensemble

```
                       Application (build des stores, vise api.monbenin.com)
                                        │
                     POST /api/auth/login (email du compte de revue ?)
                                        │
              ┌──────────── non ────────┴──────── oui ────────────┐
              ▼                                                   ▼
   api.monbenin.com (Production)                 réponse « utilisez review-api.monbenin.com »
   base beninconnect                                              │
                                                                  ▼
                                               review-api.monbenin.com (Review)
                                               base beninconnect_review
                                               données de démo, remises à zéro chaque nuit
```

Aiguillage décidé par le **serveur** (liste d'emails dans la configuration) : rien de caché dans l'application,
expliqué dans les notes de revue (pas de risque vis-à-vis de la règle Apple 2.3.1 sur les fonctions cachées).

## 2. Décisions préalables

| Sujet | Proposition |
|---|---|
| Sous-domaine | `review-api.monbenin.com` (enregistrement DNS A vers le VPS) |
| Comptes de revue | `appreview@monbenin.com` (Apple), `playreview@monbenin.com` (Google) |
| Base | `beninconnect_review` sur le même serveur PostgreSQL, utilisateur dédié sans droit sur la base de production |
| Photos / vidéos | dossier `media-review` (disque) ou préfixe dédié dans le bucket R2 |
| Version de l'API de revue | toujours la même que la production (`APP_VERSION` commun) |
| Remise à zéro | chaque nuit (ex. 03:00 UTC) + bouton manuel |
| Sauvegardes | aucune (données jetables) |

## 3. API (.NET)

### 3.1 Mode `Review`
Nouvel environnement `ASPNETCORE_ENVIRONMENT=Review` :
- garde toutes les protections de la production (proxy, limites de débit, CORS strict, journaux JSON, en-têtes) :
  les tests `!IsDevelopment()` actuels s'appliquent déjà tels quels ;
- **autorise** ce que la production refuse dans `Host/Production.cs` :
  - code de vérification fixe (`Auth__FixedVerificationCode`), pour qu'Apple s'inscrive sans recevoir d'email ;
  - données de démo au démarrage (`Database__SeedDemoData=true`) ;
  - remise à zéro des données (aujourd'hui limitée à `IsDevelopment()` dans `AdministrationModule`) ;
- emails : désactivés ou journalisés (aucun envoi réel) ;
- sauvegardes automatiques : désactivées (`Backups__Automatic=false`).

### 3.2 Aiguillage à la connexion (API de production)
- Configuration `Review:Emails` (liste) et `Review:ApiUrl`.
- `POST /api/auth/login`, `register`, `forgot-password` : si l'email figure dans la liste, réponse dédiée
  (ex. `{ "redirect": "https://review-api.monbenin.com" }`) **sans** vérifier le mot de passe ni créer quoi que ce soit.
- Côté API de revue : liste vide (pas de redirection en boucle).
- Les emails de revue ne sont pas secrets ; le mot de passe n'existe que dans la base de revue.

### 3.3 Remise à zéro planifiée
- Tâche de fond active seulement en mode `Review` (`Review:ResetDailyAtUtc`), qui appelle `DemoDataReset` existant.
- Le compte de revue est recréé avec le même mot de passe à chaque remise à zéro (seeder).

### 3.4 Tests
- Production : un email de revue est redirigé, un email normal ne l'est pas.
- Mode Review : démarre avec code fixe et données de démo ; mode Production : refuse toujours le code fixe.
- Remise à zéro planifiée : les contenus créés par le testeur disparaissent, le compte de revue reste.

## 4. Infrastructure

### 4.1 Docker compose (`docker-compose.prod.yml`)
- Service `api-review` : **même image** `${API_REGISTRY}/beninconnect-api:${APP_VERSION}`,
  port `127.0.0.1:8081`, volume `media-review`, `ASPNETCORE_ENVIRONMENT=Review`.
- Secrets dans un fichier séparé `server/.env.review` (déposé à la main, comme `.env`) :
  base de revue, **clé JWT différente** (un jeton de revue n'est jamais valable en production), jeton Expo.
- Mémoire : ~150–300 Mo de plus sur le VPS (à vérifier avec `docker stats`).

### 4.2 PostgreSQL
```sql
-- une fois, par l'administrateur de la base
CREATE ROLE beninconnect_review LOGIN PASSWORD '…';
CREATE DATABASE beninconnect_review OWNER beninconnect_review;
```
Aucun droit de ce rôle sur `beninconnect`.

### 4.3 Caddy
Bloc `review-api.monbenin.com` → `127.0.0.1:8081` (HTTPS automatique), comme le bloc de l'API.

### 4.4 Ansible
- Variable `review_enabled` (désactivable).
- Vérifier la présence de `.env.review` (arrêt clair s'il manque, comme pour `.env`).
- Santé : contrôler `/health` des deux API après déploiement.
- `rollback.sh` : redémarrer `api` **et** `api-review` (même version).

### 4.5 CI / déploiement
Rien à ajouter : même image, déployée en même temps.

## 5. Application mobile

- La connexion gère la réponse `redirect` : l'adresse de l'API de revue est mémorisée **avec la session**
  (stockage sécurisé, comme le jeton), puis la connexion est refaite sur cette adresse.
- Toute l'application suit cette adresse : requêtes, temps réel (SignalR), images, pièces jointes, vidéos.
  Aujourd'hui la source de données est créée une seule fois avec l'adresse de production (`src/data/index.ts`) :
  elle devra être recréée à la connexion et à la déconnexion.
- Déconnexion : retour à l'API de production.
- Inscription et mot de passe oublié : même gestion de la redirection.
- Aucun bandeau « démo » obligatoire ; mention discrète possible dans Paramètres > À propos.
- Publication : nouvelle version sur les deux stores (ex. 1.1.0).

## 6. Notes pour Apple / Google

> Demo account: appreview@monbenin.com / ••••••••
> This account signs in to a dedicated demonstration environment populated with sample members,
> communities, posts and conversations, so that the review does not affect real users. All features
> (posting, comments, messaging, notifications, account deletion) are fully functional.
> If you create a new account, the verification code is 123456.

(Dernière phrase : seulement si l'inscription est aussi redirigée vers la revue, voir §8.)

## 7. Ordre de mise en œuvre

| Étape | Contenu | Durée |
|---|---|---|
| 1 | DNS, base et rôle PostgreSQL, `.env.review` sur le serveur | 0,5 h (vous) |
| 2 | Mode `Review`, garde-fous, remise à zéro planifiée, tests | 0,5 j |
| 3 | Redirection à la connexion (production), tests | 0,25 j |
| 4 | Compose, Caddy, Ansible, rollback.sh, documentation | 0,5 j |
| 5 | Application : adresse d'API par session, redirection, tests | 0,5 j |
| 6 | Vérification de bout en bout sur build de test (TestFlight) | 0,25 j |
| 7 | Publication + notes de revue mises à jour | — |

Les étapes 2 à 4 peuvent être déployées **avant** la nouvelle application : sans elle, rien ne change
(l'ancienne application ignore la redirection… et ne peut donc pas utiliser le compte de revue :
attendre l'étape 5 avant de changer le compte donné à Apple).

## 8. Limites et risques

1. **Inscription d'un nouveau compte par le testeur** : l'application ne sait pas que c'est Apple ; le compte est créé
   en production (vide, supprimable depuis la console). Pour l'éviter : rediriger aussi les inscriptions d'emails
   de domaines connus d'Apple (`@apple.com`) — fiabilité limitée, à décider.
2. **Liens partagés et code QR** (`monbenin.com/post/…`, `/user/…`) : pointent vers la production ;
   sans effet pour la revue.
3. **Notifications push** : fonctionnent (même projet Expo), mais un jeton d'appareil pourrait recevoir des notifications
   des deux environnements si le testeur change de compte ; sans conséquence.
4. **Console d'administration** : reste sur la production ; la revue s'administre par remise à zéro.
5. **Ancienne application** : un testeur sur une ancienne version ne suit pas la redirection
   (voir §7 : changer le compte de revue seulement après la publication).
6. **Coût** : un conteneur de plus sur le VPS ; base sur le serveur existant ; aucun service payant ajouté.

## 9. Mise en service (dans l'ordre)

Tant que `review_enabled` vaut `false` et que `REVIEW_EMAIL_*` est vide, rien ne change en production.

### Étape 1 — Serveur (vous, ~30 min)

1. DNS : enregistrement **A** `review-api.monbenin.com` → IP du VPS (même cible que `api.monbenin.com`).
2. Base de revue, sur le serveur PostgreSQL :
   ```sql
   CREATE ROLE beninconnect_review LOGIN PASSWORD 'mot-de-passe-fort';
   CREATE DATABASE beninconnect_review OWNER beninconnect_review;
   ```
3. Secrets : `server/.env.review.example` → `/opt/beninconnect/server/.env.review` (droits 600), puis remplir :
   connexion à la base de revue, **nouvelle** clé JWT (`openssl rand -base64 48`), mot de passe du compte de revue.
4. `ansible/group_vars/production/main.yml` : `review_enabled: true`, commit, push → déploiement.
5. Vérifier : `curl https://review-api.monbenin.com/health` et une connexion avec le compte de revue
   (build de développement pointé sur `https://review-api.monbenin.com`).

### Étape 2 — Redirection en production

Sans risque si `appreview@monbenin.com` est une **nouvelle** adresse (pas celle déjà donnée à Apple) :
seul ce compte est concerné. Dans `/opt/beninconnect/server/.env` :
```
REVIEW_EMAIL_1=appreview@monbenin.com
REVIEW_API_URL=https://review-api.monbenin.com
```
puis `docker compose -f docker-compose.prod.yml --env-file .env --env-file .env.deploy --profile review up -d api`.

Une ancienne version de l'app qui tente ce compte affiche « Ce compte de démonstration nécessite la dernière version
de l'application » (rien n'est créé en production).

### Étape 3 — Application

1. Version à jour (ex. 1.1.0 dans `mobile-expo/app.json`), `eas build --platform all`.
2. TestFlight / test interne : se connecter avec `appreview@monbenin.com` → fil de démonstration (et non la production) ;
   se déconnecter puis se connecter avec un vrai compte → production.
3. Soumettre aux stores.

### Étape 4 — Notes de revue

Mettre à jour le compte et le texte (§6) dans App Store Connect et la Play Console.

### Exploitation

- Remise à zéro : automatique chaque nuit à 03:00 UTC (`REVIEW_RESET_AT_UTC` dans le `.env` pour changer l'heure).
- Remise à zéro immédiate (ex. juste avant une soumission) : recréer la base de revue puis redémarrer l'API de revue,
  qui la remplit au démarrage :
  ```sql
  DROP DATABASE beninconnect_review WITH (FORCE);
  CREATE DATABASE beninconnect_review OWNER beninconnect_review;
  ```
  ```bash
  docker compose -f docker-compose.prod.yml --env-file .env --env-file .env.deploy --profile review restart api-review
  ```
- Retour arrière : `rollback.sh` redéploie aussi `api-review` dans la même version.
- Journaux : `docker compose -f docker-compose.prod.yml --env-file .env --env-file .env.deploy --profile review logs -f api-review`.
