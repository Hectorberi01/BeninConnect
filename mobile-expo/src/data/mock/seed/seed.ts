// Généré depuis src/BeninConnect.App/MockData/Seed (même jeu de démonstration que la version Avalonia).
// Toutes les personnes, communautés et événements sont fictifs.

export interface UserSeed { firstName: string; lastName: string; city: string; country: string; originCity: string; profession: string; bio: string; interests: string[] }
export interface CommunitySeed { key: string; name: string; description: string; category: string; city: string; country: string; visibility: 'Public' | 'Private'; ownerIndex: number; members: number[]; demoIsMember: boolean }
export interface PostSeed { author: number; community: string | null; image: number; hoursAgo: number; text: string }
export interface ConversationSeed { otherUser: number; hoursAgo: number; unreadCount: number; lines: string[] }
export interface EventSeed { title: string; description: string; category: string; city: string; country: string; address: string; daysFromNow: number; startHour: number; durationHours: number; organizer: number; community: string | null; image: number; capacity: number | null; participants: number; demoParticipates: boolean }

export const AVATAR_COLORS: string[] = [
  "#087F5B",
  "#0E7490",
  "#B45309",
  "#7C3AED",
  "#BE123C",
  "#1D4ED8",
  "#15803D",
  "#C2410C",
  "#4338CA",
  "#0F766E",
  "#A21CAF",
  "#9A3412"
];

export const SEED_USERS: UserSeed[] = [
  {
    "firstName": "Sèna",
    "lastName": "Houngbo",
    "city": "Paris",
    "country": "France",
    "originCity": "Cotonou",
    "profession": "Cheffe de projet digital",
    "bio": "Arrivée à Paris pour mes études, restée pour le travail. J'aime relier les gens et les idées. Toujours partante pour un bon amiwo.",
    "interests": [
      "Tech & numérique",
      "Vie associative",
      "Cuisine béninoise",
      "Voyages"
    ]
  },
  {
    "firstName": "Kossi",
    "lastName": "Agossou",
    "city": "Montréal",
    "country": "Canada",
    "originCity": "Porto-Novo",
    "profession": "Ingénieur logiciel",
    "bio": "Développeur backend, passionné de transmission. Je mentore des étudiants qui arrivent au Québec.",
    "interests": [
      "Tech & numérique",
      "Études",
      "Football"
    ]
  },
  {
    "firstName": "Afi",
    "lastName": "Dossou",
    "city": "Lyon",
    "country": "France",
    "originCity": "Abomey",
    "profession": "Pharmacienne",
    "bio": "Pharmacienne hospitalière. Bénévole dans une association d'aide aux nouveaux arrivants.",
    "interests": [
      "Santé & bien-être",
      "Vie associative",
      "Littérature"
    ]
  },
  {
    "firstName": "Mawuli",
    "lastName": "Adjovi",
    "city": "Bruxelles",
    "country": "Belgique",
    "originCity": "Ouidah",
    "profession": "Consultant en stratégie",
    "bio": "Consultant le jour, percussionniste le week-end. Ouidah dans le cœur.",
    "interests": [
      "Musique",
      "Patrimoine & culture",
      "Entrepreneuriat"
    ]
  },
  {
    "firstName": "Ablawa",
    "lastName": "Kpadonou",
    "city": "Paris",
    "country": "France",
    "originCity": "Cotonou",
    "profession": "Fondatrice d'une marque de mode",
    "bio": "Je crée des pièces en wax et en tissus tissés au Bénin, fabriquées entre Cotonou et Paris.",
    "interests": [
      "Mode",
      "Entrepreneuriat",
      "Photographie"
    ]
  },
  {
    "firstName": "Rodrigue",
    "lastName": "Ahouansou",
    "city": "Toulouse",
    "country": "France",
    "originCity": "Parakou",
    "profession": "Étudiant en aéronautique",
    "bio": "Master en systèmes embarqués. Je cherche une alternance pour la rentrée prochaine.",
    "interests": [
      "Études",
      "Tech & numérique",
      "Football"
    ]
  },
  {
    "firstName": "Fifamè",
    "lastName": "Gbaguidi",
    "city": "Québec",
    "country": "Canada",
    "originCity": "Bohicon",
    "profession": "Infirmière",
    "bio": "Infirmière au CHU, maman de deux enfants. On apprivoise l'hiver ensemble !",
    "interests": [
      "Santé & bien-être",
      "Cuisine béninoise",
      "Vie associative"
    ]
  },
  {
    "firstName": "Gildas",
    "lastName": "Hounkpatin",
    "city": "Cotonou",
    "country": "Bénin",
    "originCity": "Cotonou",
    "profession": "Entrepreneur agritech",
    "bio": "Rentré au pays après 8 ans en France. Je développe une plateforme pour les producteurs d'ananas.",
    "interests": [
      "Agriculture",
      "Entrepreneuriat",
      "Investissement"
    ]
  },
  {
    "firstName": "Nadège",
    "lastName": "Sossou",
    "city": "Bordeaux",
    "country": "France",
    "originCity": "Lokossa",
    "profession": "Juriste en droit des affaires",
    "bio": "Juriste, j'accompagne les entrepreneurs dans leurs démarches entre la France et l'UEMOA.",
    "interests": [
      "Entrepreneuriat",
      "Littérature",
      "Voyages"
    ]
  },
  {
    "firstName": "Jospin",
    "lastName": "Tossou",
    "city": "Berlin",
    "country": "Allemagne",
    "originCity": "Natitingou",
    "profession": "Data scientist",
    "bio": "Data scientist dans une startup de mobilité. J'organise des meetups tech pour la diaspora en Allemagne.",
    "interests": [
      "Tech & numérique",
      "Investissement",
      "Cinéma"
    ]
  },
  {
    "firstName": "Rachida",
    "lastName": "Yessoufou",
    "city": "Marseille",
    "country": "France",
    "originCity": "Djougou",
    "profession": "Enseignante",
    "bio": "Professeure de mathématiques. Je donne des cours de soutien en ligne aux lycéens béninois.",
    "interests": [
      "Études",
      "Littérature",
      "Vie associative"
    ]
  },
  {
    "firstName": "Ulrich",
    "lastName": "Akpovi",
    "city": "Ottawa",
    "country": "Canada",
    "originCity": "Cotonou",
    "profession": "Analyste financier",
    "bio": "Finance le jour, cuisine le soir. Mon akassa sauce arachide a sa petite réputation.",
    "interests": [
      "Investissement",
      "Cuisine béninoise",
      "Football"
    ]
  },
  {
    "firstName": "Carine",
    "lastName": "Codjia",
    "city": "Lille",
    "country": "France",
    "originCity": "Porto-Novo",
    "profession": "Doctorante en sociologie",
    "bio": "Thèse sur les trajectoires de la diaspora ouest-africaine. Les échanges ici m'inspirent beaucoup.",
    "interests": [
      "Études",
      "Patrimoine & culture",
      "Littérature"
    ]
  },
  {
    "firstName": "Hermann",
    "lastName": "Dansou",
    "city": "Paris",
    "country": "France",
    "originCity": "Cotonou",
    "profession": "Chef cuisinier",
    "bio": "Chef d'un petit restaurant à Paris. Je revisite les classiques béninois sans les trahir.",
    "interests": [
      "Cuisine béninoise",
      "Entrepreneuriat",
      "Musique"
    ]
  },
  {
    "firstName": "Bénédicta",
    "lastName": "Fagla",
    "city": "Montréal",
    "country": "Canada",
    "originCity": "Abomey-Calavi",
    "profession": "Designer UX",
    "bio": "Designer produit. J'adore les projets qui simplifient la vie des familles entre deux pays.",
    "interests": [
      "Tech & numérique",
      "Photographie",
      "Mode"
    ]
  },
  {
    "firstName": "Arnaud",
    "lastName": "Quenum",
    "city": "Lyon",
    "country": "France",
    "originCity": "Ouidah",
    "profession": "Responsable logistique",
    "bio": "Logistique internationale, spécialiste des envois vers l'Afrique de l'Ouest. Pose-moi tes questions !",
    "interests": [
      "Entrepreneuriat",
      "Voyages",
      "Football"
    ]
  },
  {
    "firstName": "Chimène",
    "lastName": "Ahyi",
    "city": "Genève",
    "country": "Suisse",
    "originCity": "Cotonou",
    "profession": "Chargée de programmes ONG",
    "bio": "Je travaille sur des projets d'accès à l'eau. Fière de mes racines, curieuse du monde.",
    "interests": [
      "Vie associative",
      "Voyages",
      "Santé & bien-être"
    ]
  },
  {
    "firstName": "Romaric",
    "lastName": "Amoussou",
    "city": "Paris",
    "country": "France",
    "originCity": "Bohicon",
    "profession": "Photographe",
    "bio": "Photographe de mariage et de portraits. Je documente la vie de la diaspora à Paris.",
    "interests": [
      "Photographie",
      "Patrimoine & culture",
      "Cinéma"
    ]
  },
  {
    "firstName": "Prisca",
    "lastName": "Assogba",
    "city": "Nantes",
    "country": "France",
    "originCity": "Cotonou",
    "profession": "Étudiante en commerce",
    "bio": "Bachelor en commerce international. Présidente de l'association des étudiants béninois de Nantes.",
    "interests": [
      "Études",
      "Vie associative",
      "Danse"
    ]
  },
  {
    "firstName": "Fabrice",
    "lastName": "Bocovo",
    "city": "Washington",
    "country": "États-Unis",
    "originCity": "Porto-Novo",
    "profession": "Économiste",
    "bio": "Économiste du développement. Je suis de près les politiques d'investissement en Afrique de l'Ouest.",
    "interests": [
      "Investissement",
      "Littérature",
      "Voyages"
    ]
  },
  {
    "firstName": "Mireille",
    "lastName": "Chabi",
    "city": "Paris",
    "country": "France",
    "originCity": "Parakou",
    "profession": "Sage-femme",
    "bio": "Sage-femme libérale. J'anime des ateliers pour les futures mamans de la diaspora.",
    "interests": [
      "Santé & bien-être",
      "Vie associative",
      "Cuisine béninoise"
    ]
  },
  {
    "firstName": "Judicaël",
    "lastName": "Dégbé",
    "city": "Montréal",
    "country": "Canada",
    "originCity": "Cotonou",
    "profession": "Étudiant en génie civil",
    "bio": "Arrivé en août, je découvre Montréal. Les conseils sur le logement sont les bienvenus !",
    "interests": [
      "Études",
      "Football",
      "Musique"
    ]
  },
  {
    "firstName": "Laure",
    "lastName": "Djossou",
    "city": "Bruxelles",
    "country": "Belgique",
    "originCity": "Cotonou",
    "profession": "Avocate",
    "bio": "Avocate en droit des étrangers. J'organise des permanences d'information gratuites.",
    "interests": [
      "Vie associative",
      "Littérature",
      "Danse"
    ]
  },
  {
    "firstName": "Yannick",
    "lastName": "Gnonlonfoun",
    "city": "Lyon",
    "country": "France",
    "originCity": "Abomey",
    "profession": "Développeur mobile",
    "bio": "Développeur mobile, je construis des applis pour les commerçants de Dantokpa.",
    "interests": [
      "Tech & numérique",
      "Entrepreneuriat",
      "Musique"
    ]
  },
  {
    "firstName": "Ornella",
    "lastName": "Hounsou",
    "city": "Paris",
    "country": "France",
    "originCity": "Cotonou",
    "profession": "Journaliste",
    "bio": "Journaliste culture. Je raconte les artistes béninois d'ici et d'ailleurs.",
    "interests": [
      "Patrimoine & culture",
      "Cinéma",
      "Musique"
    ]
  },
  {
    "firstName": "Serge",
    "lastName": "Kiki",
    "city": "Cotonou",
    "country": "Bénin",
    "originCity": "Cotonou",
    "profession": "Promoteur immobilier",
    "bio": "J'accompagne les membres de la diaspora qui veulent construire au pays, en toute transparence.",
    "interests": [
      "Immobilier",
      "Investissement",
      "Entrepreneuriat"
    ]
  },
  {
    "firstName": "Esther",
    "lastName": "Lokossou",
    "city": "Strasbourg",
    "country": "France",
    "originCity": "Lokossa",
    "profession": "Étudiante en médecine",
    "bio": "Externe en 5e année. Je partage mes fiches et mon organisation avec ceux qui commencent.",
    "interests": [
      "Études",
      "Santé & bien-être",
      "Littérature"
    ]
  },
  {
    "firstName": "Brice",
    "lastName": "Mensah",
    "city": "Londres",
    "country": "Royaume-Uni",
    "originCity": "Porto-Novo",
    "profession": "Chef de produit fintech",
    "bio": "Fintech à Londres. Les transferts d'argent vers le Bénin devraient être simples et pas chers.",
    "interests": [
      "Tech & numérique",
      "Investissement",
      "Football"
    ]
  },
  {
    "firstName": "Josiane",
    "lastName": "Noudéhou",
    "city": "Paris",
    "country": "France",
    "originCity": "Allada",
    "profession": "Comptable",
    "bio": "Expert-comptable stagiaire. J'aide bénévolement les associations à tenir leurs comptes.",
    "interests": [
      "Vie associative",
      "Entrepreneuriat",
      "Cuisine béninoise"
    ]
  },
  {
    "firstName": "Wilfried",
    "lastName": "Oké",
    "city": "Montréal",
    "country": "Canada",
    "originCity": "Savè",
    "profession": "Technicien en maintenance",
    "bio": "Passionné de football, j'organise un tournoi de la diaspora chaque été.",
    "interests": [
      "Football",
      "Vie associative",
      "Musique"
    ]
  },
  {
    "firstName": "Aurore",
    "lastName": "Padonou",
    "city": "Lyon",
    "country": "France",
    "originCity": "Cotonou",
    "profession": "Architecte",
    "bio": "Architecte, je m'intéresse aux matériaux locaux et à la construction en terre.",
    "interests": [
      "Immobilier",
      "Patrimoine & culture",
      "Photographie"
    ]
  },
  {
    "firstName": "Steve",
    "lastName": "Sagbo",
    "city": "Francfort",
    "country": "Allemagne",
    "originCity": "Cotonou",
    "profession": "Ingénieur mécanique",
    "bio": "Ingénieur chez un équipementier. En Allemagne depuis 6 ans, je parle enfin allemand sans stress.",
    "interests": [
      "Tech & numérique",
      "Voyages",
      "Football"
    ]
  },
  {
    "firstName": "Murielle",
    "lastName": "Tchibozo",
    "city": "Paris",
    "country": "France",
    "originCity": "Ouidah",
    "profession": "Coach en reconversion",
    "bio": "J'accompagne les reconversions professionnelles, notamment les retours au pays.",
    "interests": [
      "Entrepreneuriat",
      "Santé & bien-être",
      "Danse"
    ]
  },
  {
    "firstName": "Cédric",
    "lastName": "Vodounon",
    "city": "Toulouse",
    "country": "France",
    "originCity": "Abomey",
    "profession": "Pilote de ligne",
    "bio": "Pilote, toujours entre deux avions. Cotonou reste ma destination préférée.",
    "interests": [
      "Voyages",
      "Photographie",
      "Musique"
    ]
  },
  {
    "firstName": "Grâce",
    "lastName": "Zannou",
    "city": "Québec",
    "country": "Canada",
    "originCity": "Cotonou",
    "profession": "Étudiante en informatique",
    "bio": "Étudiante en informatique à l'université Laval. Je cherche un stage d'été en développement web.",
    "interests": [
      "Études",
      "Tech & numérique",
      "Danse"
    ]
  },
  {
    "firstName": "Landry",
    "lastName": "Alladayè",
    "city": "Bruxelles",
    "country": "Belgique",
    "originCity": "Abomey",
    "profession": "Musicien",
    "bio": "Guitariste et compositeur, entre afrobeat et musique traditionnelle fon.",
    "interests": [
      "Musique",
      "Patrimoine & culture",
      "Cinéma"
    ]
  },
  {
    "firstName": "Sylvie",
    "lastName": "Badou",
    "city": "Paris",
    "country": "France",
    "originCity": "Porto-Novo",
    "profession": "Responsable RH",
    "bio": "RH dans un grand groupe. Je relis volontiers vos CV et lettres de motivation.",
    "interests": [
      "Vie associative",
      "Littérature",
      "Cuisine béninoise"
    ]
  },
  {
    "firstName": "Euloge",
    "lastName": "Cakpo",
    "city": "Cotonou",
    "country": "Bénin",
    "originCity": "Ganvié",
    "profession": "Guide touristique",
    "bio": "Guide à Ganvié et Ouidah. Je fais découvrir le pays aux enfants de la diaspora.",
    "interests": [
      "Voyages",
      "Patrimoine & culture",
      "Photographie"
    ]
  },
  {
    "firstName": "Nafissatou",
    "lastName": "Dah",
    "city": "Rennes",
    "country": "France",
    "originCity": "Kandi",
    "profession": "Ingénieure agronome",
    "bio": "Agronome, je travaille sur les filières karité et soja du nord Bénin.",
    "interests": [
      "Agriculture",
      "Entrepreneuriat",
      "Santé & bien-être"
    ]
  },
  {
    "firstName": "Gérard",
    "lastName": "Ewé",
    "city": "Paris",
    "country": "France",
    "originCity": "Cotonou",
    "profession": "Retraité, ancien enseignant",
    "bio": "Ancien instituteur, installé à Paris depuis 1988. J'aime raconter le Bénin d'hier aux plus jeunes.",
    "interests": [
      "Littérature",
      "Patrimoine & culture",
      "Vie associative"
    ]
  }
];

export const COMMUNITY_CATEGORIES: string[] = [
  "Villes & régions",
  "Études",
  "Entrepreneuriat",
  "Culture",
  "Emploi",
  "Entraide",
  "Tech",
  "Gastronomie",
  "Famille"
];

export const SEED_COMMUNITIES: CommunitySeed[] = [
  {
    "key": "paris",
    "name": "Béninois de Paris",
    "description": "Le point de rencontre des Béninoises et Béninois d'Île-de-France : sorties, bons plans, entraide et événements.",
    "category": "Villes & régions",
    "city": "Paris",
    "country": "France",
    "visibility": "Public",
    "ownerIndex": 13,
    "members": [
      4,
      13,
      17,
      20,
      24,
      28,
      32,
      36,
      39,
      3,
      8,
      2
    ],
    "demoIsMember": true
  },
  {
    "key": "canada",
    "name": "Diaspora béninoise au Canada",
    "description": "Installation, démarches, emploi et vie quotidienne au Canada, d'un océan à l'autre.",
    "category": "Villes & régions",
    "city": "",
    "country": "Canada",
    "visibility": "Public",
    "ownerIndex": 1,
    "members": [
      1,
      6,
      11,
      14,
      21,
      29,
      34
    ],
    "demoIsMember": false
  },
  {
    "key": "etudiants-fr",
    "name": "Étudiants béninois en France",
    "description": "Campus France, logement, jobs étudiants, stages : on s'entraide pour réussir ses études.",
    "category": "Études",
    "city": "",
    "country": "France",
    "visibility": "Public",
    "ownerIndex": 18,
    "members": [
      5,
      10,
      12,
      18,
      26,
      34,
      21,
      35
    ],
    "demoIsMember": true
  },
  {
    "key": "entrepreneurs",
    "name": "Entrepreneurs béninois d'Europe",
    "description": "Cercle privé de fondatrices et fondateurs : retours d'expérience, financement, partenariats.",
    "category": "Entrepreneuriat",
    "city": "",
    "country": "Europe",
    "visibility": "Private",
    "ownerIndex": 4,
    "members": [
      4,
      7,
      8,
      13,
      23,
      25,
      27,
      32,
      38
    ],
    "demoIsMember": true
  },
  {
    "key": "montreal",
    "name": "Béninois de Montréal",
    "description": "Communauté locale de Montréal : accueil des nouveaux arrivants, sorties et soirées culturelles.",
    "category": "Villes & régions",
    "city": "Montréal",
    "country": "Canada",
    "visibility": "Public",
    "ownerIndex": 29,
    "members": [
      1,
      14,
      21,
      29
    ],
    "demoIsMember": false
  },
  {
    "key": "culture",
    "name": "Culture et traditions du Bénin",
    "description": "Histoire, langues, musiques, fêtes et savoir-faire : transmettre et célébrer notre patrimoine.",
    "category": "Culture",
    "city": "",
    "country": "International",
    "visibility": "Public",
    "ownerIndex": 24,
    "members": [
      3,
      12,
      24,
      30,
      35,
      37,
      39,
      17
    ],
    "demoIsMember": true
  },
  {
    "key": "emploi",
    "name": "Opportunités professionnelles",
    "description": "Offres d'emploi, stages, alternances et conseils carrière partagés par les membres.",
    "category": "Emploi",
    "city": "",
    "country": "International",
    "visibility": "Public",
    "ownerIndex": 36,
    "members": [
      36,
      9,
      15,
      19,
      27,
      31,
      5,
      34
    ],
    "demoIsMember": false
  },
  {
    "key": "entraide",
    "name": "Solidarité et entraide",
    "description": "Un coup de main pour un déménagement, une démarche, un envoi au pays ? Demandez, proposez.",
    "category": "Entraide",
    "city": "",
    "country": "International",
    "visibility": "Public",
    "ownerIndex": 2,
    "members": [
      2,
      6,
      16,
      20,
      22,
      28,
      15
    ],
    "demoIsMember": false
  },
  {
    "key": "lyon",
    "name": "Béninois de Lyon",
    "description": "La communauté lyonnaise : rencontres, sport, cuisine et entraide entre Rhône et Saône.",
    "category": "Villes & régions",
    "city": "Lyon",
    "country": "France",
    "visibility": "Public",
    "ownerIndex": 15,
    "members": [
      2,
      15,
      23,
      30
    ],
    "demoIsMember": false
  },
  {
    "key": "retour",
    "name": "Retour et installation au Bénin",
    "description": "Préparer son retour : emploi, logement, création d'entreprise, scolarité des enfants.",
    "category": "Entrepreneuriat",
    "city": "Cotonou",
    "country": "Bénin",
    "visibility": "Public",
    "ownerIndex": 7,
    "members": [
      7,
      25,
      32,
      37,
      19,
      8
    ],
    "demoIsMember": false
  },
  {
    "key": "bruxelles",
    "name": "Béninois de Bruxelles",
    "description": "Belgique et Benelux : événements, démarches administratives et rencontres conviviales.",
    "category": "Villes & régions",
    "city": "Bruxelles",
    "country": "Belgique",
    "visibility": "Public",
    "ownerIndex": 22,
    "members": [
      3,
      22,
      35
    ],
    "demoIsMember": false
  },
  {
    "key": "tech",
    "name": "Tech & Digital Bénin",
    "description": "Développeurs, designers, data et produit : projets, meetups et opportunités dans la tech.",
    "category": "Tech",
    "city": "",
    "country": "International",
    "visibility": "Public",
    "ownerIndex": 9,
    "members": [
      1,
      9,
      14,
      23,
      27,
      31,
      34
    ],
    "demoIsMember": true
  },
  {
    "key": "cuisine",
    "name": "Cuisine béninoise",
    "description": "Recettes, adresses et astuces pour trouver les ingrédients du pays où que vous soyez.",
    "category": "Gastronomie",
    "city": "",
    "country": "International",
    "visibility": "Public",
    "ownerIndex": 13,
    "members": [
      11,
      13,
      20,
      28,
      6,
      36
    ],
    "demoIsMember": false
  },
  {
    "key": "parents",
    "name": "Parents de la diaspora",
    "description": "Groupe privé pour échanger sur l'éducation, la transmission des langues et la double culture.",
    "category": "Famille",
    "city": "",
    "country": "International",
    "visibility": "Private",
    "ownerIndex": 6,
    "members": [
      6,
      20,
      39,
      2
    ],
    "demoIsMember": false
  },
  {
    "key": "allemagne",
    "name": "Béninois d'Allemagne",
    "description": "Vivre, étudier et travailler en Allemagne : langue, visas, logement et rencontres.",
    "category": "Villes & régions",
    "city": "Berlin",
    "country": "Allemagne",
    "visibility": "Public",
    "ownerIndex": 9,
    "members": [
      9,
      31
    ],
    "demoIsMember": false
  }
];

export const SEED_POSTS: PostSeed[] = [
  {
    "author": 13,
    "community": "paris",
    "image": 3,
    "hoursAgo": 2,
    "text": "Ce samedi au restaurant, on sert un menu spécial : amiwo, poulet bicyclette et sauce gombo. Réservation conseillée, on a été débordés la dernière fois 😅"
  },
  {
    "author": 1,
    "community": "canada",
    "image": 9,
    "hoursAgo": 3,
    "text": "Premier hiver pour beaucoup d'entre vous. Petit rappel : un bon manteau, des bottes imperméables et surtout… ne restez pas seuls. On fait une soirée jeux dimanche, tout le monde est invité."
  },
  {
    "author": 5,
    "community": "etudiants-fr",
    "image": 0,
    "hoursAgo": 4,
    "text": "Quelqu'un a déjà fait une demande de titre de séjour étudiant en ligne cette année ? Mon dossier est « en cours d'instruction » depuis 6 semaines, c'est normal ?"
  },
  {
    "author": 4,
    "community": "entrepreneurs",
    "image": 10,
    "hoursAgo": 5,
    "text": "Nouvelle collection en préparation ! Les tissus viennent d'un atelier de tissage à Abomey. Je cherche une ou deux personnes pour tester les tailles, intéressées ?"
  },
  {
    "author": 24,
    "community": "culture",
    "image": 11,
    "hoursAgo": 6,
    "text": "Interview à paraître : un groupe de jeunes musiciens qui mélangent tchinkoumè et électro à Bruxelles. Le résultat est bluffant. Lien bientôt !"
  },
  {
    "author": 9,
    "community": "tech",
    "image": 7,
    "hoursAgo": 7,
    "text": "Meetup Tech Diaspora à Berlin le mois prochain : retours d'expérience sur le recrutement en Allemagne + atelier CV en anglais. Qui serait partant pour une session en ligne aussi ?"
  },
  {
    "author": 7,
    "community": "retour",
    "image": 12,
    "hoursAgo": 8,
    "text": "Un an après mon retour à Cotonou : ce qui a été le plus dur, ce n'est pas le business, c'est de réapprendre le rythme. Mais je ne regrette rien. Je réponds à vos questions en commentaire."
  },
  {
    "author": 36,
    "community": "emploi",
    "image": 0,
    "hoursAgo": 9,
    "text": "Offre : mon entreprise recrute 2 alternants en contrôle de gestion à Paris La Défense, rentrée de septembre. Envoyez-moi un message privé avec votre CV."
  },
  {
    "author": 2,
    "community": "entraide",
    "image": 0,
    "hoursAgo": 10,
    "text": "Je déménage samedi à Villeurbanne et j'ai besoin de deux paires de bras pendant 2 heures. Pizzas et boissons offertes 🙏"
  },
  {
    "author": 0,
    "community": "paris",
    "image": 2,
    "hoursAgo": 11,
    "text": "Quel bonheur de retrouver autant de monde au pique-nique des Buttes-Chaumont dimanche ! Merci à toutes celles et ceux qui ont apporté à manger. Prochain rendez-vous déjà en préparation."
  },
  {
    "author": 11,
    "community": "cuisine",
    "image": 3,
    "hoursAgo": 12,
    "text": "Ma recette d'akassa sauce arachide version « cuisine de 9 m² » est en ligne. Astuce : la pâte d'arachide se trouve facilement dans les épiceries africaines de Château-Rouge."
  },
  {
    "author": 20,
    "community": "parents",
    "image": 0,
    "hoursAgo": 13,
    "text": "Comment faites-vous pour transmettre le fon ou le yoruba aux enfants nés ici ? Chez nous, on essaie le « dimanche en fon », avec des hauts et des bas."
  },
  {
    "author": 14,
    "community": "montreal",
    "image": 0,
    "hoursAgo": 14,
    "text": "Pour les nouveaux arrivants : la carte OPUS à tarif réduit vaut vraiment le coup si vous êtes étudiant. Et l'application Transit est indispensable."
  },
  {
    "author": 27,
    "community": "tech",
    "image": 7,
    "hoursAgo": 15,
    "text": "On lance une bêta de notre appli de transfert d'argent vers le Bénin avec des frais réduits. Je cherche 20 testeurs dans la diaspora au Royaume-Uni et en France."
  },
  {
    "author": 17,
    "community": null,
    "image": 1,
    "hoursAgo": 16,
    "text": "Coucher de soleil sur la plage de Fidjrossè lors de mon dernier séjour. Il y a des lumières qu'on ne trouve qu'à la maison."
  },
  {
    "author": 12,
    "community": "etudiants-fr",
    "image": 14,
    "hoursAgo": 17,
    "text": "Je cherche des personnes de la diaspora prêtes à répondre à un entretien de 30 minutes pour ma thèse sur les parcours migratoires. Anonymat garanti."
  },
  {
    "author": 31,
    "community": "allemagne",
    "image": 0,
    "hoursAgo": 18,
    "text": "Astuce pour l'Anmeldung à Francfort : prenez rendez-vous en ligne très tôt le matin, des créneaux se libèrent souvent vers 7 h."
  },
  {
    "author": 8,
    "community": "entrepreneurs",
    "image": 0,
    "hoursAgo": 19,
    "text": "Webinaire gratuit jeudi : créer une société au Bénin depuis l'étranger, les étapes à l'APIEx et les erreurs à éviter. Inscription en commentaire."
  },
  {
    "author": 29,
    "community": "montreal",
    "image": 13,
    "hoursAgo": 20,
    "text": "Le tournoi de foot de la diaspora revient cet été ! 8 équipes, inscriptions ouvertes. Les maillots aux couleurs du pays sont commandés 💚💛❤️"
  },
  {
    "author": 3,
    "community": "bruxelles",
    "image": 11,
    "hoursAgo": 21,
    "text": "Session de percussions ouverte à tous samedi après-midi à Ixelles. Aucun niveau requis, on prête les instruments."
  },
  {
    "author": 21,
    "community": "canada",
    "image": 0,
    "hoursAgo": 22,
    "text": "Arrivé à Montréal il y a trois semaines. Le plus difficile : trouver un logement sans historique de crédit. Des conseils ?"
  },
  {
    "author": 25,
    "community": "retour",
    "image": 12,
    "hoursAgo": 23,
    "text": "Construire au pays depuis l'étranger : exigez toujours un titre foncier vérifié et un suivi photo hebdomadaire du chantier. Je vois encore trop de mauvaises surprises."
  },
  {
    "author": 6,
    "community": "parents",
    "image": 0,
    "hoursAgo": 24,
    "text": "Ma fille m'a demandé pourquoi sa grand-mère l'appelle par un autre prénom. Belle occasion de lui expliquer la tradition des noms de naissance."
  },
  {
    "author": 15,
    "community": "lyon",
    "image": 0,
    "hoursAgo": 26,
    "text": "Je fais un envoi groupé de colis vers Cotonou fin du mois. Si vous avez des cartons à envoyer depuis Lyon, contactez-moi pour mutualiser les frais."
  },
  {
    "author": 10,
    "community": "etudiants-fr",
    "image": 14,
    "hoursAgo": 27,
    "text": "Je propose des cours de maths gratuits en visio le mercredi soir pour les élèves de terminale au Bénin. Partagez autour de vous !"
  },
  {
    "author": 37,
    "community": "retour",
    "image": 15,
    "hoursAgo": 28,
    "text": "Ganvié ce matin. Si vous venez cet été avec vos enfants, faites-leur découvrir la cité lacustre, c'est un souvenir qu'ils garderont."
  },
  {
    "author": 19,
    "community": null,
    "image": 0,
    "hoursAgo": 29,
    "text": "Lecture du moment : un rapport passionnant sur la croissance des fintechs en zone UEMOA. Les transferts de la diaspora y jouent un rôle énorme."
  },
  {
    "author": 22,
    "community": "bruxelles",
    "image": 0,
    "hoursAgo": 30,
    "text": "Permanence juridique gratuite le premier samedi du mois : titres de séjour, regroupement familial, naturalisation. Sur rendez-vous."
  },
  {
    "author": 30,
    "community": "lyon",
    "image": 12,
    "hoursAgo": 31,
    "text": "Visite d'un chantier en terre crue près de Lyon, j'ai pensé aux constructions traditionnelles du Zou. Les savoir-faire voyagent dans les deux sens."
  },
  {
    "author": 0,
    "community": "tech",
    "image": 7,
    "hoursAgo": 32,
    "text": "Petite fierté : notre équipe a lancé l'appli de prise de rendez-vous pour une association de santé. Merci à celles et ceux qui ont testé la bêta ici !"
  },
  {
    "author": 16,
    "community": null,
    "image": 0,
    "hoursAgo": 33,
    "text": "De retour d'une mission dans l'Atacora sur l'accès à l'eau potable. Les comités villageois font un travail remarquable."
  },
  {
    "author": 18,
    "community": "etudiants-fr",
    "image": 6,
    "hoursAgo": 34,
    "text": "Soirée d'intégration des étudiants béninois de Nantes : merci aux 60 personnes présentes ! Les photos arrivent."
  },
  {
    "author": 32,
    "community": "emploi",
    "image": 0,
    "hoursAgo": 35,
    "text": "Reconversion : vous n'avez pas « perdu » vos années passées. Chaque compétence se transfère. Je partage une méthode simple pour faire son bilan en 3 étapes dans le prochain post."
  },
  {
    "author": 26,
    "community": "etudiants-fr",
    "image": 14,
    "hoursAgo": 36,
    "text": "Mes conseils pour survivre à la 5e année de médecine : planning sur 3 semaines glissantes, révisions actives et surtout du sommeil !"
  },
  {
    "author": 34,
    "community": "tech",
    "image": 0,
    "hoursAgo": 37,
    "text": "Je cherche un stage d'été en développement web (React ou .NET) au Québec ou en télétravail. Toute piste est la bienvenue 🙏"
  },
  {
    "author": 23,
    "community": "tech",
    "image": 7,
    "hoursAgo": 38,
    "text": "Notre appli de caisse pour les commerçantes de Dantokpa fonctionne maintenant hors ligne. Prochaine étape : le paiement mobile."
  },
  {
    "author": 35,
    "community": "culture",
    "image": 11,
    "hoursAgo": 39,
    "text": "Nouveau morceau en préparation, inspiré des chants du Zangbeto. Je partagerai un extrait dès qu'il sera mixé."
  },
  {
    "author": 28,
    "community": "entraide",
    "image": 0,
    "hoursAgo": 40,
    "text": "Rappel aux associations : je peux vous aider bénévolement pour vos comptes annuels et votre budget prévisionnel."
  },
  {
    "author": 39,
    "community": "culture",
    "image": 0,
    "hoursAgo": 42,
    "text": "Quand j'étais instituteur à Porto-Novo, on apprenait les contes à la lueur des lampes. Si des parents veulent que je raconte des histoires aux enfants, ce serait un plaisir."
  },
  {
    "author": 32,
    "community": "entrepreneurs",
    "image": 0,
    "hoursAgo": 44,
    "text": "Témoignage : j'ai accompagné cette année 12 personnes dans leur projet de retour. Le point commun des réussites : un projet testé avant le départ."
  },
  {
    "author": 34,
    "community": "etudiants-fr",
    "image": 0,
    "hoursAgo": 46,
    "text": "Quelqu'un connaît une bonne assurance logement étudiante au Québec ? Je viens de France et je suis un peu perdue."
  },
  {
    "author": 1,
    "community": "tech",
    "image": 0,
    "hoursAgo": 48,
    "text": "Je propose 3 sessions de mentorat gratuites ce mois-ci pour des développeurs juniors de la diaspora. Laissez un commentaire et je vous contacte."
  },
  {
    "author": 13,
    "community": "cuisine",
    "image": 3,
    "hoursAgo": 50,
    "text": "Le secret d'une bonne pâte rouge : la patience. Ne montez pas le feu trop vite, la sauce doit réduire doucement."
  },
  {
    "author": 4,
    "community": "paris",
    "image": 10,
    "hoursAgo": 52,
    "text": "Pop-up store ce week-end dans le 11e : créations en tissu béninois, bijoux de créatrices de la diaspora et musique live."
  },
  {
    "author": 9,
    "community": "allemagne",
    "image": 0,
    "hoursAgo": 54,
    "text": "Bonne nouvelle pour celles et ceux qui cherchent un emploi tech en Allemagne : beaucoup d'entreprises recrutent en anglais. N'attendez pas d'avoir le B2 pour postuler."
  },
  {
    "author": 2,
    "community": "entraide",
    "image": 0,
    "hoursAgo": 56,
    "text": "Merci à Arnaud et Yannick pour le coup de main hier ! Le déménagement a été bouclé en une heure et demie. Vive la solidarité."
  },
  {
    "author": 5,
    "community": "etudiants-fr",
    "image": 5,
    "hoursAgo": 58,
    "text": "Soutenance de mon projet de fin d'études validée ! Merci à toutes les personnes de ce groupe qui ont relu mon rapport. On ne lâche rien."
  },
  {
    "author": 7,
    "community": "retour",
    "image": 12,
    "hoursAgo": 60,
    "text": "La saison de l'ananas pain de sucre a commencé. Nous cherchons des partenaires pour l'export vers la France en circuit court."
  },
  {
    "author": 14,
    "community": null,
    "image": 0,
    "hoursAgo": 62,
    "text": "Petit exercice de design : à quoi ressemblerait une appli pensée pour les familles séparées par un océan ? J'aimerais vos idées."
  },
  {
    "author": 11,
    "community": "canada",
    "image": 0,
    "hoursAgo": 64,
    "text": "Rappel fiscal pour le Canada : pensez à déclarer vos revenus même si vous êtes arrivés en cours d'année, cela ouvre des droits aux crédits d'impôt."
  },
  {
    "author": 24,
    "community": "paris",
    "image": 16,
    "hoursAgo": 66,
    "text": "Concert ce vendredi à La Bellevilloise : une soirée 100 % artistes béninois. Il reste quelques places."
  },
  {
    "author": 0,
    "community": "culture",
    "image": 0,
    "hoursAgo": 68,
    "text": "Petit débat du soir : quel est pour vous le plat qui résume le mieux le Bénin ? Je vote pour la sauce feuille avec de l'akassa."
  },
  {
    "author": 27,
    "community": "emploi",
    "image": 0,
    "hoursAgo": 70,
    "text": "Ma fintech recrute un développeur backend .NET à Londres (télétravail partiel possible). Écrivez-moi."
  },
  {
    "author": 20,
    "community": "parents",
    "image": 0,
    "hoursAgo": 72,
    "text": "Atelier « préparer l'arrivée de bébé loin de la famille » le mois prochain. On parlera aussi des traditions autour de la naissance."
  },
  {
    "author": 30,
    "community": "lyon",
    "image": 0,
    "hoursAgo": 74,
    "text": "Afterwork des Béninois de Lyon jeudi dans le 7e. Venez comme vous êtes, on prend une grande table."
  },
  {
    "author": 17,
    "community": "paris",
    "image": 6,
    "hoursAgo": 76,
    "text": "Merci à toutes les familles venues pour la séance photo gratuite de la fête des mères. Les tirages sont prêts !"
  },
  {
    "author": 12,
    "community": "culture",
    "image": 0,
    "hoursAgo": 78,
    "text": "Savez-vous que les bas-reliefs des palais royaux d'Abomey racontent l'histoire du royaume du Danhomè ? Je prépare une petite série de posts sur le sujet."
  },
  {
    "author": 21,
    "community": "montreal",
    "image": 0,
    "hoursAgo": 80,
    "text": "Mise à jour : j'ai trouvé un appartement grâce à une personne de ce groupe. Merci infiniment, vous êtes formidables."
  },
  {
    "author": 3,
    "community": null,
    "image": 11,
    "hoursAgo": 82,
    "text": "Ouidah, la Porte du Non-Retour. Un lieu qui oblige au silence et à la mémoire."
  },
  {
    "author": 15,
    "community": "emploi",
    "image": 0,
    "hoursAgo": 84,
    "text": "Mon entreprise de logistique cherche un préparateur de commandes à Lyon Est, CDI. Horaires de journée. Message privé pour plus d'infos."
  },
  {
    "author": 36,
    "community": "emploi",
    "image": 0,
    "hoursAgo": 86,
    "text": "Conseil RH : sur un CV, mettez vos résultats chiffrés plutôt qu'une liste de tâches. « Réduit les délais de 20 % » parle plus que « gestion des délais »."
  },
  {
    "author": 8,
    "community": "entrepreneurs",
    "image": 0,
    "hoursAgo": 88,
    "text": "Rappel : l'OHADA harmonise le droit des affaires dans 17 pays. Un statut créé au Bénin facilite votre développement dans la sous-région."
  },
  {
    "author": 19,
    "community": "retour",
    "image": 0,
    "hoursAgo": 90,
    "text": "Pour celles et ceux qui pensent à investir au pays : diversifiez, documentez tout et entourez-vous de personnes de confiance avec des références."
  },
  {
    "author": 6,
    "community": "canada",
    "image": 9,
    "hoursAgo": 92,
    "text": "Première tempête de neige de la saison à Québec. Les enfants sont ravis, moi beaucoup moins ❄️"
  },
  {
    "author": 26,
    "community": "entraide",
    "image": 0,
    "hoursAgo": 94,
    "text": "Je donne des manuels de première année de médecine (PASS). À récupérer à Strasbourg."
  },
  {
    "author": 32,
    "community": "paris",
    "image": 0,
    "hoursAgo": 96,
    "text": "Atelier gratuit samedi : « oser sa reconversion ». Places limitées à 15 personnes pour garder un format convivial."
  },
  {
    "author": 9,
    "community": "tech",
    "image": 0,
    "hoursAgo": 98,
    "text": "Thread : les 5 compétences data les plus demandées en Europe en ce moment. 1) SQL, toujours. 2) Python. 3) Modélisation. 4) Visualisation. 5) Savoir expliquer aux métiers."
  },
  {
    "author": 16,
    "community": null,
    "image": 0,
    "hoursAgo": 100,
    "text": "Genève accueille une exposition sur l'art contemporain africain. Plusieurs artistes béninois y sont exposés, allez-y si vous passez par là."
  },
  {
    "author": 29,
    "community": "canada",
    "image": 13,
    "hoursAgo": 104,
    "text": "Victoire 3-1 en amical contre l'équipe sénégalaise de Montréal ! Super ambiance, merci aux supporters."
  },
  {
    "author": 25,
    "community": "retour",
    "image": 0,
    "hoursAgo": 108,
    "text": "Question fréquente : peut-on acheter un terrain sans être présent au Bénin ? Oui, par procuration notariée, mais restez extrêmement vigilants."
  },
  {
    "author": 35,
    "community": "bruxelles",
    "image": 16,
    "hoursAgo": 112,
    "text": "Merci Bruxelles pour cette soirée afrobeat ! Le public a chanté en fon avec nous, j'en ai encore des frissons."
  },
  {
    "author": 18,
    "community": "etudiants-fr",
    "image": 0,
    "hoursAgo": 116,
    "text": "L'association des étudiants béninois de Nantes recherche un ou une trésorière pour l'année. Engagement léger, expérience enrichissante !"
  },
  {
    "author": 37,
    "community": null,
    "image": 15,
    "hoursAgo": 120,
    "text": "Programme spécial « racines » cet été : 7 jours entre Ouidah, Abomey et Ganvié pour les jeunes de la diaspora. Les familles sont les bienvenues."
  },
  {
    "author": 10,
    "community": null,
    "image": 0,
    "hoursAgo": 124,
    "text": "Ce qui me rend fière : trois de mes élèves en ligne viennent d'obtenir leur bac avec mention. Bravo à eux, ils ont beaucoup travaillé."
  },
  {
    "author": 23,
    "community": "lyon",
    "image": 0,
    "hoursAgo": 128,
    "text": "Quelqu'un connaît un bon coiffeur afro à Lyon qui fait aussi les enfants ? Merci d'avance."
  },
  {
    "author": 31,
    "community": "allemagne",
    "image": 9,
    "hoursAgo": 132,
    "text": "Weekend à Hambourg avec des amis de la diaspora. Le port sous la neige, c'est quelque chose."
  },
  {
    "author": 28,
    "community": "cuisine",
    "image": 3,
    "hoursAgo": 136,
    "text": "Testé ce week-end : le ago glain (sauce de crabe) avec des crabes surgelés. Pas tout à fait comme à Cotonou, mais franchement réussi."
  },
  {
    "author": 2,
    "community": "lyon",
    "image": 0,
    "hoursAgo": 140,
    "text": "Collecte de fournitures scolaires pour une école de Bohicon : cahiers, stylos, calculatrices. Point de dépôt à la Croix-Rousse jusqu'à la fin du mois."
  },
  {
    "author": 0,
    "community": "entrepreneurs",
    "image": 0,
    "hoursAgo": 144,
    "text": "Je réfléchis à un projet d'accompagnement numérique pour les petites associations de la diaspora. Est-ce que ce besoin vous parle ?"
  },
  {
    "author": 34,
    "community": "canada",
    "image": 0,
    "hoursAgo": 150,
    "text": "Bonne nouvelle : j'ai décroché un stage d'été en développement web à Québec ! Merci pour les conseils sur la lettre de motivation."
  },
  {
    "author": 13,
    "community": "cuisine",
    "image": 0,
    "hoursAgo": 156,
    "text": "Question existentielle : le piment, avant ou après la cuisson ? Je lance le débat, j'ai mon avis 😄"
  },
  {
    "author": 5,
    "community": null,
    "image": 8,
    "hoursAgo": 162,
    "text": "Billet pour Cotonou réservé pour les vacances ! Deux ans sans rentrer, j'ai hâte de revoir la famille."
  },
  {
    "author": 22,
    "community": "entraide",
    "image": 0,
    "hoursAgo": 168,
    "text": "Rappel : en cas de refus de titre de séjour, les délais de recours sont très courts. Ne restez pas seuls, contactez une association ou un avocat rapidement."
  },
  {
    "author": 14,
    "community": "montreal",
    "image": 6,
    "hoursAgo": 174,
    "text": "Soirée « Saveurs du Bénin » à Montréal : plus de 80 personnes ! Merci aux bénévoles en cuisine, vous avez assuré."
  },
  {
    "author": 39,
    "community": "parents",
    "image": 0,
    "hoursAgo": 180,
    "text": "Les enfants posent souvent la question : « d'où vient-on ? ». Je recommande de leur montrer une carte et de leur raconter l'histoire de la famille, village par village."
  },
  {
    "author": 32,
    "community": "entrepreneurs",
    "image": 0,
    "hoursAgo": 186,
    "text": "Retour de Cotonou où j'ai visité trois incubateurs. L'écosystème bouge vite, il y a de vraies opportunités de collaboration avec la diaspora."
  },
  {
    "author": 4,
    "community": null,
    "image": 10,
    "hoursAgo": 192,
    "text": "Les coulisses d'un shooting : nos mannequins sont toutes des membres de la communauté. Merci pour votre confiance !"
  },
  {
    "author": 21,
    "community": "etudiants-fr",
    "image": 0,
    "hoursAgo": 198,
    "text": "Pour celles et ceux qui hésitent entre la France et le Canada pour leurs études : faites le calcul complet (frais, logement, job étudiant). Je partage mon tableau si besoin."
  },
  {
    "author": 34,
    "community": "tech",
    "image": 0,
    "hoursAgo": 204,
    "text": "Petit projet perso : une appli pour apprendre les nombres en fon. Des volontaires pour vérifier la prononciation ?"
  },
  {
    "author": 11,
    "community": "emploi",
    "image": 0,
    "hoursAgo": 210,
    "text": "Je relis gratuitement les CV des jeunes diplômés en finance. Envoyez-moi un message, je réponds sous une semaine."
  },
  {
    "author": 20,
    "community": "paris",
    "image": 0,
    "hoursAgo": 216,
    "text": "Atelier portage bébé et massage traditionnel dimanche matin, ouvert aux futures et jeunes mamans."
  },
  {
    "author": 24,
    "community": "culture",
    "image": 4,
    "hoursAgo": 222,
    "text": "Le marché Dantokpa vu par une photographe béninoise installée à Paris : une série d'images saisissante. Je vous mets le lien de l'expo."
  },
  {
    "author": 7,
    "community": "retour",
    "image": 0,
    "hoursAgo": 228,
    "text": "Erreur que j'ai faite : sous-estimer le temps des démarches. Comptez toujours le double de ce que vous imaginez."
  },
  {
    "author": 1,
    "community": "montreal",
    "image": 0,
    "hoursAgo": 234,
    "text": "On organise un atelier « premier emploi au Québec » : CV québécois, réseau, entrevues. Inscription gratuite."
  },
  {
    "author": 19,
    "community": null,
    "image": 0,
    "hoursAgo": 240,
    "text": "Une pensée pour toutes celles et ceux qui fêtent le 1er août loin du pays. Le Bénin vit aussi à travers nous."
  },
  {
    "author": 30,
    "community": "culture",
    "image": 12,
    "hoursAgo": 252,
    "text": "Les maisons tata somba de l'Atacora sont de véritables chefs-d'œuvre d'architecture. J'aimerais organiser une visite d'étude l'an prochain."
  },
  {
    "author": 36,
    "community": "paris",
    "image": 0,
    "hoursAgo": 264,
    "text": "Grand merci aux 40 participants de l'atelier CV ! Prochaine session en ligne pour celles et ceux hors d'Île-de-France."
  },
  {
    "author": 3,
    "community": "culture",
    "image": 11,
    "hoursAgo": 276,
    "text": "Les rythmes du Bénin à l'honneur dans un festival à Liège. Fier de représenter notre culture avec mon groupe."
  },
  {
    "author": 0,
    "community": "etudiants-fr",
    "image": 0,
    "hoursAgo": 288,
    "text": "Je me souviens de mon arrivée à Paris pour mes études : une valise, une adresse griffonnée et beaucoup d'espoir. Accrochez-vous, ça vaut le coup."
  },
  {
    "author": 17,
    "community": null,
    "image": 1,
    "hoursAgo": 300,
    "text": "Archives : la route des pêches au petit matin. Une série photo que je n'avais jamais publiée."
  }
];

export const COMMENT_POOL: string[] = [
  "Merci pour le partage, c'est très utile !",
  "Je suis intéressé(e), je t'envoie un message.",
  "Super initiative, bravo 👏",
  "On se voit là-bas alors !",
  "Ça me rappelle tellement la maison…",
  "Est-ce que c'est ouvert aux familles avec enfants ?",
  "J'ai vécu exactement la même chose à mon arrivée.",
  "Tu as une adresse précise ?",
  "Je partage avec mes cousins, ça va les intéresser.",
  "Merci, je ne connaissais pas cette astuce.",
  "Courage, ça finit toujours par se débloquer.",
  "Quelle belle photo !",
  "Je confirme, c'est une très bonne adresse.",
  "Félicitations, c'est mérité !",
  "Tu peux m'inscrire s'il te plaît ?",
  "Excellente idée, il faudrait le faire aussi à Lyon.",
  "Je suis disponible samedi matin si besoin.",
  "Ça donne faim rien qu'à lire 😄",
  "Merci pour ces conseils concrets.",
  "Combien de places restent-il ?",
  "On devrait organiser ça plus souvent.",
  "Très bonne analyse, je suis d'accord.",
  "Je n'ai pas eu ce problème, mais mon dossier a pris 2 mois.",
  "Pense à vérifier sur le site officiel, les règles ont changé.",
  "Je relaie dans le groupe des étudiants de Rennes.",
  "Trop fier de voir ça !",
  "Est-ce qu'il y aura une session en ligne ?",
  "Bonne chance pour la suite 🙏",
  "Les enfants vont adorer.",
  "J'ai testé ta recette, validée par toute la famille.",
  "Je peux aider pour la communication si tu veux.",
  "Tu as raison, il faut être patient.",
  "Quelle émotion…",
  "Je me souviens de cet endroit, magnifique.",
  "Super, merci pour l'info !",
  "C'est exactement ce dont on avait besoin.",
  "Je suis preneur pour le tableau, merci !",
  "Ça mérite d'être plus connu.",
  "J'ai envoyé mon CV, merci pour l'opportunité.",
  "Vivement la prochaine édition !",
  "Bienvenue à Montréal ! Tu vas te plaire.",
  "Attention aux arnaques, toujours vérifier les références.",
  "Belle leçon de persévérance.",
  "Je suis partant pour le test.",
  "Tu fais un travail formidable.",
  "Avec plaisir, je viendrai avec des amis.",
  "On compte sur toi pour les photos !",
  "Le piment après, évidemment 😅",
  "Merci, ça rassure de lire ça.",
  "C'est noté dans mon agenda.",
  "Je connais quelqu'un qui pourrait t'aider, je te mets en relation.",
  "Bravo pour ce parcours inspirant.",
  "Hâte d'écouter ça !",
  "Je découvre, merci pour le partage culturel.",
  "Est-ce que tu livres en Belgique ?",
  "Excellente nouvelle !",
  "On en parle à la prochaine rencontre ?",
  "Ça fait chaud au cœur.",
  "Pareil pour moi, l'hiver c'est un vrai sport.",
  "Merci pour ton engagement auprès des jeunes."
];

export const SEED_CONVERSATIONS: ConversationSeed[] = [
  {
    "otherUser": 13,
    "hoursAgo": 1,
    "unreadCount": 2,
    "lines": [
      "Salut Sèna ! Tu viens samedi au restaurant ?",
      "> Bonjour Hermann ! Oui, normalement on sera quatre.",
      "Parfait, je vous garde la table près de la fenêtre.",
      "> Super, merci. Tu fais toujours ton amiwo du samedi ?",
      "Évidemment, c'est la spécialité de la maison 😄",
      "> Alors c'est réglé, on arrive vers 20 h.",
      "Top. Tu peux me confirmer le nombre définitif vendredi ?",
      "> Je te dis ça demain soir sans faute.",
      "Merci ! Au fait, tu connais quelqu'un qui cherche un job en salle ?",
      "> Je vais poser la question dans le groupe Béninois de Paris.",
      "Ce serait top, merci beaucoup.",
      "J'ai aussi une nouvelle recette de sauce gombo à te faire goûter.",
      "Tu me diras ce que tu en penses samedi !"
    ]
  },
  {
    "otherUser": 1,
    "hoursAgo": 5,
    "unreadCount": 0,
    "lines": [
      "Hello Sèna, j'ai vu ton post sur l'appli de rendez-vous, bravo !",
      "> Merci Kossi ! Ton retour sur l'architecture nous a beaucoup aidés.",
      "Avec plaisir. Vous êtes partis sur quelle stack finalement ?",
      "> .NET pour l'API et une appli mobile multiplateforme.",
      "Bon choix. Pensez à la gestion hors ligne dès le début.",
      "> C'est prévu, les utilisateurs ont souvent une connexion instable.",
      "Exactement. Tu fais une démo au prochain meetup ?",
      "> Bonne idée, je prépare 15 minutes.",
      "Je préviens Jospin, il organise le programme.",
      "> Merci ! On se cale un appel avant ?",
      "Mardi 19 h heure de Paris, ça te va ?",
      "> Parfait pour moi."
    ]
  },
  {
    "otherUser": 4,
    "hoursAgo": 9,
    "unreadCount": 1,
    "lines": [
      "Coucou ! Tu serais partante pour tester des tailles de la nouvelle collection ?",
      "> Avec plaisir Ablawa ! C'est quand ?",
      "Je pensais à dimanche après-midi, à l'atelier.",
      "> Dimanche ça marche. Je viens avec ma sœur, elle adore tes créations.",
      "Génial, plus on est de fous… 😄",
      "> Tu as besoin qu'on apporte quelque chose ?",
      "Juste votre bonne humeur. Il y aura du bissap.",
      "> Alors on sera là à 15 h.",
      "Parfait. Je t'envoie l'adresse exacte.",
      "> Merci, à dimanche !",
      "J'ai hâte de vous montrer les tissus d'Abomey.",
      "Ils sont arrivés hier, les couleurs sont incroyables.",
      "Je te mets une photo demain pour te donner un avant-goût."
    ]
  },
  {
    "otherUser": 9,
    "hoursAgo": 20,
    "unreadCount": 0,
    "lines": [
      "Salut Sèna, je finalise le programme du meetup.",
      "> Salut Jospin ! Kossi m'en a parlé, je suis partante pour une démo.",
      "Top ! Tu préfères passer en premier ou en dernier ?",
      "> Plutôt en deuxième, le temps que tout le monde arrive.",
      "C'est noté. Tu as besoin d'un écran particulier ?",
      "> Juste un câble HDMI et une bonne connexion.",
      "On sera dans un espace de coworking, aucun souci.",
      "> Parfait. On sera combien à peu près ?",
      "Une quarantaine d'inscrits pour l'instant.",
      "> Super, ça fait plaisir de voir la communauté grandir.",
      "Oui ! Je t'envoie l'ordre de passage cette semaine.",
      "> Merci pour l'organisation, c'est un vrai travail."
    ]
  },
  {
    "otherUser": 2,
    "hoursAgo": 28,
    "unreadCount": 0,
    "lines": [
      "Bonjour Sèna, tu as reçu le formulaire pour la collecte ?",
      "> Bonjour Afi ! Oui, je l'ai partagé dans deux groupes.",
      "Merci beaucoup, on a déjà 30 kits scolaires.",
      "> C'est génial ! Il en faut combien au total ?",
      "L'école de Bohicon nous a demandé 120 kits.",
      "> On va y arriver. Je peux faire un point de collecte à Paris ?",
      "Ce serait parfait, beaucoup de gens m'ont demandé.",
      "> Je vois avec la mairie du 19e pour une salle.",
      "Tiens-moi au courant, je préviens l'équipe.",
      "> Promis, je te redis ça vendredi.",
      "Merci pour ton aide, vraiment.",
      "> C'est normal, c'est pour les enfants.",
      "On fera une belle photo de remise des kits !"
    ]
  },
  {
    "otherUser": 32,
    "hoursAgo": 40,
    "unreadCount": 0,
    "lines": [
      "Bonjour Sèna, merci pour ton message sur l'atelier reconversion.",
      "> Bonjour Murielle ! Une amie aimerait y participer.",
      "Avec plaisir, il reste trois places.",
      "> Elle travaille dans la banque et pense à rentrer au Bénin.",
      "C'est exactement le public de l'atelier.",
      "> Je lui transmets le lien d'inscription ?",
      "Oui, et dis-lui de préparer trois choses qu'elle aime faire.",
      "> C'est noté, merci !",
      "Tu pourrais venir aussi, si le sujet t'intéresse.",
      "> Pourquoi pas, je regarde mon planning.",
      "Pas de pression, le prochain est dans un mois.",
      "> Merci Murielle, belle journée !"
    ]
  },
  {
    "otherUser": 21,
    "hoursAgo": 55,
    "unreadCount": 1,
    "lines": [
      "Bonjour, désolé de vous écrire directement…",
      "J'ai vu que vous connaissiez bien les démarches étudiantes.",
      "> Bonjour Judicaël, aucun souci, je t'écoute.",
      "Je viens d'arriver à Montréal et je cherche un logement.",
      "> Je suis à Paris, mais Kossi et Bénédicta sont à Montréal.",
      "Ah super, vous pourriez me mettre en relation ?",
      "> Bien sûr, je leur écris aujourd'hui.",
      "Merci infiniment !",
      "> Pense aussi au groupe Béninois de Montréal, il est très actif.",
      "Je viens de le rejoindre.",
      "> Parfait. Tiens-moi au courant de ta recherche.",
      "Je n'y manquerai pas, merci encore.",
      "Bonne nouvelle : j'ai une visite demain grâce au groupe !"
    ]
  },
  {
    "otherUser": 36,
    "hoursAgo": 72,
    "unreadCount": 0,
    "lines": [
      "Hello Sèna, ton CV est super clair.",
      "> Merci Sylvie ! Tu as des suggestions ?",
      "Ajoute des résultats chiffrés sur ton dernier projet.",
      "> Bonne idée, on a réduit les délais de traitement de 30 %.",
      "Voilà, c'est le genre de phrase qui fait la différence.",
      "> Je le modifie ce soir.",
      "Et mets ton engagement associatif en avant.",
      "> Tu penses que ça compte autant ?",
      "Énormément, surtout pour les postes de gestion de projet.",
      "> Merci pour tes conseils, c'est précieux.",
      "Avec plaisir. Renvoie-le-moi quand c'est fait.",
      "> Promis !"
    ]
  },
  {
    "otherUser": 7,
    "hoursAgo": 96,
    "unreadCount": 0,
    "lines": [
      "Salut Sèna, comment va Paris ?",
      "> Ça va bien Gildas ! Et Cotonou ?",
      "Il fait chaud, les ananas sont beaux, tout va bien 😄",
      "> Tu as trouvé ton partenaire pour l'export ?",
      "Deux pistes sérieuses à Rungis.",
      "> Super ! Tu veux que je relise ta présentation ?",
      "Ce serait top, je te l'envoie ce week-end.",
      "> OK, je regarde dimanche.",
      "Merci ! Au fait, tu rentres quand au pays ?",
      "> En décembre, pour les fêtes.",
      "On se fait une visite de la plantation alors.",
      "> Avec plaisir, j'en parle à ma famille.",
      "Je vous attends !"
    ]
  },
  {
    "otherUser": 17,
    "hoursAgo": 140,
    "unreadCount": 0,
    "lines": [
      "Bonjour Sèna ! Les photos du pique-nique sont prêtes.",
      "> Merci Romaric, tu es le meilleur !",
      "Je t'envoie le lien de l'album.",
      "> Je peux les partager dans le groupe ?",
      "Bien sûr, c'est fait pour.",
      "> Il y a quelques photos d'enfants, on floute ?",
      "Bonne remarque, je m'en occupe avant le partage.",
      "> Merci, c'est plus prudent.",
      "Je te renvoie l'album corrigé demain.",
      "> Parfait.",
      "Tu me diras laquelle tu préfères pour l'affiche du prochain.",
      "> Celle du grand cercle, sans hésiter !"
    ]
  },
  {
    "otherUser": 30,
    "hoursAgo": 200,
    "unreadCount": 0,
    "lines": [
      "Coucou Sèna, tu viens à l'afterwork de Lyon jeudi ?",
      "> Je serai à Lyon pour le travail, donc oui !",
      "Génial, on sera une quinzaine.",
      "> Je viens avec une collègue, ça ne dérange pas ?",
      "Pas du tout, plus on est nombreux mieux c'est.",
      "> C'est où exactement ?",
      "Dans le 7e, je t'envoie l'adresse jeudi matin.",
      "> Super, merci Aurore.",
      "Tu restes jusqu'à quand à Lyon ?",
      "> Jusqu'à vendredi soir.",
      "On pourrait visiter le chantier en terre crue vendredi midi.",
      "> Avec plaisir, ça m'intéresse beaucoup.",
      "Je préviens l'équipe du chantier."
    ]
  },
  {
    "otherUser": 39,
    "hoursAgo": 320,
    "unreadCount": 0,
    "lines": [
      "Bonjour ma fille, j'ai lu ton message sur les contes.",
      "> Bonjour monsieur Ewé ! Les enfants seraient ravis.",
      "Je peux venir un samedi après-midi.",
      "> On organise un goûter le 20, ça vous irait ?",
      "C'est parfait. Combien d'enfants ?",
      "> Une douzaine, entre 5 et 11 ans.",
      "Je préparerai l'histoire de la tortue et du léopard.",
      "> Ils vont adorer !",
      "Et je leur apprendrai une petite chanson en fon.",
      "> Merci beaucoup, c'est un vrai cadeau.",
      "C'est moi qui vous remercie de penser aux anciens.",
      "> À très bientôt monsieur Ewé."
    ]
  }
];

export const EVENT_CATEGORIES: string[] = [
  "Culture",
  "Networking",
  "Gastronomie",
  "Sport",
  "Formation",
  "Entraide",
  "Famille"
];

export const SEED_EVENTS: EventSeed[] = [
  {
    "title": "Dîner « Saveurs du Bénin »",
    "description": "Menu découverte en cinq plats préparé par le chef Hermann Dansou : amiwo, sauce gombo, poisson braisé, ablo et dèguè. Places limitées.",
    "category": "Gastronomie",
    "city": "Paris",
    "country": "France",
    "address": "Restaurant de démonstration, 12 rue Fictive, 75011 Paris",
    "daysFromNow": 3,
    "startHour": 20,
    "durationHours": 3,
    "organizer": 13,
    "community": "paris",
    "image": 3,
    "capacity": 40,
    "participants": 31,
    "demoParticipates": false
  },
  {
    "title": "Meetup Tech Diaspora",
    "description": "Démos de projets, retours d'expérience sur le recrutement tech en Europe et networking. Session diffusée en ligne.",
    "category": "Networking",
    "city": "Berlin",
    "country": "Allemagne",
    "address": "Espace de coworking (adresse communiquée aux inscrits)",
    "daysFromNow": 12,
    "startHour": 18,
    "durationHours": 3,
    "organizer": 9,
    "community": "tech",
    "image": 7,
    "capacity": 80,
    "participants": 42,
    "demoParticipates": true
  },
  {
    "title": "Pique-nique de rentrée",
    "description": "Grand pique-nique convivial : chacun apporte un plat, on fournit la musique et les jeux pour les enfants.",
    "category": "Famille",
    "city": "Paris",
    "country": "France",
    "address": "Parc des Buttes-Chaumont, entrée rue Botzaris",
    "daysFromNow": 9,
    "startHour": 12,
    "durationHours": 5,
    "organizer": 0,
    "community": "paris",
    "image": 6,
    "capacity": null,
    "participants": 54,
    "demoParticipates": true
  },
  {
    "title": "Atelier CV et entretien",
    "description": "Rendez-vous individuels de 20 minutes avec des recruteurs bénévoles. Apportez votre CV imprimé.",
    "category": "Formation",
    "city": "Paris",
    "country": "France",
    "address": "Maison des associations (démo), Paris 19e",
    "daysFromNow": 6,
    "startHour": 14,
    "durationHours": 3,
    "organizer": 36,
    "community": "emploi",
    "image": 14,
    "capacity": 20,
    "participants": 17,
    "demoParticipates": false
  },
  {
    "title": "Tournoi de football de la diaspora",
    "description": "8 équipes, matchs de 2 × 20 minutes, finale en fin d'après-midi. Venez supporter !",
    "category": "Sport",
    "city": "Montréal",
    "country": "Canada",
    "address": "Parc de démonstration, terrain synthétique n° 2",
    "daysFromNow": 25,
    "startHour": 10,
    "durationHours": 8,
    "organizer": 29,
    "community": "montreal",
    "image": 13,
    "capacity": null,
    "participants": 64,
    "demoParticipates": false
  },
  {
    "title": "Soirée contes pour enfants",
    "description": "Gérard Ewé raconte les contes de son enfance, suivis d'un goûter et d'une chanson en fon.",
    "category": "Famille",
    "city": "Paris",
    "country": "France",
    "address": "Médiathèque de démonstration, Paris 20e",
    "daysFromNow": 15,
    "startHour": 15,
    "durationHours": 2,
    "organizer": 39,
    "community": "culture",
    "image": 14,
    "capacity": 30,
    "participants": 12,
    "demoParticipates": true
  },
  {
    "title": "Webinaire : créer son entreprise au Bénin",
    "description": "Les étapes de création à l'APIEx, les statuts OHADA et les erreurs à éviter quand on est à l'étranger.",
    "category": "Formation",
    "city": "En ligne",
    "country": "",
    "address": "Lien de visioconférence envoyé aux inscrits",
    "daysFromNow": 4,
    "startHour": 19,
    "durationHours": 2,
    "organizer": 8,
    "community": "entrepreneurs",
    "image": 7,
    "capacity": 150,
    "participants": 87,
    "demoParticipates": false
  },
  {
    "title": "Concert afrobeat & musique fon",
    "description": "Landry Alladayè et ses musiciens en concert. Première partie : jeunes talents de la diaspora.",
    "category": "Culture",
    "city": "Bruxelles",
    "country": "Belgique",
    "address": "Salle de concert de démonstration, Ixelles",
    "daysFromNow": 18,
    "startHour": 20,
    "durationHours": 4,
    "organizer": 35,
    "community": "bruxelles",
    "image": 16,
    "capacity": 200,
    "participants": 133,
    "demoParticipates": false
  },
  {
    "title": "Afterwork des Béninois de Lyon",
    "description": "Rencontre informelle après le travail pour faire connaissance et échanger les bons plans.",
    "category": "Networking",
    "city": "Lyon",
    "country": "France",
    "address": "Bar de démonstration, Lyon 7e",
    "daysFromNow": 2,
    "startHour": 19,
    "durationHours": 3,
    "organizer": 30,
    "community": "lyon",
    "image": 6,
    "capacity": null,
    "participants": 18,
    "demoParticipates": false
  },
  {
    "title": "Collecte de fournitures scolaires",
    "description": "Point de dépôt pour la collecte au profit d'une école de Bohicon : cahiers, stylos, calculatrices.",
    "category": "Entraide",
    "city": "Lyon",
    "country": "France",
    "address": "Local associatif de démonstration, Croix-Rousse",
    "daysFromNow": 7,
    "startHour": 10,
    "durationHours": 6,
    "organizer": 2,
    "community": "entraide",
    "image": 14,
    "capacity": null,
    "participants": 23,
    "demoParticipates": false
  },
  {
    "title": "Atelier « Préparer son retour »",
    "description": "Emploi, logement, scolarité, création d'entreprise : un atelier concret avec des membres déjà rentrés.",
    "category": "Formation",
    "city": "Paris",
    "country": "France",
    "address": "Salle de réunion de démonstration, Paris 10e",
    "daysFromNow": 21,
    "startHour": 14,
    "durationHours": 3,
    "organizer": 32,
    "community": "retour",
    "image": 12,
    "capacity": 35,
    "participants": 21,
    "demoParticipates": false
  },
  {
    "title": "Cours de cuisine : la pâte rouge",
    "description": "Apprenez à réussir la pâte rouge et sa sauce tomate pimentée. Repas partagé à la fin.",
    "category": "Gastronomie",
    "city": "Ottawa",
    "country": "Canada",
    "address": "Cuisine partagée de démonstration, centre-ville",
    "daysFromNow": 11,
    "startHour": 17,
    "durationHours": 3,
    "organizer": 11,
    "community": "cuisine",
    "image": 3,
    "capacity": 12,
    "participants": 9,
    "demoParticipates": false
  },
  {
    "title": "Accueil des nouveaux arrivants",
    "description": "Démarches, logement, transport, emploi : une soirée pour bien démarrer au Québec.",
    "category": "Entraide",
    "city": "Montréal",
    "country": "Canada",
    "address": "Centre communautaire de démonstration, Plateau",
    "daysFromNow": 5,
    "startHour": 18,
    "durationHours": 2,
    "organizer": 1,
    "community": "canada",
    "image": 9,
    "capacity": 60,
    "participants": 28,
    "demoParticipates": false
  },
  {
    "title": "Exposition photo « Dantokpa »",
    "description": "Vernissage de la série photo consacrée au plus grand marché de Cotonou, en présence de la photographe.",
    "category": "Culture",
    "city": "Paris",
    "country": "France",
    "address": "Galerie de démonstration, Paris 3e",
    "daysFromNow": 28,
    "startHour": 18,
    "durationHours": 3,
    "organizer": 24,
    "community": "culture",
    "image": 4,
    "capacity": null,
    "participants": 46,
    "demoParticipates": false
  },
  {
    "title": "Journée « racines » à Ouidah",
    "description": "Visite guidée de Ouidah pour les jeunes de la diaspora : route des esclaves, forêt sacrée, musée.",
    "category": "Culture",
    "city": "Ouidah",
    "country": "Bénin",
    "address": "Point de rendez-vous : place de démonstration, Ouidah",
    "daysFromNow": 45,
    "startHour": 8,
    "durationHours": 9,
    "organizer": 37,
    "community": "retour",
    "image": 15,
    "capacity": 25,
    "participants": 14,
    "demoParticipates": false
  },
  {
    "title": "Rencontre des étudiants béninois de Nantes",
    "description": "Soirée d'intégration, présentation de l'association et jeux pour faire connaissance.",
    "category": "Networking",
    "city": "Nantes",
    "country": "France",
    "address": "Maison des étudiants (démo), Nantes",
    "daysFromNow": 8,
    "startHour": 19,
    "durationHours": 3,
    "organizer": 18,
    "community": "etudiants-fr",
    "image": 5,
    "capacity": 80,
    "participants": 37,
    "demoParticipates": false
  },
  {
    "title": "Permanence juridique gratuite",
    "description": "Titres de séjour, regroupement familial, naturalisation : information juridique gratuite sur rendez-vous.",
    "category": "Entraide",
    "city": "Bruxelles",
    "country": "Belgique",
    "address": "Cabinet de démonstration, Bruxelles centre",
    "daysFromNow": 10,
    "startHour": 10,
    "durationHours": 4,
    "organizer": 22,
    "community": "bruxelles",
    "image": 14,
    "capacity": 16,
    "participants": 7,
    "demoParticipates": false
  },
  {
    "title": "Atelier portage et massage bébé",
    "description": "Atelier animé par une sage-femme : portage, massage traditionnel et échanges entre parents.",
    "category": "Famille",
    "city": "Paris",
    "country": "France",
    "address": "Espace parents de démonstration, Paris 18e",
    "daysFromNow": 13,
    "startHour": 10,
    "durationHours": 2,
    "organizer": 20,
    "community": "parents",
    "image": 6,
    "capacity": 6,
    "participants": 6,
    "demoParticipates": false
  },
  {
    "title": "Hackathon « Fintech UEMOA »",
    "description": "48 heures pour imaginer des solutions de paiement et de transfert pour la diaspora. Équipes de 3 à 5.",
    "category": "Networking",
    "city": "Londres",
    "country": "Royaume-Uni",
    "address": "Campus de démonstration, Shoreditch",
    "daysFromNow": 35,
    "startHour": 9,
    "durationHours": 33,
    "organizer": 27,
    "community": "tech",
    "image": 7,
    "capacity": 100,
    "participants": 61,
    "demoParticipates": false
  },
  {
    "title": "Séance de percussions ouverte",
    "description": "Découverte des rythmes traditionnels du Bénin. Instruments prêtés, aucun niveau requis.",
    "category": "Culture",
    "city": "Bruxelles",
    "country": "Belgique",
    "address": "Studio de démonstration, Ixelles",
    "daysFromNow": 4,
    "startHour": 15,
    "durationHours": 2,
    "organizer": 3,
    "community": "culture",
    "image": 11,
    "capacity": 20,
    "participants": 11,
    "demoParticipates": false
  },
  {
    "title": "Fête de l'indépendance",
    "description": "Célébration conviviale avec musique, danse et cuisine. Toute la famille est la bienvenue.",
    "category": "Culture",
    "city": "Paris",
    "country": "France",
    "address": "Salle des fêtes de démonstration, Montreuil",
    "daysFromNow": 60,
    "startHour": 16,
    "durationHours": 7,
    "organizer": 13,
    "community": "paris",
    "image": 16,
    "capacity": 300,
    "participants": 112,
    "demoParticipates": false
  },
  {
    "title": "Cours de soutien en maths (en ligne)",
    "description": "Session hebdomadaire gratuite pour les élèves de terminale. Exercices de type bac.",
    "category": "Formation",
    "city": "En ligne",
    "country": "",
    "address": "Lien envoyé aux inscrits",
    "daysFromNow": 1,
    "startHour": 18,
    "durationHours": 2,
    "organizer": 10,
    "community": "etudiants-fr",
    "image": 14,
    "capacity": 30,
    "participants": 19,
    "demoParticipates": false
  },
  {
    "title": "Randonnée hivernale",
    "description": "Raquettes et chocolat chaud : une sortie pour apprivoiser l'hiver québécois en groupe.",
    "category": "Sport",
    "city": "Québec",
    "country": "Canada",
    "address": "Parc de démonstration, Québec",
    "daysFromNow": 19,
    "startHour": 9,
    "durationHours": 6,
    "organizer": 6,
    "community": "canada",
    "image": 9,
    "capacity": 25,
    "participants": 15,
    "demoParticipates": false
  },
  {
    "title": "Brunch des entrepreneuses",
    "description": "Échanges entre fondatrices de la diaspora : financement, production au Bénin, distribution en Europe.",
    "category": "Networking",
    "city": "Paris",
    "country": "France",
    "address": "Café de démonstration, Paris 11e",
    "daysFromNow": 16,
    "startHour": 11,
    "durationHours": 3,
    "organizer": 4,
    "community": "entrepreneurs",
    "image": 10,
    "capacity": 20,
    "participants": 13,
    "demoParticipates": true
  },
  {
    "title": "Atelier « oser sa reconversion »",
    "description": "Bilan de compétences express et méthode en trois étapes pour préparer une reconversion.",
    "category": "Formation",
    "city": "Paris",
    "country": "France",
    "address": "Tiers-lieu de démonstration, Paris 12e",
    "daysFromNow": 23,
    "startHour": 10,
    "durationHours": 3,
    "organizer": 32,
    "community": null,
    "image": 12,
    "capacity": 15,
    "participants": 11,
    "demoParticipates": false
  }
];

