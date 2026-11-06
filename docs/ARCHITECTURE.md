# Architecture et décisions

## Vue d'ensemble

MVVM (CommunityToolkit.Mvvm) + organisation par fonctionnalité. Chaque dossier `Features/<X>` contient `Models`, `Contracts`, `ViewModels`, `Views`, `Components`.

```
Core/        Navigation, DI, thème, convertisseurs, services transverses (session, toast, dialogues, préférences)
Shared/      Contrôles (AvatarView, AppTextBox, PasswordField, SearchBar, BadgeView, boutons), composants (AppHeader, états vide/chargement/erreur, BottomNavigationBar, UserCard), dialogues
Shell/       MainView (racine + surimpressions) et ShellView (onglets)
Features/    Onboarding, Authentication, Home, Feed, Communities, Messaging, Events, Search, Notifications, Profile, Settings
MockData/    MockDataStore, Seed (contenus fictifs), Factories (génération déterministe), Repositories
```

## Navigation

- `INavigationService` gère une **pile de ViewModels**. `NavigateTo<T>(param)`, `ReplaceWith<T>`, `ResetTo<T>`, `GoBack`, `TryHandleBack`.
- Le paramètre (id de publication, de communauté, de conversation…) arrive dans `OnNavigatedToAsync(object?)`.
- `ShellViewModel` contient les 4 onglets ; il reste dans la pile pendant la navigation secondaire et ses vues restent instanciées : **changer d'onglet ne recrée ni l'état ni le défilement**. L'onglet « Publier » ouvre `CreatePostViewModel`.
- `ViewLocator` : table explicite ViewModel → Vue (pas de réflexion, compatible trimming iOS/Android) et cache par instance pour conserver le défilement au retour.
- Bouton retour Android : `MainView` relaie `TopLevel.BackRequested` → dialogue ouvert fermé, sinon l'écran peut intercepter (onboarding, onglet ≠ Accueil, brouillon de publication), sinon retour dans la pile.
- Anti double-tap : même destination + même paramètre en moins de 400 ms ignorée ; les commandes asynchrones sont désactivées pendant leur exécution.

## Données

- `MockDataStore` (singleton) : listes de records **immuables**. Les repositories remplacent l'élément modifié ; les ViewModels n'accèdent jamais au store.
- `MockDataFactory` : graine fixe (2026), dates relatives à « maintenant » → identifiants stables, fil et événements toujours d'actualité. Volumes : 40 utilisateurs, 15 communautés, 100 publications, 200 commentaires, 12 conversations, 150 messages, 25 événements, 30 notifications (vérifiés par les tests).
- Notification des changements : `IMessenger` (WeakReferenceMessenger dédié par conteneur). Messages : `PostChangedMessage`, `PostCreatedMessage`, `MembershipChangedMessage`, `ConversationChangedMessage`, `EventChangedMessage`, `NotificationsChangedMessage`, `UserUpdatedMessage`…
- Latence simulée (180 ms) et erreurs simulées activables dans Paramètres.
- Les cartes (`PostItemViewModel`, `CommunityItemViewModel`, `EventItemViewModel`) portent leurs propres commandes (J'aime, Rejoindre, Participer…) ; des fabriques injectées résolvent auteurs et communautés via les repositories.

## États d'écran

`ViewModelBase.LoadAsync` gère `Loading / Content / Empty / Error` (+ annulation du chargement précédent). Chaque écran affiche `LoadingState`, `EmptyState` ou `ErrorState` (avec « Réessayer »).

## Thème

`Core/Theme/Colors.axaml` : palette de la spec en `ThemeDictionaries` clair/sombre ; toutes les vues utilisent `DynamicResource`. `Styles.axaml` : typographie (h1 28, h2 22, h3 18, corps 15, légende 13) et classes de boutons (`primary`, `secondary`, `outline`, `danger`, `chip`, `icon`…). Police Inter embarquée (`Avalonia.Fonts.Inter`). Icônes : tracés Material Design Icons (Apache 2.0) en `StreamGeometry`.

## Décisions notables

- **Hôte Desktop ajouté** pour itérer vite et exécuter les tests UI headless ; il n'est pas livré.
- **Avatars à initiales** plutôt que photos : aucun visage réel, aucune licence à gérer.
- **Illustrations générées** par `tools/generate_assets.py` (formes géométriques), embarquées en `AvaloniaResource`.
- **Galerie d'images de démonstration** à la place d'un sélecteur de fichiers natif (hors ligne, identique sur toutes les plateformes).
- **Communautés privées** : rejoindre crée une demande en attente ; le contenu reste masqué tant que l'utilisateur n'est pas membre.
- **Réponses de messagerie** : uniquement via le bouton explicite « Démo : simuler une réponse », toujours étiquetées.
- **Langue** : seul le français est disponible ; l'anglais est affiché comme « Bientôt » (pas de bouton factice).
- Pagination simulée du fil par pages de 10 (`Voir plus de publications`). Les listes longues et homogènes (membres, conversations, notifications) utilisent `ListBox` (virtualisé).
