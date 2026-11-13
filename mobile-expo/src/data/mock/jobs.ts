import type { JobOffer } from '../types';

const days = (n: number) => new Date(Date.now() - n * 86_400_000).toISOString();

type Seed = Omit<JobOffer, 'isFavorite' | 'reportedByMe' | 'status' | 'latitude' | 'longitude' | 'postalCode' | 'companyLogoUrl' | 'closingAt'>
  & Partial<Pick<JobOffer, 'closingAt'>>;

/** Offres d'exemple du mode démonstration (hors ligne). */
const SEEDS: Seed[] = [
  {
    id: 'job-demo-1', title: 'Développeur .NET (H/F)', companyName: 'Exemple Services', locationLabel: '75 - Paris', country: 'FR', department: '75',
    contractType: 'Cdi', contractLabel: 'Contrat à durée indéterminée', isRemote: false, publishedAt: days(1), source: 'FranceTravail',
    sourceLabel: 'France Travail', workingTime: 'Temps plein', salaryLabel: 'Annuel de 45 000 à 55 000 €', sectorLabel: 'Programmation informatique',
    applyUrl: 'https://candidat.francetravail.fr/offres/emploi',
    description: "Offre d'exemple du mode démonstration.\n\nVous rejoignez une équipe produit pour développer des API .NET et des applications web.",
  },
  {
    id: 'job-demo-2', title: 'Préparateur de commandes (H/F)', companyName: 'Exemple Logistique', locationLabel: '93 - Saint-Denis', country: 'FR',
    department: '93', contractType: 'Interim', contractLabel: 'Mission intérimaire', isRemote: false, publishedAt: days(2), source: 'FranceTravail',
    sourceLabel: 'France Travail', workingTime: 'Temps plein', salaryLabel: 'Horaire de 12,50 €', sectorLabel: 'Entreposage',
    applyUrl: 'https://candidat.francetravail.fr/offres/emploi',
    description: "Offre d'exemple du mode démonstration.\n\nPréparation et contrôle des commandes en entrepôt, travail en équipe du matin.",
  },
  {
    id: 'job-demo-3', title: 'Coordinateur de projet santé', companyName: 'ONG Exemple', locationLabel: 'Cotonou, Benin', country: 'BJ', department: null,
    contractType: 'Autre', contractLabel: 'Job', isRemote: false, publishedAt: days(3), source: 'ReliefWeb', sourceLabel: 'ReliefWeb · ONG Exemple',
    workingTime: null, salaryLabel: null, sectorLabel: 'Program/Project Management', applyUrl: 'https://reliefweb.int/jobs',
    closingAt: new Date(Date.now() + 20 * 86_400_000).toISOString(),
    description: "Offre d'exemple du mode démonstration.\n\nCoordination d'un projet de santé communautaire dans le sud du Bénin.",
  },
  {
    id: 'job-demo-4', title: 'Chargé de suivi-évaluation (stage)', companyName: 'Agence Exemple', locationLabel: 'Lomé, Togo', country: 'TG', department: null,
    contractType: 'Stage', contractLabel: 'Internship', isRemote: false, publishedAt: days(4), source: 'ReliefWeb', sourceLabel: 'ReliefWeb · Agence Exemple',
    workingTime: null, salaryLabel: null, sectorLabel: 'Monitoring and Evaluation', applyUrl: 'https://reliefweb.int/jobs',
    description: "Offre d'exemple du mode démonstration.\n\nAppui à la collecte et à l'analyse des données d'un programme régional.",
  },
  {
    id: 'job-demo-5', title: 'Comptable en alternance', companyName: 'Cabinet Exemple', locationLabel: '69 - Lyon', country: 'FR', department: '69',
    contractType: 'Alternance', contractLabel: "Contrat d'apprentissage", isRemote: false, publishedAt: days(5), source: 'FranceTravail',
    sourceLabel: 'France Travail', workingTime: 'Temps plein', salaryLabel: null, sectorLabel: 'Activités comptables',
    applyUrl: 'https://candidat.francetravail.fr/offres/emploi',
    description: "Offre d'exemple du mode démonstration.\n\nPréparation de dossiers comptables aux côtés d'un expert-comptable.",
  },
];

export const createMockJobs = (): JobOffer[] =>
  SEEDS.map((s) => ({
    closingAt: null, ...s, status: 'Active', isFavorite: false, reportedByMe: false, latitude: null, longitude: null, postalCode: null,
    companyLogoUrl: null,
  }));
