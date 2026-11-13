import { AppError } from '../types';
import type {
  DocumentKind, MyProvider, OfferingInput, OutgoingFile, ProviderCard, ProviderDetail, ProviderDocument, ProviderInput, ProviderPage, ReviewPage,
  ServiceCategory, ServiceOffering, ServiceRequestItem, ServiceReview, ServiceSearch, ServicesInfo,
} from '../types';
import { newId } from './random';

const days = (n: number) => new Date(Date.now() - n * 86_400_000).toISOString();

const cat = (id: string, name: string, icon: string, parentId: string | null, q = false): ServiceCategory =>
  ({ id, slug: id, name, icon, parentId, sortOrder: 0, isActive: true, requiresQualification: q, activeProviders: 0, children: [] });

const TREE: ServiceCategory[] = [
  { ...cat('beaute', 'Beauté', 'face-woman-shimmer', null),
    children: [cat('coiffure', 'Coiffure', 'hair-dryer', 'beaute', true), cat('tresses-locks', 'Tresses et locks', 'spa', 'beaute'),
      cat('manucure', 'Manucure et ongles', 'hand-back-left', 'beaute')] },
  { ...cat('maison', 'Maison', 'home-outline', null),
    children: [cat('menage', 'Ménage', 'broom', 'maison'), cat('peinture', 'Peinture', 'format-paint', 'maison')] },
  { ...cat('evenements', 'Événements', 'party-popper', null), children: [cat('traiteur', 'Traiteur', 'silverware-fork-knife', 'evenements')] },
  { ...cat('cours', 'Cours', 'school-outline', null), children: [cat('langues', 'Langues', 'translate', 'cours')] },
];

const leaf = (id: string) => TREE.flatMap((p) => p.children).find((c) => c.id === id)!;

type Provider = Omit<ProviderDetail, 'isFavorite' | 'isMine' | 'reviews' | 'offerings'> & { lat: number; lng: number };

/** Prestataires d'exemple du mode démonstration (hors ligne). */
export class MockServices {
  private providers: Provider[] = [
    { id: 'svc-1', userId: 'demo-provider-1', displayName: 'Afi Tresses (exemple)', bio: "Tresses, nattes collées et locks à domicile. Exemple du mode démonstration.",
      photoPath: null, city: 'Paris', country: 'FR', serviceRadiusKm: 15, servesAtHome: true, hasPremises: false, legalStatus: 'MicroEntrepreneur',
      verificationLevel: 'Pro', ratingAverage: 4.8, ratingCount: 12, responseRate: 0.95, medianResponseMinutes: 45, contactPreference: 'InApp',
      phone: null, memberSince: days(120), hasRegulatedServices: false, lat: 48.8566, lng: 2.3522 },
    { id: 'svc-2', userId: 'demo-provider-2', displayName: 'Koffi Peinture (exemple)', bio: 'Peinture intérieure, finitions soignées. Exemple du mode démonstration.',
      photoPath: null, city: 'Montreuil', country: 'FR', serviceRadiusKm: 25, servesAtHome: true, hasPremises: false, legalStatus: 'Individual',
      verificationLevel: 'Identity', ratingAverage: 4.2, ratingCount: 5, responseRate: 0.8, medianResponseMinutes: 180, contactPreference: 'InApp',
      phone: null, memberSince: days(60), hasRegulatedServices: false, lat: 48.8638, lng: 2.4485 },
    { id: 'svc-3', userId: 'demo-provider-3', displayName: 'Sena Traiteur (exemple)', bio: 'Cuisine béninoise pour vos fêtes. Exemple du mode démonstration.',
      photoPath: null, city: 'Cotonou', country: 'BJ', serviceRadiusKm: 30, servesAtHome: true, hasPremises: true, legalStatus: 'Company',
      verificationLevel: 'None', ratingAverage: 0, ratingCount: 0, responseRate: 0, medianResponseMinutes: null, contactPreference: 'InApp',
      phone: null, memberSince: days(10), hasRegulatedServices: false, lat: 6.3703, lng: 2.3912 },
  ];

  private offerings: (ServiceOffering & { providerId: string })[] = [
    this.offer('svc-1', 'tresses-locks', 'Tresses africaines', 'From', 60),
    this.offer('svc-2', 'peinture', 'Peinture intérieure', 'Hourly', 25),
    this.offer('svc-3', 'traiteur', 'Plats béninois pour vos fêtes', 'From', 15000, 'XOF'),
  ];

  private reviews: (ServiceReview & { providerId: string; requestId: string })[] = [
    { id: 'rev-1', providerId: 'svc-1', requestId: 'r-0', authorId: 'demo-x', authorName: 'Mawuli K.', rating: 5, comment: 'Travail soigné et ponctuelle.',
      createdAt: days(20), updatedAt: null, providerReply: 'Merci beaucoup !', repliedAt: days(19), verified: true, canEdit: false },
  ];

  private favorites = new Set<string>();
  private requests: ServiceRequestItem[] = [];
  private mine: MyProvider | null = null;

  constructor(private readonly me: () => string) {}

  private offer(providerId: string, categoryId: string, title: string, priceType: ServiceOffering['priceType'], price: number, currency = 'EUR') {
    const c = leaf(categoryId);
    return { id: newId(), providerId, categoryId, categoryName: c.name, requiresQualification: c.requiresQualification, title, description: 'Sur rendez-vous.',
      priceType, priceAmount: price, currency, durationMinutes: 120, images: [], isActive: true, hiddenReason: 'None' as const, alertKeyword: null };
  }

  categories = (): ServiceCategory[] => TREE.map((p) => {
    const count = (id: string) => new Set(this.offerings.filter((o) => o.categoryId === id).map((o) => o.providerId)).size;
    const children = p.children.map((c) => ({ ...c, activeProviders: count(c.id) }));
    return { ...p, children, activeProviders: children.reduce((n, c) => n + c.activeProviders, 0) };
  });

  info = (): ServicesInfo => ({
    intermediation: "BéninConnect met en relation des membres. La plateforme n'est pas partie au contrat de prestation.",
    qualifications: 'BéninConnect ne vérifie pas les qualifications des particuliers.',
    reviews: 'Seuls les membres ayant réalisé une prestation via BéninConnect peuvent laisser un avis.',
    taxNotice: 'Les revenus tirés de vos prestations doivent être déclarés.',
    taxLinks: [{ label: 'impots.gouv.fr', url: 'https://www.impots.gouv.fr' }, { label: 'urssaf.fr', url: 'https://www.urssaf.fr' }],
  });

  private card(p: Provider, distanceKm: number | null): ProviderCard {
    const own = this.offerings.filter((o) => o.providerId === p.id);
    const prices = own.filter((o) => o.priceAmount != null && o.priceType !== 'Quote').map((o) => o.priceAmount!);
    const first = own[0];
    return { id: p.id, userId: p.userId, displayName: p.displayName, photoPath: p.photoPath, city: p.city, country: p.country,
      categoryName: first?.categoryName ?? 'Services', categoryIcon: leaf(first?.categoryId ?? 'menage').icon, ratingAverage: p.ratingAverage,
      ratingCount: p.ratingCount, priceFrom: prices.length ? Math.min(...prices) : null, currency: first?.currency ?? 'EUR',
      verificationLevel: p.verificationLevel, servesAtHome: p.servesAtHome, hasPremises: p.hasPremises, distanceKm, responseRate: p.responseRate,
      isFavorite: this.favorites.has(p.id) };
  }

  search(f: ServiceSearch): ProviderPage {
    const ids = f.categoryId ? [f.categoryId, ...TREE.filter((p) => p.id === f.categoryId).flatMap((p) => p.children.map((c) => c.id))] : null;
    const q = f.q?.trim().toLowerCase();
    const items = this.providers
      .filter((p) => this.offerings.some((o) => o.providerId === p.id && (!ids || ids.includes(o.categoryId))
        && (!q || `${o.title} ${o.description} ${p.displayName}`.toLowerCase().includes(q)) && (!f.priceMax || (o.priceAmount ?? Infinity) <= f.priceMax)))
      .filter((p) => !f.atHome || p.servesAtHome)
      .filter((p) => !f.minRating || p.ratingAverage >= f.minRating)
      .filter((p) => !f.verifiedOnly || p.verificationLevel === 'Pro')
      .map((p) => ({ p, d: f.lat != null && f.lng != null ? Math.max(1, Math.round(Math.hypot(p.lat - f.lat, (p.lng - f.lng) * Math.cos(f.lat * Math.PI / 180)) * 111)) : null }))
      .filter((x) => x.d == null || x.d <= (f.radiusKm ?? 25))
      .sort((a, b) => (f.sort === 'Distance' ? (a.d ?? 0) - (b.d ?? 0) : b.p.ratingAverage - a.p.ratingAverage))
      .map((x) => this.card(x.p, x.d));
    return { items, nextCursor: null, total: items.length };
  }

  detail(id: string): ProviderDetail {
    const p = this.providers.find((x) => x.id === id);
    if (!p) throw new AppError('Prestataire introuvable.', 404);
    const { lat: _lat, lng: _lng, ...rest } = p;
    const accepted = this.requests.some((r) => r.providerId === id && r.clientId === this.me() && (r.status === 'Accepted' || r.status === 'Completed'));
    return { ...rest, phone: accepted ? '+33 6 00 00 00 00' : null, isFavorite: this.favorites.has(id), isMine: false,
      offerings: this.offerings.filter((o) => o.providerId === id), reviews: this.reviewPage(id) };
  }

  reviewPage = (id: string): ReviewPage => {
    const items = this.reviews.filter((r) => r.providerId === id);
    return { items, nextCursor: null, total: items.length };
  };

  favorite(id: string, on: boolean) {
    if (on) this.favorites.add(id);
    else this.favorites.delete(id);
  }

  favoriteCards = () => this.providers.filter((p) => this.favorites.has(p.id)).map((p) => this.card(p, null));

  myProvider = () => this.mine;

  saveMine(input: ProviderInput, declared: boolean): MyProvider {
    this.mine = {
      ...(this.mine ?? { id: newId(), status: 'Draft', reviewNote: null, verificationLevel: 'None', hiddenReason: 'None', ratingAverage: 0, ratingCount: 0,
        responseRate: 0, medianResponseMinutes: null, offerings: [], documents: [] }),
      ...input, qualificationDeclared: declared, currency: ['BJ', 'TG', 'SN', 'CI', 'BF', 'ML', 'NE'].includes(input.country) ? 'XOF' : 'EUR',
    };
    return this.mine;
  }

  submitMine(): MyProvider {
    if (!this.mine) throw new AppError('Créez d’abord votre profil prestataire.', 400);
    if (this.mine.offerings.length === 0) throw new AppError("Ajoutez au moins une offre avant l'envoi en vérification.", 400);
    // Mode démonstration : validé aussitôt
    this.mine = { ...this.mine, status: 'Active' };
    return this.mine;
  }

  saveOffering(id: string | null, input: OfferingInput): ServiceOffering {
    if (!this.mine) throw new AppError('Créez d’abord votre profil prestataire.', 400);
    const c = leaf(input.categoryId) ?? cat(input.categoryId, 'Service', 'briefcase-outline', null);
    const offering: ServiceOffering = { id: id ?? newId(), ...input, description: input.description ?? '', categoryName: c.name,
      requiresQualification: c.requiresQualification, currency: this.mine.currency, hiddenReason: 'None', alertKeyword: null };
    this.mine = { ...this.mine, offerings: id ? this.mine.offerings.map((o) => (o.id === id ? offering : o)) : [...this.mine.offerings, offering] };
    return offering;
  }

  deleteOffering(id: string) {
    if (this.mine) this.mine = { ...this.mine, offerings: this.mine.offerings.filter((o) => o.id !== id) };
  }

  addDocument(kind: DocumentKind, file: OutgoingFile): ProviderDocument {
    if (!this.mine) throw new AppError('Créez d’abord votre profil prestataire.', 400);
    const doc: ProviderDocument = { id: newId(), kind, originalName: file.name, contentType: file.mimeType, size: 0, status: 'Pending', reviewNote: null,
      uploadedAt: new Date().toISOString(), reviewedAt: null };
    this.mine = { ...this.mine, documents: [doc, ...this.mine.documents] };
    return doc;
  }

  deleteDocument(id: string) {
    if (this.mine) this.mine = { ...this.mine, documents: this.mine.documents.filter((d) => d.id !== id) };
  }

  createRequest(offeringId: string, message: string, preferredDate: string | null, city: string | null): ServiceRequestItem {
    const offering = this.offerings.find((o) => o.id === offeringId);
    if (!offering) throw new AppError('Offre introuvable.', 404);
    if (message.trim().length < 10) throw new AppError('Votre message : 10 à 2 000 caractères.', 400);
    const provider = this.providers.find((p) => p.id === offering.providerId)!;
    const request: ServiceRequestItem = { id: newId(), status: 'Sent', offeringId, offeringTitle: offering.title, providerId: provider.id,
      providerName: provider.displayName, providerPhoto: null, clientId: this.me(), clientName: 'Moi', message: message.trim(), preferredDate, city,
      conversationId: null, declineReason: null, createdAt: new Date().toISOString(), viewedAt: null, respondedAt: null, completedAt: null,
      providerPhone: null, isProvider: false, canReview: false, reviewId: null };
    this.requests = [request, ...this.requests];
    return request;
  }

  requestsFor = (role: 'Client' | 'Provider') => (role === 'Client' ? this.requests : []);

  request(id: string) {
    const r = this.requests.find((x) => x.id === id);
    if (!r) throw new AppError('Demande introuvable.', 404);
    return r;
  }

  respond(id: string, action: 'accept' | 'decline' | 'complete' | 'cancel'): ServiceRequestItem {
    const r = this.request(id);
    const now = new Date().toISOString();
    // Mode démonstration : le prestataire fictif accepte aussitôt, puis le client peut terminer
    const next: ServiceRequestItem = action === 'complete'
      ? { ...r, status: 'Completed', completedAt: now, canReview: true }
      : action === 'cancel' ? { ...r, status: 'Cancelled' }
        : action === 'accept' ? { ...r, status: 'Accepted', respondedAt: now, providerPhone: '+33 6 00 00 00 00' } : { ...r, status: 'Declined', respondedAt: now };
    this.requests = this.requests.map((x) => (x.id === id ? next : x));
    return next;
  }

  review(requestId: string, rating: number, comment: string | null): ServiceReview {
    const r = this.request(requestId);
    if (r.status !== 'Completed') throw new AppError('Vous pourrez laisser un avis quand la prestation sera terminée.', 400);
    const review = { id: newId(), providerId: r.providerId, requestId, authorId: this.me(), authorName: 'Moi', rating, comment, createdAt: new Date().toISOString(),
      updatedAt: null, providerReply: null, repliedAt: null, verified: true, canEdit: true };
    this.reviews = [review, ...this.reviews];
    this.requests = this.requests.map((x) => (x.id === requestId ? { ...x, canReview: false, reviewId: review.id } : x));
    return review;
  }

  editReview(id: string, rating: number, comment: string | null): ServiceReview {
    const r = this.reviews.find((x) => x.id === id);
    if (!r) throw new AppError('Avis introuvable.', 404);
    Object.assign(r, { rating, comment, updatedAt: new Date().toISOString() });
    return r;
  }
}
