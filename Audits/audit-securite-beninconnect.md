# Audit de sécurité — BéninConnect (API .NET + app mobile Expo)

Date : 13/11/2026 · Périmètre : `server/` (API, Caddy, docker-compose, Ansible) et `mobile-expo/` (app client React Native).
Méthode : relecture du code source (pas de test d'intrusion sur la production).

## Synthèse

| # | Gravité | Constat | Où |
|---|---|---|---|
| 1 | **CRITIQUE** | Prise de contrôle de n'importe quel compte vérifié via `/api/auth/verify-email` | `Identity/Domain/User.cs` + `Application/Auth.cs` |
| 2 | **ÉLEVÉE** | L'email de chaque membre est renvoyé à tous les membres connectés | `IdentityAbstractions.cs` (`UserDto`) |
| 3 | **ÉLEVÉE** | Jeton de session de 7 jours placé dans l'URL des documents ouverts dans le navigateur | `apiDataSource.ts`, `attachments.tsx` |
| 4 | MOYENNE | Un membre peut faire supprimer les photos d'un autre membre | `PostDeletion.cs`, `Media.cs` |
| 5 | MOYENNE | Code de vérification d'email : pas de limite d'essais ni d'expiration | `User.VerifyEmail` |
| 6 | MOYENNE | Jetons non révocables (changement de mot de passe, suppression de compte, déconnexion) | `Jwt.cs`, `IdentityModule.cs` |
| 7 | MOYENNE | Pièces jointes « privées » dans le bucket public si `S3_PRIVATE_BUCKET` est vide | `S3MediaStorage.CreatePrivate` |
| 8 | FAIBLE | Énumération des emails à l'inscription (409) et par le temps de réponse du login | `Auth.cs` |
| 9 | FAIBLE | Limites de débit par IP : risque de blocage des utilisateurs derrière le CGNAT des opérateurs mobiles | `Production.cs` |
| 10 | FAIBLE | Mises à jour OTA (EAS Update) non signées | `app.json` |
| 11 | FAIBLE | Divers durcissements (console admin sans MFA ni CSP, `NSAllowsLocalNetworking` en prod, redirection de revue acceptée vers n'importe quel hôte https) | voir détail |

Le reste est solide : PBKDF2 + `FixedTimeEquals`, audiences JWT séparées membre / admin, contrôle du compte admin désactivé à chaque requête, permissions par rôle sur toutes les routes `/api/admin`, contrôle des participants sur la messagerie et le hub SignalR, détection du type des fichiers par signature, noms de sauvegarde validés (pas de traversée de chemin), XML sans DTD, API liée à `127.0.0.1` derrière Caddy, jeton masqué dans les logs Caddy, SSH par clé + UFW + fail2ban, garde-fous de démarrage en production, jeton mobile dans le trousseau / Keystore (SecureStore), EXIF désactivé côté app, secrets (`.env`, compte de service Google Play) absents de Git.

---

## 1. CRITIQUE — Prise de contrôle de compte via `verify-email`

`User.VerifyEmail` sort sans rien vérifier si le compte est déjà vérifié :

```csharp
public void VerifyEmail(string code)
{
    if (IsEmailVerified) return;   // <-- aucun contrôle du code
    ...
}
```

et `VerifyEmailHandler` délivre ensuite un jeton membre :

```csharp
user.VerifyEmail(request.Code);
...
return new AuthResponse(tokens.CreateToken(user), user.ToDto());
```

**Exploitation** : `POST /api/auth/verify-email {"email":"victime@x.com","code":"000000"}` renvoie un JWT valable 7 jours pour n'importe quel compte vérifié, sans mot de passe. Cela marche aussi pour le **compte officiel** (`officiel@monbenin.com` par défaut, `IsEmailVerified = true`) : la règle « jamais de connexion » n'est appliquée que dans `LoginHandler`. Combiné au constat 2, un attaquant récupère la liste des emails puis prend tous les comptes.

**Correctif** (à déployer en priorité) :

```csharp
// VerifyEmailHandler
if (user.IsEmailVerified || user.IsOfficial)
    throw new DomainException("Code de vérification incorrect.");
user.VerifyEmail(request.Code);
```

et dans le domaine, remplacer le `return` par une exception. Ajouter un test d'intégration : « verify-email sur un compte déjà vérifié → 400, aucun jeton ».

## 2. ÉLEVÉE — Fuite des emails de tous les membres

`UserDto` contient `Email`, et il est renvoyé par `GET /api/users?search=`, `GET /api/users/{id}`, `POST /api/users/batch` (500 ids) et dans les DTO de conversation. N'importe quel membre peut aspirer l'annuaire complet des emails : problème RGPD et base de spam / phishing.

**Correctif** : un `PublicUserDto` sans email (ni `IsEmailVerified`) pour tout ce qui concerne les autres membres ; garder `UserDto` complet pour `/api/auth/me`, `PUT /api/users/me` et la console admin. Côté app, vérifier qu'aucun écran n'affiche l'email d'un autre membre.

## 3. ÉLEVÉE — Jeton de session dans l'URL des pièces jointes

`attachmentUrl()` construit `…/api/messages/{id}/attachment?access_token=<JWT 7 jours>`. Pour les documents, `WebBrowser.openBrowserAsync(url)` ouvre cette URL dans Safari View Controller / Chrome Custom Tabs. Sur Android, Custom Tabs partage l'historique de Chrome ; l'utilisateur peut aussi « Ouvrir dans Chrome » ou « Partager le lien » : le jeton complet du compte part avec. Il finit aussi dans le cache disque d'`expo-image`.

**Correctif** : une URL signée courte et limitée à un fichier, par exemple `POST /api/messages/{id}/attachment/link` qui renvoie `?sig=` (HMAC du messageId, de l'utilisateur et d'une expiration de 5 minutes). Retirer ensuite `/api/messages/*/attachment` de la liste `OnMessageReceived` (garder `access_token` seulement pour `/hubs`). Alternative pour les documents : télécharger avec l'en-tête `Authorization` puis ouvrir le fichier local.

## 4. MOYENNE — Suppression des photos d'autres membres

Les chemins `uploads/…` envoyés par le client sont validés pour leur format, pas pour leur propriétaire. Or la suppression d'une publication (et le remplacement d'une photo en modification) publie `ImagesReleased`, qui efface le fichier.

**Exploitation** : créer une publication avec `Images = ["uploads/<photo d'un autre membre>"]` (chemin visible dans tout `PostDto`), puis la supprimer : la photo disparaît aussi de la publication ou du profil d'origine. Même chose via avatar / couverture.

**Correctif** : enregistrer le propriétaire à l'upload (table `media.files` : nom, ownerId, createdAt) et, à la création ou modification d'un post ou d'un profil, refuser un fichier qui n'appartient pas au membre. Option plus simple : n'effacer un fichier que s'il n'est plus référencé nulle part.

## 5. MOYENNE — Code de vérification d'email devinable

Le code à 6 chiffres n'a ni compteur d'essais ni date d'expiration (contrairement au code de réinitialisation, limité à 5 essais et 30 minutes). La seule protection est 10 requêtes par minute et par IP : avec quelques dizaines d'IP, un compte non vérifié se valide en quelques heures. Ce n'est grave que pour les comptes en attente, mais c'est à aligner sur le reset.

**Correctif** : `VerificationAttempts` (5 au maximum puis code invalidé) et `VerificationExpiresAt` (15 à 30 minutes), comme pour `ResetPassword`.

## 6. MOYENNE — Jetons non révocables

Les JWT membres durent 7 jours et ne sont contrôlés que pour `IsSuspended`. Un changement de mot de passe (après un vol de compte), une déconnexion ou une suppression de compte laissent les jetons existants valides. Après suppression, un jeton peut encore créer des publications rattachées à un compte qui n'existe plus (`CreatePost` ne vérifie pas l'existence de l'auteur).

**Correctif** : un `SecurityStamp` (ou `TokenVersion`) sur `User`, mis dans le jeton et changé au reset de mot de passe ou sur demande « Se déconnecter partout ». Dans `OnTokenValidated`, une seule requête vérifie à la fois l'existence, la suspension et le stamp. À terme : jeton d'accès court (15 à 60 minutes) et refresh token révocable.

## 7. MOYENNE — Pièces jointes privées dans le bucket public

`S3MediaStorage.CreatePrivate` réutilise le bucket principal (préfixe `private/`) si `Media__S3__PrivateBucket` est vide. Comme le bucket principal est servi publiquement via l'URL `r2.dev`, les pièces jointes des messages seraient alors lisibles par quiconque connaît ou retrouve leur nom, sans contrôle des participants.

**À vérifier sur le serveur** : `S3_PRIVATE_BUCKET` est bien renseigné dans le `.env` de production, avec un bucket sans accès public. Pour durcir : faire échouer `Production.Validate` si `Provider=s3`, `PublicBaseUrl` renseigné et `PrivateBucket` vide.

## 8. FAIBLE — Énumération des emails

- `POST /api/auth/register` renvoie 409 « Un compte existe déjà », ce qui contredit le soin pris sur `forgot-password` et `resend-code`. Option : toujours répondre 201 et envoyer à l'adresse existante un email « Vous avez déjà un compte ».
- `LoginHandler` ne calcule pas de PBKDF2 quand l'email est inconnu (réponse plus rapide). Option : vérifier contre un hash factice.

Moins prioritaire tant que le constat 2 n'est pas corrigé.

## 9. FAIBLE — Limites de débit et CGNAT

`UseRateLimiter` s'exécute avant `UseAuthentication` : `ctx.User` est vide, donc toutes les limites (y compris la globale de 600/min) sont **par IP**. Les opérateurs mobiles béninois partagent beaucoup d'IP (CGNAT) : 10 tentatives de connexion par minute et par IP peuvent bloquer des utilisateurs légitimes au même moment.

**Pistes** : placer le limiteur global après l'authentification (clé = `sub`), et pour `/api/auth` utiliser une clé IP + email normalisé, avec une limite par IP plus large.

## 10. FAIBLE — Mises à jour OTA non signées

`expo-updates` est actif (`checkAutomatically: ON_LOAD`) sans signature de code. Si le compte Expo / EAS est compromis, du JavaScript arbitraire est poussé à tous les utilisateurs. Activer la signature EAS Update (`expo-updates` `codeSigningCertificate`) et la 2FA sur le compte Expo.

## 11. FAIBLE — Durcissements divers

- **Console admin** : pas de MFA alors qu'elle donne accès aux exports CSV et aux sauvegardes complètes de la base (rôles SuperAdmin et Dev). Ajouter un TOTP au moins pour ces rôles. Ajouter aussi une `Content-Security-Policy` sur `admin.monbenin.com` (`default-src 'self'; img-src 'self' https://<r2>; connect-src 'self'`). Le stockage du jeton en `sessionStorage` est un bon choix.
- **Sauvegardes** : les dumps `.sql.gz` (local et copie R2) ne sont pas chiffrés. Si les identifiants R2 fuient, toute la base fuit (les mêmes clés servent aux médias et aux sauvegardes). Utiliser une clé R2 dédiée aux sauvegardes et chiffrer les dumps (age ou gpg).
- **App iOS** : `NSAllowsLocalNetworking` et `NSLocalNetworkUsageDescription` sont présents dans le build de production ; les réserver aux builds de développement (config dynamique `app.config.ts`).
- **Redirection de revue** : l'app accepte toute URL `https://` renvoyée dans un 421. Le risque est faible (cela suppose un serveur compromis), mais une liste blanche `*.monbenin.com` ne coûte rien.
- **Android** : `allowBackup` reste à la valeur par défaut (true). Le jeton n'est pas concerné (Keystore), mais les préférences AsyncStorage le sont. Le passer à `false` si rien ne doit être restauré.
- **`google-services.json`** est versionné. La clé Firebase n'est pas un secret, mais restreignez-la au package Android dans la console Google Cloud, surtout si le dépôt GitHub est public.
- **Source éditoriale** : un admin peut faire récupérer par le serveur n'importe quelle URL (SSRF limitée aux comptes `EditorialManage`). Refuser les IP privées et loopback dans le client HTTP éditorial.

---

## Ordre de traitement conseillé

1. **Aujourd'hui** : constat 1 (2 lignes + test), puis déploiement.
2. **Cette semaine** : constat 2 (DTO public) et vérification de `S3_PRIVATE_BUCKET` (constat 7).
3. **Prochaine version de l'app** : constat 3 (URL signées) avec la modification serveur.
4. Ensuite : 4, 5, 6, puis les durcissements.

Après le correctif 1, consultez les logs Caddy (`/var/log/caddy/api.log`) pour repérer d'éventuels appels `POST /api/auth/verify-email` suivis d'un 200 sur des comptes créés il y a longtemps : ce serait le signe d'une exploitation passée.
