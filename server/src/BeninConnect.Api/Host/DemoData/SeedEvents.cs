namespace BeninConnect.Host.DemoData;

/// <summary>25 événements fictifs, positionnés dans le futur relativement à la date de lancement.</summary>
internal static class SeedEvents
{
    public sealed record EventSeed(
        string Title,
        string Description,
        string Category,
        string City,
        string Country,
        string Address,
        int DaysFromNow,
        int StartHour,
        int DurationHours,
        int Organizer,
        string? Community,
        int Image,
        int? Capacity,
        int Participants,
        bool DemoParticipates);

    public static readonly string[] Categories =
        ["Culture", "Networking", "Gastronomie", "Sport", "Formation", "Entraide", "Famille"];

    public static readonly EventSeed[] All =
    [
        new("Dîner « Saveurs du Bénin »", "Menu découverte en cinq plats préparé par le chef Hermann Dansou : amiwo, sauce gombo, poisson braisé, ablo et dèguè. Places limitées.",
            "Gastronomie", "Paris", "France", "Restaurant de démonstration, 12 rue Fictive, 75011 Paris", 3, 20, 3, 13, "paris", 3, 40, 31, false),
        new("Meetup Tech Diaspora", "Démos de projets, retours d'expérience sur le recrutement tech en Europe et networking. Session diffusée en ligne.",
            "Networking", "Berlin", "Allemagne", "Espace de coworking (adresse communiquée aux inscrits)", 12, 18, 3, 9, "tech", 7, 80, 42, true),
        new("Pique-nique de rentrée", "Grand pique-nique convivial : chacun apporte un plat, on fournit la musique et les jeux pour les enfants.",
            "Famille", "Paris", "France", "Parc des Buttes-Chaumont, entrée rue Botzaris", 9, 12, 5, 0, "paris", 6, null, 54, true),
        new("Atelier CV et entretien", "Rendez-vous individuels de 20 minutes avec des recruteurs bénévoles. Apportez votre CV imprimé.",
            "Formation", "Paris", "France", "Maison des associations (démo), Paris 19e", 6, 14, 3, 36, "emploi", 14, 20, 17, false),
        new("Tournoi de football de la diaspora", "8 équipes, matchs de 2 × 20 minutes, finale en fin d'après-midi. Venez supporter !",
            "Sport", "Montréal", "Canada", "Parc de démonstration, terrain synthétique n° 2", 25, 10, 8, 29, "montreal", 13, null, 64, false),
        new("Soirée contes pour enfants", "Gérard Ewé raconte les contes de son enfance, suivis d'un goûter et d'une chanson en fon.",
            "Famille", "Paris", "France", "Médiathèque de démonstration, Paris 20e", 15, 15, 2, 39, "culture", 14, 30, 12, true),
        new("Webinaire : créer son entreprise au Bénin", "Les étapes de création à l'APIEx, les statuts OHADA et les erreurs à éviter quand on est à l'étranger.",
            "Formation", "En ligne", "", "Lien de visioconférence envoyé aux inscrits", 4, 19, 2, 8, "entrepreneurs", 7, 150, 87, false),
        new("Concert afrobeat & musique fon", "Landry Alladayè et ses musiciens en concert. Première partie : jeunes talents de la diaspora.",
            "Culture", "Bruxelles", "Belgique", "Salle de concert de démonstration, Ixelles", 18, 20, 4, 35, "bruxelles", 16, 200, 133, false),
        new("Afterwork des Béninois de Lyon", "Rencontre informelle après le travail pour faire connaissance et échanger les bons plans.",
            "Networking", "Lyon", "France", "Bar de démonstration, Lyon 7e", 2, 19, 3, 30, "lyon", 6, null, 18, false),
        new("Collecte de fournitures scolaires", "Point de dépôt pour la collecte au profit d'une école de Bohicon : cahiers, stylos, calculatrices.",
            "Entraide", "Lyon", "France", "Local associatif de démonstration, Croix-Rousse", 7, 10, 6, 2, "entraide", 14, null, 23, false),
        new("Atelier « Préparer son retour »", "Emploi, logement, scolarité, création d'entreprise : un atelier concret avec des membres déjà rentrés.",
            "Formation", "Paris", "France", "Salle de réunion de démonstration, Paris 10e", 21, 14, 3, 32, "retour", 12, 35, 21, false),
        new("Cours de cuisine : la pâte rouge", "Apprenez à réussir la pâte rouge et sa sauce tomate pimentée. Repas partagé à la fin.",
            "Gastronomie", "Ottawa", "Canada", "Cuisine partagée de démonstration, centre-ville", 11, 17, 3, 11, "cuisine", 3, 12, 9, false),
        new("Accueil des nouveaux arrivants", "Démarches, logement, transport, emploi : une soirée pour bien démarrer au Québec.",
            "Entraide", "Montréal", "Canada", "Centre communautaire de démonstration, Plateau", 5, 18, 2, 1, "canada", 9, 60, 28, false),
        new("Exposition photo « Dantokpa »", "Vernissage de la série photo consacrée au plus grand marché de Cotonou, en présence de la photographe.",
            "Culture", "Paris", "France", "Galerie de démonstration, Paris 3e", 28, 18, 3, 24, "culture", 4, null, 46, false),
        new("Journée « racines » à Ouidah", "Visite guidée de Ouidah pour les jeunes de la diaspora : route des esclaves, forêt sacrée, musée.",
            "Culture", "Ouidah", "Bénin", "Point de rendez-vous : place de démonstration, Ouidah", 45, 8, 9, 37, "retour", 15, 25, 14, false),
        new("Rencontre des étudiants béninois de Nantes", "Soirée d'intégration, présentation de l'association et jeux pour faire connaissance.",
            "Networking", "Nantes", "France", "Maison des étudiants (démo), Nantes", 8, 19, 3, 18, "etudiants-fr", 5, 80, 37, false),
        new("Permanence juridique gratuite", "Titres de séjour, regroupement familial, naturalisation : information juridique gratuite sur rendez-vous.",
            "Entraide", "Bruxelles", "Belgique", "Cabinet de démonstration, Bruxelles centre", 10, 10, 4, 22, "bruxelles", 14, 16, 7, false),
        new("Atelier portage et massage bébé", "Atelier animé par une sage-femme : portage, massage traditionnel et échanges entre parents.",
            "Famille", "Paris", "France", "Espace parents de démonstration, Paris 18e", 13, 10, 2, 20, "parents", 6, 6, 6, false),
        new("Hackathon « Fintech UEMOA »", "48 heures pour imaginer des solutions de paiement et de transfert pour la diaspora. Équipes de 3 à 5.",
            "Networking", "Londres", "Royaume-Uni", "Campus de démonstration, Shoreditch", 35, 9, 33, 27, "tech", 7, 100, 61, false),
        new("Séance de percussions ouverte", "Découverte des rythmes traditionnels du Bénin. Instruments prêtés, aucun niveau requis.",
            "Culture", "Bruxelles", "Belgique", "Studio de démonstration, Ixelles", 4, 15, 2, 3, "culture", 11, 20, 11, false),
        new("Fête de l'indépendance", "Célébration conviviale avec musique, danse et cuisine. Toute la famille est la bienvenue.",
            "Culture", "Paris", "France", "Salle des fêtes de démonstration, Montreuil", 60, 16, 7, 13, "paris", 16, 300, 112, false),
        new("Cours de soutien en maths (en ligne)", "Session hebdomadaire gratuite pour les élèves de terminale. Exercices de type bac.",
            "Formation", "En ligne", "", "Lien envoyé aux inscrits", 1, 18, 2, 10, "etudiants-fr", 14, 30, 19, false),
        new("Randonnée hivernale", "Raquettes et chocolat chaud : une sortie pour apprivoiser l'hiver québécois en groupe.",
            "Sport", "Québec", "Canada", "Parc de démonstration, Québec", 19, 9, 6, 6, "canada", 9, 25, 15, false),
        new("Brunch des entrepreneuses", "Échanges entre fondatrices de la diaspora : financement, production au Bénin, distribution en Europe.",
            "Networking", "Paris", "France", "Café de démonstration, Paris 11e", 16, 11, 3, 4, "entrepreneurs", 10, 20, 13, true),
        new("Atelier « oser sa reconversion »", "Bilan de compétences express et méthode en trois étapes pour préparer une reconversion.",
            "Formation", "Paris", "France", "Tiers-lieu de démonstration, Paris 12e", 23, 10, 3, 32, null, 12, 15, 11, false),
    ];
}
