# BéninConnect Community — prototype mobile Avalonia

Application communautaire pour la diaspora béninoise : fil d'actualité, communautés, messagerie, événements, notifications, profil et paramètres. **Toutes les données sont fictives, en mémoire, sans backend ni connexion Internet.**

## Prérequis

- SDK .NET 9 (ou plus récent : `global.json` autorise la version majeure supérieure)
- Android : `dotnet workload install android` + SDK Android (Android Studio)
- iOS : `dotnet workload install ios` + Xcode (macOS uniquement)

## Lancer

```bash
# Version desktop au format téléphone (le plus rapide pour développer)
dotnet run --project src/BeninConnect.Desktop

# Android (émulateur ou appareil connecté)
dotnet build src/BeninConnect.Android -t:Run

# iOS (simulateur)
dotnet build src/BeninConnect.iOS -t:Run -p:RuntimeIdentifier=iossimulator-arm64

# Tests
dotnet test tests/BeninConnect.Tests     # ViewModels, repositories, navigation
dotnet test tests/BeninConnect.UiTests   # vues réelles en mode headless (parcours complet)
```

## Compte de démonstration

| Email | Mot de passe |
|---|---|
| `demo@beninconnect.test` | `Demo123!` |

Le bouton « Utiliser » de l'écran de connexion remplit ces identifiants. Inscription : le code de vérification simulé est `123456` (aucun email n'est envoyé).

## Projets

| Projet | Rôle |
|---|---|
| `src/BeninConnect.App` | Application partagée (vues, ViewModels, données mockées) |
| `src/BeninConnect.Android` | Hôte Android |
| `src/BeninConnect.iOS` | Hôte iOS |
| `src/BeninConnect.Desktop` | Hôte de développement (fenêtre 400×860) — ajout par rapport à la spec |
| `tests/BeninConnect.Tests` | Tests unitaires xUnit |
| `tests/BeninConnect.UiTests` | Tests d'intégration UI (Avalonia.Headless) |

## Fonctionnalités de démonstration utiles

- **Paramètres › Mode développeur › Simuler des erreurs réseau** : environ un appel sur deux échoue, pour voir les états d'erreur et le bouton « Réessayer ».
- **Conversation › « Démo : simuler une réponse »** : ajoute une réponse fictive, toujours étiquetée « Réponse simulée ».
- Le thème clair/sombre/système est enregistré localement, comme l'onboarding et l'option « Rester connecté ».
- Au redémarrage, le jeu de données initial est restauré (les publications créées disparaissent).

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) : structure, navigation, décisions.
- [`docs/REMPLACEMENT_API.md`](docs/REMPLACEMENT_API.md) : brancher l'API ASP.NET Core 10 à la place des mocks.
- `tools/generate_assets.py` : régénère les illustrations (aucune photo réelle, aucune licence tierce).
