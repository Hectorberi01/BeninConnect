# Notifications push

Chaîne : action d'un membre → notification enregistrée (module Notifications) → file en mémoire → envoi en
arrière-plan au service push d'Expo → APNs (iPhone) / FCM (Android).

| Côté | Contenu |
|---|---|
| API | table `notifications.push_devices` (jeton Expo, plateforme, catégories coupées), routes `POST /api/notifications/devices` et `/devices/unregister`, envoi par `ExpoPushSender`, jetons invalides supprimés automatiquement |
| App | `src/core/push.ts` : autorisation, jeton Expo, enregistrement après connexion, mise à jour quand les réglages de notifications changent, désinscription à la déconnexion, ouverture de l'écran concerné au toucher |
| Contenu | même titre et texte que la notification dans l'app ; pastille = nombre de non lues ; données `target` / `targetId` |

Les catégories coupées dans Paramètres › Notifications (Social, Communautés, Messages, Événements) ne sont pas
envoyées en push ; elles restent visibles dans l'onglet Notifications.

## 1. Projet Expo (une fois)

```bash
cd mobile-expo
npm install                       # expo-notifications, expo-device
npx eas-cli@latest login
npx eas-cli@latest init           # ajoute extra.eas.projectId et owner dans app.json : à committer
```

Sans `projectId`, l'app n'enregistre aucun téléphone (avertissement dans la console Metro).

## 2. iPhone (APNs)

- Compte Apple Developer payant obligatoire (le compte gratuit « Personal Team » ne permet pas le push).
- `npx eas-cli@latest credentials -p ios` → Push Notifications : créer ou importer une clé APNs (.p8).
- Régénérer et réinstaller l'app (ajoute le droit `aps-environment`) :
  ```bash
  npx expo prebuild -p ios --clean
  npx expo run:ios --device       # ou un build EAS : npx eas-cli build -p ios --profile development
  ```

## 3. Android (FCM)

1. console.firebase.google.com → projet → ajouter une app Android `com.hbatech.beninconnect.rn`,
   télécharger `google-services.json` dans `mobile-expo/`, puis dans `app.json` : `"android": { "googleServicesFile": "./google-services.json", ... }`.
2. Firebase → Paramètres → Comptes de service → générer une clé privée (JSON).
3. `npx eas-cli@latest credentials -p android` → Google Service Account → FCM V1 : importer ce JSON.
4. `npx expo prebuild -p android --clean && npx expo run:android`.

Expo Go ne reçoit pas les push sur Android : utiliser un build de développement.

## 4. Serveur

Rien d'obligatoire. Si « Enhanced Security for Push Notifications » est activé dans le projet Expo :
`EXPO_ACCESS_TOKEN=...` dans le `.env` du serveur (expo.dev → Access tokens), puis redémarrer l'API.

Désactiver l'envoi (journal seulement) : `Push__Provider=log`.

## 5. Tester

1. Se connecter dans l'app sur un vrai téléphone, accepter les notifications.
2. Avec un second compte, commenter ou aimer une publication du premier, ou lui envoyer un message.
3. En cas de problème : `docker compose ... logs api | grep -i push` (refus d'Expo, jeton invalide), et
   `select "Platform", "MutedCategories", "LastSeenAt" from notifications.push_devices;` pour vérifier l'enregistrement.
   L'outil https://expo.dev/notifications permet d'envoyer un push de test à un jeton.
