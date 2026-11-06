# BéninConnect – version React Native (Expo)

Deuxième client mobile de BéninConnect Community, écrit en **React Native + TypeScript** avec **Expo SDK 58**.
Il reprend tous les écrans et parcours de la version Avalonia (`../src/BeninConnect.App`), qui reste inchangée,
et parle à la même API (`../server`).

## Prérequis

- Node.js 20 LTS ou plus récent, npm
- iOS : Xcode 26 + simulateur, ou un iPhone avec l'app **Expo Go** (ou un build de développement)
- Android : Android Studio + émulateur, ou un téléphone avec **Expo Go**

## Lancer l'application

```bash
cd mobile-expo
npm install
npx expo start          # puis i (simulateur iOS), a (émulateur Android), w (navigateur)
```

Sur un vrai téléphone : scanner le QR code affiché avec l'appareil photo (iOS) ou Expo Go (Android).

Build natif local (sans Expo Go) :

```bash
npx expo run:ios --device     # iPhone branché en USB (signature Xcode requise)
npx expo run:android
```

Compte de démonstration : `demo@beninconnect.test` / `Demo123!` (bouton « Remplir automatiquement » sur l'écran de connexion).
Code de vérification d'email en démo : `123456`.

## Deux sources de données

| Source | Contenu | Réglage |
|---|---|---|
| Démo hors ligne (défaut) | 40 membres, 15 communautés, 100 publications, 12 conversations, 25 événements générés en mémoire (mêmes données que la version Avalonia) | Paramètres › Source de données › Démo hors ligne |
| API BéninConnect | Le serveur ASP.NET Core de `../server` (PostgreSQL) | Paramètres › Source de données › API, puis l'adresse |

Le changement de source déconnecte l'utilisateur puis revient à l'écran de connexion.

Adresse de l'API selon l'appareil :

- simulateur iOS / navigateur : `http://localhost:5080`
- émulateur Android : `http://10.0.2.2:5080`
- téléphone physique : `http://<IP du Mac sur le Wi‑Fi>:5080` (le serveur écoute sur `0.0.0.0:5080`)

Démarrer l'API : voir `../server/README.md` (`docker compose up -d` puis `dotnet run --project src/BeninConnect.Api`).
Le jeton JWT est conservé dans le trousseau iOS / Keystore Android (`expo-secure-store`) si « Rester connecté » est coché.

## Commandes utiles

```bash
npm test              # tests Jest (formats, validations, règles d'écran, source mock, client HTTP)
npm run typecheck     # TypeScript strict
npx expo export -p web   # vérifie que tout se compile (bundle web)
```

## Organisation

```
src/
  app/                    écrans (expo-router, routage par fichiers)
    index.tsx             splash : onboarding, session mémorisée ou connexion
    onboarding.tsx
    (auth)/               login, register, verify-email, forgot-password
    (app)/                zone connectée (redirige vers /login sans session)
      (tabs)/             Accueil, Communautés, Publier, Messages, Profil
      post/  community/  conversation/  events/  user/  profile/
      search.tsx  notifications.tsx  settings.tsx
  components/             ui.tsx (boutons, champs, puces, états), cards.tsx (publication, communauté, événement…)
  features/               queries.ts (React Query : lectures, écritures, caches), ProfileView, rules.ts
  core/                   thème, préférences, session, formats, validation, toast, dialogues, images
  data/
    repositories.ts       contrat DataSource unique
    mock/                 source hors ligne (données générées, mêmes règles que l'API)
    api/                  client HTTP de l'API (JWT, ProblemDetails → messages lisibles)
__tests__/                tests Jest
```

Équivalences avec la version Avalonia :

| Avalonia | React Native |
|---|---|
| ViewModels + CommunityToolkit.Mvvm | composants + hooks (`features/queries.ts`) |
| Messenger (PostChangedMessage…) | caches React Query mis à jour après chaque écriture |
| Repositories mock / HTTP | `DataSource` : `MockDataSource` / `ApiDataSource` |
| INavigationService | expo-router (`router.push`, `router.replace`) |
| IPreferencesService | `usePreferences` (AsyncStorage) |

## Différences connues

- Choix de la date d'un événement par saisie `JJ/MM/AAAA` et `HH:MM` (pas de sélecteur natif, pour éviter une dépendance native).
- Les images proposées à la publication sont celles embarquées dans l'app (aucun accès à la galerie), comme la version Avalonia.
- Sur le web, la session n'est pas mémorisée entre deux rechargements (pas de stockage sécurisé).
