# Module Voyages : « Qui part au pays ? »

Mise en relation entre un membre qui voyage (des kilos libres dans sa valise) et un membre qui veut envoyer un colis. Les deux sens sont gérés : Europe → Bénin et Bénin → Europe (tout couple de pays et de villes).

BéninConnect ne transporte rien et ne touche pas l'argent : le prix au kilo est indicatif et se règle entre membres.

## Version 1 (livrée)

### Parcours

**Voyageur**

1. Publie un trajet en deux étapes : départ, arrivée, date, kilos ; puis catégories acceptées, prix indicatif (EUR ou FCFA), zones de remise, informations et acceptation des règles.
2. Reçoit les demandes de réservation et les accepte ou les refuse. L'acceptation ouvre une conversation directe avec l'expéditeur, avec un premier message automatique.
3. À la remise, ouvre et vérifie le colis : il ne doit jamais accepter un colis fermé.
4. À la livraison, saisit le code à 6 chiffres donné par le destinataire. La réservation passe à « Livrée ».
5. Note l'expéditeur.

**Expéditeur**

1. Cherche un trajet (départ, arrivée, date limite, catégorie) ou crée une alerte.
2. Demande une réservation : poids, catégorie, contenu et attestation « aucun objet interdit ».
3. Une fois la réservation acceptée, génère le code de remise et le partage à son destinataire (WhatsApp, SMS…).
4. Note le voyageur après la livraison.

### Règles métier

| Règle | Valeur |
|---|---|
| Date de départ | d'aujourd'hui à 90 jours |
| Kilos d'un trajet | 0,5 à 100 kg |
| Poids d'une réservation | 0,5 à 50 kg ; vérifié à la demande et de nouveau à l'acceptation |
| Prix indicatif | 0 à 200 €/kg ou 0 à 130 000 FCFA/kg, facultatif |
| Trajets publiés | 5 par jour et par membre |
| Demandes de réservation | 10 par jour et par membre ; une seule en cours par trajet |
| Alertes | 5 par membre |
| Code de remise | 6 chiffres, haché (PBKDF2), 5 essais ; au-delà l'expéditeur génère un nouveau code |
| Avis | un par côté et par réservation, seulement après « Livrée », bloqués pendant un litige |
| Signalements | un par membre et par cible ; trajet ou avis masqué automatiquement au 3e |
| Voyageur de confiance | 3 livraisons notées 4/5 ou plus par les expéditeurs |

- Le trajet passe à « Complet » quand tous les kilos sont réservés, et redevient « Ouvert » si une réservation est annulée avant le départ.
- Tâche horaire (`VoyagesWorker`) :
  - rappel la veille du départ au voyageur et aux expéditeurs acceptés ;
  - trajet « Parti » le lendemain du départ, avec les demandes restées sans réponse annulées et l'expéditeur prévenu ;
  - trajets à venir des voyageurs suspendus masqués, puis rétablis à la levée de la suspension.
- Une modification de trajet n'est possible que si aucune réservation n'a été acceptée. L'annulation d'un trajet annule les réservations en cours et prévient les expéditeurs.
- Deux acceptations simultanées ne peuvent pas dépasser la capacité : jeton de concurrence `xmin` sur le trajet.

### Sécurité et vie privée

- Aucun téléphone ni email dans les réponses : le membre apparaît sous la forme « Prénom I. », et le contact passe uniquement par la messagerie.
- Une réservation n'est visible que de l'expéditeur et du voyageur. Le code en clair n'est renvoyé qu'une fois, à l'expéditeur seulement.
- Les essais de code sont comptés en base avant la réponse. La route de livraison est aussi limitée par IP (politique `auth`).
- Membres bloqués (dans un sens ou dans l'autre) : ils ne voient pas leurs trajets, leur fiche, leurs statistiques ni leurs avis, et ne peuvent pas réserver. L'acceptation est refusée si l'un a bloqué l'autre.
- Toutes les routes membres demandent une connexion. Les routes console demandent la permission `ContentModerate`, et chaque décision est inscrite au journal d'audit.
- Suppression de compte (`MemberDeleted`) : trajets, réservations, avis, alertes et signalements effacés. L'autre partie d'une réservation en cours est prévenue, et les kilos libérés sur les trajets des autres.

### API

```
GET    /api/trips?from=FR&to=BJ&fromCity=&toCity=Cotonou&after=&before=&category=&minKg=&cursor=&take=
GET    /api/trips/info                       règles, objets interdits, conseils, liens douane
GET    /api/trips/{id}
POST   /api/trips                            publier
PUT    /api/trips/{id}                       modifier (aucune réservation acceptée)
POST   /api/trips/{id}/cancel
GET    /api/trips/mine                       mes trajets et réservations reçues
POST   /api/trips/{id}/bookings              demander une réservation
GET    /api/trips/users/{id}/stats           bloc « Voyages » du profil
GET    /api/trips/users/{id}/reviews
GET    /api/trips/alerts | POST /api/trips/alerts | DELETE /api/trips/alerts/{id}
POST   /api/trips/reports                    { targetType: Trip|Booking|Review, targetId, reason }

GET    /api/bookings/mine                    mes envois
GET    /api/bookings/{id}
POST   /api/bookings/{id}/accept | /decline | /cancel
POST   /api/bookings/{id}/code               nouveau code (expéditeur)
POST   /api/bookings/{id}/deliver   {code}   voyageur
POST   /api/bookings/{id}/dispute   {reason}
POST   /api/bookings/{id}/review    {rating, comment}

GET    /api/admin/trips/overview
GET    /api/admin/trips?view=All|Upcoming|Hidden|Reported&q=
GET    /api/admin/trips/{id}
POST   /api/admin/trips/{id}/hide {reason} | /show
GET    /api/admin/trips/disputes?resolved=
POST   /api/admin/trips/bookings/{id}/resolve  { outcome: Delivered|Cancelled, note }
GET    /api/admin/trips/reports?handled=
POST   /api/admin/trips/reports/{id}/close
POST   /api/admin/trips/reviews/{id}/hide | /show
```

Index PostgreSQL de recherche : `(FromCountry, ToCountry, DepartureDate)` filtré sur `Status = 'Open'` (schéma `voyages`).

### Application et console

- **Application :**
  - accueil « Qui part au pays ? » ;
  - recherche ;
  - fiche trajet ;
  - réservation avec attestation ;
  - publication en 2 étapes ;
  - « Mes voyages et envois » ;
  - saisie du code ;
  - avis ;
  - alertes ;
  - bloc « Voyages » du profil (livraisons, note, badge, trajets à venir) ;
  - entrée dans « Mes activités ».
- **Console :** page « Voyages » avec les indicateurs, les itinéraires les plus publiés, les trajets (masquer ou rétablir, réservations, avis), les litiges à trancher et les signalements.

### Choix liés à la règle « ne pas modifier les modules existants »

| Besoin | V1 | Évolution nécessaire |
|---|---|---|
| Catégorie de notification « Voyages » désactivable | notifications en catégorie « System » | ajouter la catégorie au module Notifications et aux réglages de l'appli |
| Notification qui ouvre un trajet | ouvre la conversation ou le profil du voyageur (ses trajets y sont listés) | cible « Trip » dans Notifications et `notificationHref` |
| Suspension → trajets masqués | lecture horaire de l'export des membres (seul accès du contrat Identity à la suspension) | exposer `GetSuspendedAsync` ou un événement `MemberSuspended` dans Identity |
| Signalements | gérés dans le module Voyages, page console dédiée | cibles Trip et Booking dans le module Moderation si l'on veut une file unique |

## Version 2 (à faire)

1. **Demandes d'envoi publiques** (le sens inverse) : `ShipmentRequest` (`SenderId`, départ, arrivée, `WantedBefore`, poids, catégorie, description, statut). Un voyageur répond à une demande, ce qui crée une réservation sur son trajet. Routes `GET|POST /api/shipment-requests`.
2. **Vérification renforcée pour publier un trajet** : d'abord le téléphone par SMS (fournisseur à choisir, code haché, 5 essais, comme le code de remise). Ensuite une vérification d'identité par un prestataire KYC, avec un badge « Identité vérifiée ».
3. **Paiement avec séquestre** : un prestataire agréé (Mobile Money UEMOA ou carte) bloque le montant à l'acceptation, le libère au code de remise et prélève une commission. Encaisser pour le compte des membres est une activité réglementée, d'où le passage obligatoire par un prestataire agréé.
4. **Mise en avant payante** d'un trajet dans la recherche et sur l'accueil.
5. **Étape « Arrivé au pays »** : le voyageur signale son arrivée et l'expéditeur prévient son destinataire. Avec une catégorie de notifications « Voyages » désactivable.
6. **File de modération unique** : rapprocher les signalements de Voyages du module Moderation.

## Projet de section des CGU

> TODO JURIDIQUE : texte à faire relire par un juriste avant le lancement. Le transport de colis pour autrui engage la responsabilité des voyageurs.

**Voyages entre membres**

1. BéninConnect est un intermédiaire technique qui met en relation des membres. BéninConnect n'est ni transporteur, ni commissionnaire de transport, ni partie à l'accord conclu entre le voyageur et l'expéditeur.
2. Aucun paiement ne transite par BéninConnect. Le prix indiqué est indicatif et se règle directement entre membres.
3. Le voyageur est seul responsable des objets qu'il accepte de transporter et de leur déclaration en douane. Il s'engage à ouvrir et vérifier chaque colis à la remise et à refuser tout colis fermé ou douteux.
4. L'expéditeur certifie que le colis ne contient aucun objet interdit : stupéfiants, armes, argent liquide, médicaments sans ordonnance, contrefaçons, produits périssables, objets de valeur non déclarés. Il est responsable de l'exactitude de la description.
5. Chacun respecte les règles douanières des pays de départ et d'arrivée (liens officiels dans l'application).
6. En cas de litige, chaque partie peut le signaler dans l'application. L'équipe examine les éléments disponibles et peut confirmer la livraison, annuler la réservation, masquer un contenu ou suspendre un compte. L'équipe ne rembourse ni n'indemnise.
