import { AppError } from '../types';
import type {
  Booking, BookingInput, DeliveryCode, MyTrip, RouteAlert, RouteAlertInput, TripCard, TripDetail, TripInput, TripPage, TripSearch, User, VoyagePerson,
  VoyageReview, VoyageReviewPage, VoyagesInfo, VoyageStats,
} from '../types';
import { newId } from './random';

const inDays = (n: number) => {
  const d = new Date(Date.now() + n * 86_400_000);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
const today = () => inDays(0);
const key = (city: string) => city.trim().toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, ' ').trim();

type Trip = Omit<TripCard, 'remainingKg' | 'isMine' | 'travelerRating' | 'travelerReviewCount' | 'trustedTraveler'> & {
  dropOffZones: string | null; notes: string | null; createdAt: string;
};
type StoredBooking = Omit<Booking, 'fromCity' | 'toCity' | 'departureDate' | 'tripStatus' | 'iAmTraveler' | 'hasCode' | 'canReview' | 'myReviewId'> & {
  code: string | null; reviews: string[];
};

const person = (id: string, name: string, avatarColor: string): VoyagePerson => ({ id, name, avatarColor, avatarImage: null });
const SAMPLES = [person('demo-traveler-1', 'Afi K. (exemple)', '#2E7D5B'), person('demo-traveler-2', 'Codjo A. (exemple)', '#C2571B')];

export const VOYAGES_INFO: VoyagesInfo = {
  intermediation: 'BéninConnect met en relation des voyageurs et des expéditeurs. La plateforme ne transporte rien et aucun paiement ne transite par l’application.',
  travelerResponsibility: 'Le voyageur est personnellement responsable de ce qu’il transporte et déclare en douane.',
  prohibitedItems: ['Stupéfiants et substances illicites', 'Armes, munitions, objets tranchants', 'Argent liquide', 'Médicaments sans ordonnance',
    'Contrefaçons', 'Produits périssables', 'Objets de valeur non déclarés', 'Colis fermés dont le contenu n’a pas été vérifié'],
  travelerAdvice: ['Ouvrez et vérifiez chaque colis à la remise : n’acceptez jamais un colis fermé.', 'Prenez une photo du contenu au moment de la remise.',
    'Refusez en cas de doute, sans avoir à vous justifier.', 'À la livraison, demandez le code à 6 chiffres au destinataire : c’est la preuve de la remise.'],
  customsLinks: [{ label: 'Douane française : franchises des voyageurs', url: 'https://www.douane.gouv.fr/fiche/franchises-douanieres-et-fiscales-en-quantites-et-en-valeurs' },
    { label: 'Douanes du Bénin', url: 'https://douanes.gouv.bj' }],
  maxDaysAhead: 90, minBookingKg: 0.5, maxBookingKg: 50, maxCapacityKg: 100, maxAlerts: 5,
};

/** Trajets d'exemple du mode démonstration (hors ligne). */
export class MockVoyages {
  private trips: Trip[] = [
    { id: 'trip-1', traveler: SAMPLES[0]!, fromCountry: 'FR', fromCity: 'Paris', toCountry: 'BJ', toCity: 'Cotonou', departureDate: inDays(12), capacityKg: 8,
      pricePerKg: 10, currency: 'EUR', acceptedCategories: ['Documents', 'Clothes', 'Cosmetics'], status: 'Open', dropOffZones: 'Paris 11e, Gare du Nord',
      notes: 'Exemple du mode démonstration.', createdAt: new Date().toISOString() },
    { id: 'trip-2', traveler: SAMPLES[1]!, fromCountry: 'BJ', fromCity: 'Cotonou', toCountry: 'FR', toCity: 'Lyon', departureDate: inDays(20), capacityKg: 5,
      pricePerKg: 5000, currency: 'XOF', acceptedCategories: ['DryFood', 'Clothes'], status: 'Open', dropOffZones: 'Cadjèhoun', notes: null,
      createdAt: new Date().toISOString() },
  ];
  private bookings: StoredBooking[] = [];
  private alerts: RouteAlert[] = [];
  private reviews: (VoyageReview & { targetId: string })[] = [];

  constructor(private readonly me: () => string, private readonly user: (id: string) => User | undefined) {}

  private person(id: string): VoyagePerson {
    const sample = SAMPLES.find((p) => p.id === id);
    if (sample) return sample;
    const u = this.user(id);
    return u ? person(u.id, `${u.firstName} ${u.lastName.charAt(0)}.`, u.avatarColor) : person(id, 'Membre', '#2E7D5B');
  }

  private reserved = (t: Trip) => this.bookings.filter((b) => b.tripId === t.id && ['Accepted', 'Delivered', 'Disputed'].includes(b.status))
    .reduce((s, b) => s + b.weightKg, 0);

  private card(t: Trip): TripCard {
    const rated = this.reviews.filter((r) => r.targetId === t.traveler.id && r.aboutTraveler);
    const avg = rated.length ? rated.reduce((s, r) => s + r.rating, 0) / rated.length : 0;
    return { ...t, remainingKg: Math.max(0, t.capacityKg - this.reserved(t)), isMine: t.traveler.id === this.me(), travelerRating: Math.round(avg * 10) / 10,
      travelerReviewCount: rated.length, trustedTraveler: rated.filter((r) => r.rating >= 4).length >= 3 };
  }

  private trip(id: string) {
    const t = this.trips.find((x) => x.id === id);
    if (!t) throw new AppError('Trajet introuvable.', 404);
    return t;
  }

  private view(b: StoredBooking): Booking {
    const t = this.trip(b.tripId);
    const { code, reviews, ...rest } = b;
    const mine = reviews.includes(this.me());
    return { ...rest, fromCity: t.fromCity, toCity: t.toCity, departureDate: t.departureDate, tripStatus: t.status, iAmTraveler: b.traveler.id === this.me(),
      hasCode: !!code && b.status === 'Accepted', canReview: b.status === 'Delivered' && !mine, myReviewId: mine ? 'review' : null };
  }

  private detail(t: Trip): TripDetail {
    const mine = this.bookings.find((b) => b.tripId === t.id && b.sender.id === this.me());
    const card = this.card(t);
    const active = mine && ['Pending', 'Accepted', 'Disputed'].includes(mine.status);
    return { trip: card, dropOffZones: t.dropOffZones, notes: t.notes, travelerDeliveries: 0, myBooking: mine ? this.view(mine) : null,
      canBook: !card.isMine && t.status === 'Open' && card.remainingKg >= 0.5 && !active, hiddenReason: 'None', createdAt: t.createdAt };
  }

  search(f: TripSearch): TripPage {
    const items = this.trips
      .filter((t) => t.status === 'Open' && t.departureDate >= today() && (!f.from || t.fromCountry === f.from) && (!f.to || t.toCountry === f.to)
        && (!f.fromCity || key(t.fromCity) === key(f.fromCity)) && (!f.toCity || key(t.toCity) === key(f.toCity))
        && (!f.after || t.departureDate >= f.after) && (!f.before || t.departureDate <= f.before)
        && (!f.category || t.acceptedCategories.includes(f.category)))
      .sort((a, b) => a.departureDate.localeCompare(b.departureDate)).map((t) => this.card(t));
    return { items, nextCursor: null, total: items.length };
  }

  get = (id: string) => this.detail(this.trip(id));

  publish(input: TripInput, id?: string): TripDetail {
    if (input.departureDate < today()) throw new AppError('Date de départ : d’aujourd’hui à 90 jours au plus.', 400);
    if (!input.acceptedCategories.length) throw new AppError('Choisissez au moins une catégorie acceptée.', 400);
    const base = { ...input, status: 'Open' as const };
    if (id) {
      const t = this.trip(id);
      Object.assign(t, base);
      return this.detail(t);
    }
    const t: Trip = { ...base, id: newId(), traveler: this.person(this.me()), createdAt: new Date().toISOString() };
    this.trips.unshift(t);
    return this.detail(t);
  }

  cancel(id: string) {
    const t = this.trip(id);
    t.status = 'Cancelled';
    this.bookings.filter((b) => b.tripId === id && ['Pending', 'Accepted'].includes(b.status)).forEach((b) => { b.status = 'Cancelled'; });
    return this.detail(t);
  }

  mine(): MyTrip[] {
    return this.trips.filter((t) => t.traveler.id === this.me()).map((t) => {
      const list = this.bookings.filter((b) => b.tripId === t.id).map((b) => this.view(b));
      return { detail: this.detail(t), bookings: list, pendingCount: list.filter((b) => b.status === 'Pending').length };
    });
  }

  request(tripId: string, input: BookingInput): Booking {
    const t = this.trip(tripId);
    if (!input.certified) throw new AppError('Certifiez que le colis ne contient aucun objet interdit.', 400);
    if (t.traveler.id === this.me()) throw new AppError('Vous ne pouvez pas réserver votre propre trajet.', 400);
    if (this.bookings.some((b) => b.tripId === tripId && b.sender.id === this.me() && ['Pending', 'Accepted', 'Disputed'].includes(b.status))) {
      throw new AppError('Vous avez déjà une réservation sur ce trajet.', 409);
    }
    // Mode démonstration : le voyageur d'exemple accepte aussitôt
    const b: StoredBooking = { id: newId(), tripId, sender: this.person(this.me()), traveler: t.traveler, weightKg: input.weightKg,
      category: input.category, description: input.description, status: 'Accepted', declineReason: null, conversationId: null, codeLocked: false,
      attemptsLeft: 5, createdAt: new Date().toISOString(), respondedAt: new Date().toISOString(), deliveredAt: null, disputeReason: null,
      resolutionNote: null, code: null, reviews: [] };
    this.bookings.unshift(b);
    return this.view(b);
  }

  myBookings = () => this.bookings.filter((b) => b.sender.id === this.me()).map((b) => this.view(b));

  private booking(id: string) {
    const b = this.bookings.find((x) => x.id === id);
    if (!b) throw new AppError('Réservation introuvable.', 404);
    return b;
  }

  respond(id: string, action: 'accept' | 'decline' | 'cancel') {
    const b = this.booking(id);
    b.status = action === 'accept' ? 'Accepted' : action === 'decline' ? 'Declined' : 'Cancelled';
    return this.view(b);
  }

  issueCode(id: string): DeliveryCode {
    const b = this.booking(id);
    b.code = String(Math.floor(Math.random() * 1_000_000)).padStart(6, '0');
    b.attemptsLeft = 5;
    b.codeLocked = false;
    // Mode démonstration : la livraison est confirmée tout de suite pour pouvoir tester l'avis
    const issued = b.code;
    b.status = 'Delivered';
    b.deliveredAt = new Date().toISOString();
    return { code: issued, shareText: `Code de remise BéninConnect : ${issued}.` };
  }

  deliver(id: string, code: string) {
    const b = this.booking(id);
    if (b.code !== code.trim()) {
      b.attemptsLeft = Math.max(0, b.attemptsLeft - 1);
      b.codeLocked = b.attemptsLeft === 0;
      throw new AppError(`Code erroné. Il reste ${b.attemptsLeft} essai(s).`, 400);
    }
    b.status = 'Delivered';
    b.deliveredAt = new Date().toISOString();
    return this.view(b);
  }

  dispute(id: string, reason: string) {
    const b = this.booking(id);
    b.status = 'Disputed';
    b.disputeReason = reason;
    return this.view(b);
  }

  review(id: string, rating: number, comment: string | null): VoyageReview {
    const b = this.booking(id);
    if (b.status !== 'Delivered') throw new AppError('Les avis s’ouvrent une fois la livraison confirmée.', 400);
    b.reviews.push(this.me());
    const aboutTraveler = b.sender.id === this.me();
    const review = { id: newId(), author: this.person(this.me()), rating, comment, createdAt: new Date().toISOString(), aboutTraveler,
      targetId: aboutTraveler ? b.traveler.id : b.sender.id };
    this.reviews.unshift(review);
    return review;
  }

  stats(userId: string): VoyageStats {
    const rated = this.reviews.filter((r) => r.targetId === userId && r.aboutTraveler);
    const avg = rated.length ? rated.reduce((s, r) => s + r.rating, 0) / rated.length : 0;
    return {
      deliveriesAsTraveler: this.bookings.filter((b) => b.traveler.id === userId && b.status === 'Delivered').length,
      shipmentsDelivered: this.bookings.filter((b) => b.sender.id === userId && b.status === 'Delivered').length,
      ratingAverage: Math.round(avg * 10) / 10, ratingCount: rated.length, trustedTraveler: rated.filter((r) => r.rating >= 4).length >= 3,
      upcomingTrips: this.trips.filter((t) => t.traveler.id === userId && t.status === 'Open' && t.departureDate >= today()).map((t) => this.card(t)),
    };
  }

  reviewPage(userId: string): VoyageReviewPage {
    const items = this.reviews.filter((r) => r.targetId === userId);
    const s = this.stats(userId);
    return { items, ratingAverage: s.ratingAverage, ratingCount: s.ratingCount };
  }

  alertList = () => this.alerts;

  addAlert(input: RouteAlertInput) {
    if (this.alerts.length >= 5) throw new AppError('5 alertes au plus : supprimez-en une.', 400);
    const alert: RouteAlert = { ...input, id: newId(), createdAt: new Date().toISOString() };
    this.alerts.push(alert);
    return alert;
  }

  removeAlert(id: string) {
    this.alerts = this.alerts.filter((a) => a.id !== id);
  }
}
