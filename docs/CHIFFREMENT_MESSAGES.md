# Chiffrement des messages en base

Le texte des messages et le nom des pièces jointes sont chiffrés en **AES-256-GCM** avant l'écriture en base (module Messaging). Une lecture directe de la table `messaging.messages`, ou d'une sauvegarde, ne montre que des valeurs `enc1:<id de clé>:<…>`. L'API déchiffre à la lecture : l'application ne voit aucune différence.

Ce chiffrement protège la base et ses sauvegardes. Ce n'est pas un chiffrement de bout en bout : le serveur, qui détient la clé, peut toujours lire les messages.

## Clé

| Variable | Rôle |
|---|---|
| `MESSAGES_ENCRYPTION_KEY` (`.env`) → `Messaging__EncryptionKey` | clé actuelle, 32 octets en base64 : `openssl rand -base64 32` |
| `MESSAGES_PREVIOUS_KEYS` → `Messaging__PreviousEncryptionKeys` | anciennes clés après une rotation (séparées par des virgules), encore lues |

- Hors développement, l'API **refuse de démarrer** sans clé valide.
- En développement et dans les tests, une clé est dérivée de la clé JWT.
- La revue Apple/Google utilise sa propre clé (`.env.review`).
- **Perdre la clé rend les messages illisibles** : ils s'affichent « 🔒 Message illisible ». Gardez-la dans un gestionnaire de mots de passe, séparément des sauvegardes de la base.

### Rotation

1. Placer l'ancienne clé dans `MESSAGES_PREVIOUS_KEYS`, puis la nouvelle dans `MESSAGES_ENCRYPTION_KEY`.
2. Redémarrer l'API. Les nouveaux messages sont chiffrés avec la nouvelle clé, et les anciens restent lisibles.

## Messages existants

Les messages enregistrés avant la mise en service restent en clair jusqu'à la reprise, mais sont lus normalement.

- **Lancer la reprise :** console → Tableau de bord → carte « Chiffrement des messages » → **Chiffrer les messages existants** (rôles SuperAdmin, Admin, Développeur). Le lancement est inscrit au journal d'audit.
- **Fonctionnement :** la tâche traite les messages par lots de 200, en arrière-plan. Un message modifié pendant la reprise n'est pas écrasé : il est repris au lancement suivant.
- **Routes :** `GET /api/admin/messaging/encryption` (état) et `POST /api/admin/messaging/encryption/run`.

## Hors périmètre

- Les **fichiers** joints (photos, PDF, messages vocaux) sont dans le stockage privé, accessibles aux seuls participants, mais pas chiffrés par l'application. Le chiffrement côté serveur du fournisseur (R2) s'applique.
- Les **notifications** de nouveau message (module Notifications) gardent un extrait du texte dans leur table. Pour l'éviter : un corps générique (« Nouveau message ») ou le même chiffrement côté Notifications.
