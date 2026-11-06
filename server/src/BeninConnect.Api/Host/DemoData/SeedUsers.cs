namespace BeninConnect.Host.DemoData;

/// <summary>40 profils entièrement fictifs. Toute ressemblance avec des personnes réelles serait fortuite.</summary>
internal static class SeedUsers
{
    public sealed record UserSeed(
        string FirstName,
        string LastName,
        string City,
        string Country,
        string OriginCity,
        string Profession,
        string Bio,
        string[] Interests);

    public static readonly string[] Interests =
    [
        "Entrepreneuriat", "Tech & numérique", "Cuisine béninoise", "Musique", "Football", "Études",
        "Vie associative", "Voyages", "Patrimoine & culture", "Mode", "Littérature", "Investissement",
        "Santé & bien-être", "Photographie", "Danse", "Cinéma", "Agriculture", "Immobilier"
    ];

    public static readonly string[] AvatarColors =
    [
        "#087F5B", "#0E7490", "#B45309", "#7C3AED", "#BE123C", "#1D4ED8", "#15803D", "#C2410C",
        "#4338CA", "#0F766E", "#A21CAF", "#9A3412"
    ];

    // Index 0 : compte de démonstration (demo@beninconnect.test).
    public static readonly UserSeed[] All =
    [
        new("Sèna", "Houngbo", "Paris", "France", "Cotonou", "Cheffe de projet digital",
            "Arrivée à Paris pour mes études, restée pour le travail. J'aime relier les gens et les idées. Toujours partante pour un bon amiwo.",
            ["Tech & numérique", "Vie associative", "Cuisine béninoise", "Voyages"]),
        new("Kossi", "Agossou", "Montréal", "Canada", "Porto-Novo", "Ingénieur logiciel",
            "Développeur backend, passionné de transmission. Je mentore des étudiants qui arrivent au Québec.", ["Tech & numérique", "Études", "Football"]),
        new("Afi", "Dossou", "Lyon", "France", "Abomey", "Pharmacienne",
            "Pharmacienne hospitalière. Bénévole dans une association d'aide aux nouveaux arrivants.", ["Santé & bien-être", "Vie associative", "Littérature"]),
        new("Mawuli", "Adjovi", "Bruxelles", "Belgique", "Ouidah", "Consultant en stratégie",
            "Consultant le jour, percussionniste le week-end. Ouidah dans le cœur.", ["Musique", "Patrimoine & culture", "Entrepreneuriat"]),
        new("Ablawa", "Kpadonou", "Paris", "France", "Cotonou", "Fondatrice d'une marque de mode",
            "Je crée des pièces en wax et en tissus tissés au Bénin, fabriquées entre Cotonou et Paris.", ["Mode", "Entrepreneuriat", "Photographie"]),
        new("Rodrigue", "Ahouansou", "Toulouse", "France", "Parakou", "Étudiant en aéronautique",
            "Master en systèmes embarqués. Je cherche une alternance pour la rentrée prochaine.", ["Études", "Tech & numérique", "Football"]),
        new("Fifamè", "Gbaguidi", "Québec", "Canada", "Bohicon", "Infirmière",
            "Infirmière au CHU, maman de deux enfants. On apprivoise l'hiver ensemble !", ["Santé & bien-être", "Cuisine béninoise", "Vie associative"]),
        new("Gildas", "Hounkpatin", "Cotonou", "Bénin", "Cotonou", "Entrepreneur agritech",
            "Rentré au pays après 8 ans en France. Je développe une plateforme pour les producteurs d'ananas.", ["Agriculture", "Entrepreneuriat", "Investissement"]),
        new("Nadège", "Sossou", "Bordeaux", "France", "Lokossa", "Juriste en droit des affaires",
            "Juriste, j'accompagne les entrepreneurs dans leurs démarches entre la France et l'UEMOA.", ["Entrepreneuriat", "Littérature", "Voyages"]),
        new("Jospin", "Tossou", "Berlin", "Allemagne", "Natitingou", "Data scientist",
            "Data scientist dans une startup de mobilité. J'organise des meetups tech pour la diaspora en Allemagne.", ["Tech & numérique", "Investissement", "Cinéma"]),
        new("Rachida", "Yessoufou", "Marseille", "France", "Djougou", "Enseignante",
            "Professeure de mathématiques. Je donne des cours de soutien en ligne aux lycéens béninois.", ["Études", "Littérature", "Vie associative"]),
        new("Ulrich", "Akpovi", "Ottawa", "Canada", "Cotonou", "Analyste financier",
            "Finance le jour, cuisine le soir. Mon akassa sauce arachide a sa petite réputation.", ["Investissement", "Cuisine béninoise", "Football"]),
        new("Carine", "Codjia", "Lille", "France", "Porto-Novo", "Doctorante en sociologie",
            "Thèse sur les trajectoires de la diaspora ouest-africaine. Les échanges ici m'inspirent beaucoup.", ["Études", "Patrimoine & culture", "Littérature"]),
        new("Hermann", "Dansou", "Paris", "France", "Cotonou", "Chef cuisinier",
            "Chef d'un petit restaurant à Paris. Je revisite les classiques béninois sans les trahir.", ["Cuisine béninoise", "Entrepreneuriat", "Musique"]),
        new("Bénédicta", "Fagla", "Montréal", "Canada", "Abomey-Calavi", "Designer UX",
            "Designer produit. J'adore les projets qui simplifient la vie des familles entre deux pays.", ["Tech & numérique", "Photographie", "Mode"]),
        new("Arnaud", "Quenum", "Lyon", "France", "Ouidah", "Responsable logistique",
            "Logistique internationale, spécialiste des envois vers l'Afrique de l'Ouest. Pose-moi tes questions !", ["Entrepreneuriat", "Voyages", "Football"]),
        new("Chimène", "Ahyi", "Genève", "Suisse", "Cotonou", "Chargée de programmes ONG",
            "Je travaille sur des projets d'accès à l'eau. Fière de mes racines, curieuse du monde.", ["Vie associative", "Voyages", "Santé & bien-être"]),
        new("Romaric", "Amoussou", "Paris", "France", "Bohicon", "Photographe",
            "Photographe de mariage et de portraits. Je documente la vie de la diaspora à Paris.", ["Photographie", "Patrimoine & culture", "Cinéma"]),
        new("Prisca", "Assogba", "Nantes", "France", "Cotonou", "Étudiante en commerce",
            "Bachelor en commerce international. Présidente de l'association des étudiants béninois de Nantes.", ["Études", "Vie associative", "Danse"]),
        new("Fabrice", "Bocovo", "Washington", "États-Unis", "Porto-Novo", "Économiste",
            "Économiste du développement. Je suis de près les politiques d'investissement en Afrique de l'Ouest.", ["Investissement", "Littérature", "Voyages"]),
        new("Mireille", "Chabi", "Paris", "France", "Parakou", "Sage-femme",
            "Sage-femme libérale. J'anime des ateliers pour les futures mamans de la diaspora.", ["Santé & bien-être", "Vie associative", "Cuisine béninoise"]),
        new("Judicaël", "Dégbé", "Montréal", "Canada", "Cotonou", "Étudiant en génie civil",
            "Arrivé en août, je découvre Montréal. Les conseils sur le logement sont les bienvenus !", ["Études", "Football", "Musique"]),
        new("Laure", "Djossou", "Bruxelles", "Belgique", "Cotonou", "Avocate",
            "Avocate en droit des étrangers. J'organise des permanences d'information gratuites.", ["Vie associative", "Littérature", "Danse"]),
        new("Yannick", "Gnonlonfoun", "Lyon", "France", "Abomey", "Développeur mobile",
            "Développeur mobile, je construis des applis pour les commerçants de Dantokpa.", ["Tech & numérique", "Entrepreneuriat", "Musique"]),
        new("Ornella", "Hounsou", "Paris", "France", "Cotonou", "Journaliste",
            "Journaliste culture. Je raconte les artistes béninois d'ici et d'ailleurs.", ["Patrimoine & culture", "Cinéma", "Musique"]),
        new("Serge", "Kiki", "Cotonou", "Bénin", "Cotonou", "Promoteur immobilier",
            "J'accompagne les membres de la diaspora qui veulent construire au pays, en toute transparence.", ["Immobilier", "Investissement", "Entrepreneuriat"]),
        new("Esther", "Lokossou", "Strasbourg", "France", "Lokossa", "Étudiante en médecine",
            "Externe en 5e année. Je partage mes fiches et mon organisation avec ceux qui commencent.", ["Études", "Santé & bien-être", "Littérature"]),
        new("Brice", "Mensah", "Londres", "Royaume-Uni", "Porto-Novo", "Chef de produit fintech",
            "Fintech à Londres. Les transferts d'argent vers le Bénin devraient être simples et pas chers.", ["Tech & numérique", "Investissement", "Football"]),
        new("Josiane", "Noudéhou", "Paris", "France", "Allada", "Comptable",
            "Expert-comptable stagiaire. J'aide bénévolement les associations à tenir leurs comptes.", ["Vie associative", "Entrepreneuriat", "Cuisine béninoise"]),
        new("Wilfried", "Oké", "Montréal", "Canada", "Savè", "Technicien en maintenance",
            "Passionné de football, j'organise un tournoi de la diaspora chaque été.", ["Football", "Vie associative", "Musique"]),
        new("Aurore", "Padonou", "Lyon", "France", "Cotonou", "Architecte",
            "Architecte, je m'intéresse aux matériaux locaux et à la construction en terre.", ["Immobilier", "Patrimoine & culture", "Photographie"]),
        new("Steve", "Sagbo", "Francfort", "Allemagne", "Cotonou", "Ingénieur mécanique",
            "Ingénieur chez un équipementier. En Allemagne depuis 6 ans, je parle enfin allemand sans stress.", ["Tech & numérique", "Voyages", "Football"]),
        new("Murielle", "Tchibozo", "Paris", "France", "Ouidah", "Coach en reconversion",
            "J'accompagne les reconversions professionnelles, notamment les retours au pays.", ["Entrepreneuriat", "Santé & bien-être", "Danse"]),
        new("Cédric", "Vodounon", "Toulouse", "France", "Abomey", "Pilote de ligne",
            "Pilote, toujours entre deux avions. Cotonou reste ma destination préférée.", ["Voyages", "Photographie", "Musique"]),
        new("Grâce", "Zannou", "Québec", "Canada", "Cotonou", "Étudiante en informatique",
            "Étudiante en informatique à l'université Laval. Je cherche un stage d'été en développement web.", ["Études", "Tech & numérique", "Danse"]),
        new("Landry", "Alladayè", "Bruxelles", "Belgique", "Abomey", "Musicien",
            "Guitariste et compositeur, entre afrobeat et musique traditionnelle fon.", ["Musique", "Patrimoine & culture", "Cinéma"]),
        new("Sylvie", "Badou", "Paris", "France", "Porto-Novo", "Responsable RH",
            "RH dans un grand groupe. Je relis volontiers vos CV et lettres de motivation.", ["Vie associative", "Littérature", "Cuisine béninoise"]),
        new("Euloge", "Cakpo", "Cotonou", "Bénin", "Ganvié", "Guide touristique",
            "Guide à Ganvié et Ouidah. Je fais découvrir le pays aux enfants de la diaspora.", ["Voyages", "Patrimoine & culture", "Photographie"]),
        new("Nafissatou", "Dah", "Rennes", "France", "Kandi", "Ingénieure agronome",
            "Agronome, je travaille sur les filières karité et soja du nord Bénin.", ["Agriculture", "Entrepreneuriat", "Santé & bien-être"]),
        new("Gérard", "Ewé", "Paris", "France", "Cotonou", "Retraité, ancien enseignant",
            "Ancien instituteur, installé à Paris depuis 1988. J'aime raconter le Bénin d'hier aux plus jeunes.", ["Littérature", "Patrimoine & culture", "Vie associative"]),
    ];
}
