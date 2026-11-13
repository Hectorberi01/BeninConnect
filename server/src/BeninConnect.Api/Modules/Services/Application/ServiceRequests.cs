using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Application;

public enum RequestRole
{
    Client,
    Provider
}

public sealed record CreateServiceRequestCommand(Guid OfferingId, string Message, DateOnly? PreferredDate, string? City) : ICommand<RequestDto>;

public sealed record GetServiceRequestsQuery(RequestRole Role) : IQuery<IReadOnlyList<RequestDto>>;

/// <summary>Ouverte par le prestataire : passe en « vue ».</summary>
public sealed record GetServiceRequestQuery(Guid Id) : IQuery<RequestDto>;

public sealed record AcceptServiceRequestCommand(Guid Id) : ICommand<RequestDto>;

public sealed record DeclineServiceRequestCommand(Guid Id, string? Reason) : ICommand<RequestDto>;

public sealed record CompleteServiceRequestCommand(Guid Id) : ICommand<RequestDto>;

public sealed record CancelServiceRequestCommand(Guid Id) : ICommand<RequestDto>;

public sealed record CreateReviewCommand(Guid RequestId, int Rating, string? Comment) : ICommand<ReviewDto>;

public sealed record EditReviewCommand(Guid Id, int Rating, string? Comment) : ICommand<ReviewDto>;

public sealed record ReplyReviewCommand(Guid Id, string Reply) : ICommand<ReviewDto>;

public sealed record ReportServiceCommand(ReportTarget TargetType, Guid TargetId, string Reason) : ICommand<CountDto>;

/// <summary>Mise en relation : demande → conversation → acceptation → prestation terminée → avis vérifié. Aucun paiement.</summary>
public sealed class ServiceRequestHandlers(
    IServicesDb db, IMessagingModule messaging, IIdentityModule identity, IEventBus bus, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<CreateServiceRequestCommand, RequestDto>,
    IRequestHandler<GetServiceRequestsQuery, IReadOnlyList<RequestDto>>,
    IRequestHandler<GetServiceRequestQuery, RequestDto>,
    IRequestHandler<AcceptServiceRequestCommand, RequestDto>,
    IRequestHandler<DeclineServiceRequestCommand, RequestDto>,
    IRequestHandler<CompleteServiceRequestCommand, RequestDto>,
    IRequestHandler<CancelServiceRequestCommand, RequestDto>,
    IRequestHandler<CreateReviewCommand, ReviewDto>,
    IRequestHandler<EditReviewCommand, ReviewDto>,
    IRequestHandler<ReplyReviewCommand, ReviewDto>,
    IRequestHandler<ReportServiceCommand, CountDto>
{
    public async Task<RequestDto> Handle(CreateServiceRequestCommand r, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var offering = await db.Offerings.AsNoTracking().FirstOrDefaultAsync(o => o.Id == r.OfferingId, ct) ?? throw new NotFoundException("Offre");
        var provider = await db.Providers.FirstAsync(p => p.Id == offering.ProviderId, ct);
        var request = ServiceRequest.Create(offering, provider, me.Id, r.Message, r.PreferredDate, r.City, now);

        // Anti-spam : une seule demande en cours par offre, nombre de demandes par jour
        if (await db.Requests.AnyAsync(x => x.ClientId == me.Id && x.OfferingId == offering.Id
                                            && (x.Status == RequestStatus.Sent || x.Status == RequestStatus.Viewed || x.Status == RequestStatus.Accepted), ct))
        {
            throw new ConflictException("Vous avez déjà une demande en cours pour cette offre : poursuivez la conversation.");
        }

        var settings = await ServicesData.SettingsAsync(db, ct);
        if (await db.Requests.CountAsync(x => x.ClientId == me.Id && x.CreatedAt > now.AddDays(-1), ct) >= settings.DailyRequestLimit)
        {
            throw new DomainException($"Limite atteinte : {settings.DailyRequestLimit} demandes par jour. Réessayez demain.");
        }

        // Le message part dans la messagerie existante (conversation directe)
        var text = $"Demande pour « {offering.Title} »" + (r.PreferredDate is { } d ? $" (date souhaitée : {d:dd/MM/yyyy})" : "") +
                   (string.IsNullOrWhiteSpace(r.City) ? "" : $" à {request.City}") + $"\n\n{request.Message}";
        request.AttachConversation(await messaging.SendDirectAsync(me.Id, provider.UserId, text, notifyRecipient: false, ct));
        db.Requests.Add(request);
        await db.SaveChangesAsync(ct);

        var client = await NameAsync(me.Id, ct);
        await ServicesData.NotifyAsync(bus, provider.UserId, $"Nouvelle demande de {client}", $"{offering.Title} : {request.Message}", request.ConversationId, ct);
        return (await DescribeAsync([request], ct))[0];
    }

    public async Task<IReadOnlyList<RequestDto>> Handle(GetServiceRequestsQuery r, CancellationToken ct)
    {
        var query = r.Role == RequestRole.Provider ? db.Requests.Where(x => x.ProviderUserId == me.Id) : db.Requests.Where(x => x.ClientId == me.Id);
        var list = await query.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        return await DescribeAsync(list, ct);
    }

    public async Task<RequestDto> Handle(GetServiceRequestQuery r, CancellationToken ct)
    {
        var request = await MineAsync(r.Id, ct);
        if (request.ProviderUserId == me.Id && request.Status == RequestStatus.Sent)
        {
            request.MarkViewed(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }

        return (await DescribeAsync([request], ct))[0];
    }

    public Task<RequestDto> Handle(AcceptServiceRequestCommand r, CancellationToken ct) =>
        ChangeAsync(r.Id, x => x.Accept(me.Id, clock.GetUtcNow()), respond: true,
            async x => await ServicesData.NotifyAsync(bus, x.ClientId, "Demande acceptée",
                $"{await ProviderNameAsync(x.ProviderId, ct)} a accepté votre demande. Convenez des détails dans la conversation.", x.ConversationId, ct), ct);

    public Task<RequestDto> Handle(DeclineServiceRequestCommand r, CancellationToken ct) =>
        ChangeAsync(r.Id, x => x.Decline(me.Id, r.Reason, clock.GetUtcNow()), respond: true,
            async x => await ServicesData.NotifyAsync(bus, x.ClientId, "Demande refusée",
                $"{await ProviderNameAsync(x.ProviderId, ct)} n'est pas disponible pour cette demande." +
                (x.DeclineReason is null ? "" : $" Motif : {x.DeclineReason}"), x.ConversationId, ct), ct);

    public Task<RequestDto> Handle(CompleteServiceRequestCommand r, CancellationToken ct) =>
        ChangeAsync(r.Id, x => x.Complete(me.Id, clock.GetUtcNow()), respond: false, async x =>
        {
            var other = x.ClientId == me.Id ? x.ProviderUserId : x.ClientId;
            var body = other == x.ClientId ? "Prestation marquée comme terminée : vous pouvez laisser un avis." : "Le client a marqué la prestation comme terminée.";
            await ServicesData.NotifyAsync(bus, other, "Prestation terminée", body, x.ConversationId, ct);
        }, ct);

    public Task<RequestDto> Handle(CancelServiceRequestCommand r, CancellationToken ct) =>
        ChangeAsync(r.Id, x => x.Cancel(me.Id, clock.GetUtcNow()), respond: false,
            async x => await ServicesData.NotifyAsync(bus, x.ProviderUserId, "Demande annulée", $"{await NameAsync(x.ClientId, ct)} a annulé sa demande.",
                x.ConversationId, ct), ct);

    public async Task<ReviewDto> Handle(CreateReviewCommand r, CancellationToken ct)
    {
        var request = await db.Requests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.RequestId && (x.ClientId == me.Id || x.ProviderUserId == me.Id), ct)
                      ?? throw new NotFoundException("Demande");
        if (await db.Reviews.AnyAsync(x => x.RequestId == request.Id, ct))
        {
            throw new ConflictException("Vous avez déjà laissé un avis pour cette prestation.");
        }

        var review = Review.Create(request, me.Id, r.Rating, r.Comment, clock.GetUtcNow());
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        await ServicesData.RefreshRatingAsync(db, review.ProviderId, ct);
        await db.SaveChangesAsync(ct);
        await ServicesData.NotifyAsync(bus, request.ProviderUserId, $"Nouvel avis : {review.Rating}/5", review.Comment ?? "Un client a noté votre prestation.",
            null, ct);
        return await ReviewDtoAsync(review, ct);
    }

    public async Task<ReviewDto> Handle(EditReviewCommand r, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException("Avis");
        review.Edit(me.Id, r.Rating, r.Comment, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await ServicesData.RefreshRatingAsync(db, review.ProviderId, ct);
        await db.SaveChangesAsync(ct);
        return await ReviewDtoAsync(review, ct);
    }

    public async Task<ReviewDto> Handle(ReplyReviewCommand r, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException("Avis");
        if (!await db.Providers.AnyAsync(p => p.Id == review.ProviderId && p.UserId == me.Id, ct))
        {
            throw new ForbiddenException("Seul le prestataire concerné peut répondre à cet avis.");
        }

        review.Reply(r.Reply, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ReviewDtoAsync(review, ct);
    }

    /// <summary>Un signalement par membre et par cible ; au seuil, la cible est masquée en attendant la revue.</summary>
    public async Task<CountDto> Handle(ReportServiceCommand r, CancellationToken ct)
    {
        if (await db.Reports.AnyAsync(x => x.TargetType == r.TargetType && x.TargetId == r.TargetId && x.AuthorId == me.Id, ct))
        {
            throw new ConflictException("Vous avez déjà signalé ce contenu.");
        }

        var report = ServiceReport.Create(r.TargetType, r.TargetId, me.Id, r.Reason, clock.GetUtcNow());
        var threshold = (await ServicesData.SettingsAsync(db, ct)).ReportThreshold;
        int count;
        switch (r.TargetType)
        {
            case ReportTarget.Profile:
                var profile = await db.Providers.FirstOrDefaultAsync(p => p.Id == r.TargetId, ct) ?? throw new NotFoundException("Prestataire");
                NotOwn(profile.UserId);
                profile.AddReport(threshold);
                count = profile.ReportCount;
                break;
            case ReportTarget.Offering:
                var offering = await db.Offerings.FirstOrDefaultAsync(o => o.Id == r.TargetId, ct) ?? throw new NotFoundException("Offre");
                NotOwn((await db.Providers.AsNoTracking().FirstAsync(p => p.Id == offering.ProviderId, ct)).UserId);
                offering.AddReport(threshold);
                count = offering.ReportCount;
                break;
            default:
                var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == r.TargetId && x.Status == ReviewStatus.Published, ct)
                             ?? throw new NotFoundException("Avis");
                NotOwn(review.AuthorId);
                var hidden = review.AddReport(threshold);
                count = review.ReportCount;
                db.Reports.Add(report);
                await db.SaveChangesAsync(ct);
                if (hidden)
                {
                    await ServicesData.RefreshRatingAsync(db, review.ProviderId, ct);
                    await db.SaveChangesAsync(ct);
                }

                return new CountDto(count);
        }

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
        return new CountDto(count);
    }

    private void NotOwn(Guid ownerId)
    {
        if (ownerId == me.Id)
        {
            throw new DomainException("Vous ne pouvez pas signaler votre propre contenu.");
        }
    }

    private async Task<RequestDto> ChangeAsync(Guid id, Action<ServiceRequest> change, bool respond, Func<ServiceRequest, Task> notify, CancellationToken ct)
    {
        var request = await MineAsync(id, ct);
        change(request);
        await db.SaveChangesAsync(ct);
        if (respond)
        {
            var provider = await db.Providers.FirstAsync(p => p.Id == request.ProviderId, ct);
            await ServicesData.RefreshResponseStatsAsync(db, provider, ct);
            await db.SaveChangesAsync(ct);
        }

        await notify(request);
        return (await DescribeAsync([request], ct))[0];
    }

    private async Task<ServiceRequest> MineAsync(Guid id, CancellationToken ct) =>
        await db.Requests.FirstOrDefaultAsync(x => x.Id == id && (x.ClientId == me.Id || x.ProviderUserId == me.Id), ct)
        ?? throw new NotFoundException("Demande");

    private async Task<string> NameAsync(Guid userId, CancellationToken ct) =>
        (await identity.GetNamesAsync([userId], ct)).GetValueOrDefault(userId) ?? "Un membre";

    private async Task<string> ProviderNameAsync(Guid providerId, CancellationToken ct) =>
        await db.Providers.Where(p => p.Id == providerId).Select(p => p.DisplayName).FirstOrDefaultAsync(ct) ?? "Le prestataire";

    private async Task<ReviewDto> ReviewDtoAsync(Review r, CancellationToken ct) =>
        new(r.Id, r.AuthorId, ServiceSearchHandlers.ShortName(await NameAsync(r.AuthorId, ct)), r.Rating, r.Comment, r.CreatedAt, r.UpdatedAt,
            r.ProviderReply, r.RepliedAt, true, r.AuthorId == me.Id && clock.GetUtcNow() <= r.CreatedAt.AddDays(ServiceRules.ReviewEditDays));

    private async Task<List<RequestDto>> DescribeAsync(IReadOnlyList<ServiceRequest> requests, CancellationToken ct)
    {
        var offeringIds = requests.Select(x => x.OfferingId).Distinct().ToList();
        var providerIds = requests.Select(x => x.ProviderId).Distinct().ToList();
        var requestIds = requests.Select(x => x.Id).ToList();
        var titles = await db.Offerings.AsNoTracking().Where(o => offeringIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Title, ct);
        var providers = await db.Providers.AsNoTracking().Where(p => providerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var reviews = await db.Reviews.AsNoTracking().Where(x => requestIds.Contains(x.RequestId)).ToDictionaryAsync(x => x.RequestId, x => x.Id, ct);
        var names = await identity.GetNamesAsync(requests.Select(x => x.ClientId).Distinct(), ct);
        return requests.Select(x =>
        {
            var provider = providers.GetValueOrDefault(x.ProviderId);
            var isProvider = x.ProviderUserId == me.Id;
            var phoneVisible = provider?.Phone is not null && (isProvider || provider.PhoneIsPublic || x.Status is RequestStatus.Accepted or RequestStatus.Completed);
            var reviewId = reviews.TryGetValue(x.Id, out var rid) ? rid : (Guid?)null;
            return new RequestDto(x.Id, x.Status, x.OfferingId, titles.GetValueOrDefault(x.OfferingId) ?? "Offre supprimée", x.ProviderId,
                provider?.DisplayName ?? "Prestataire", provider?.PhotoPath, x.ClientId, names.GetValueOrDefault(x.ClientId) ?? "Membre", x.Message,
                x.PreferredDate, x.City, x.ConversationId, x.DeclineReason, x.CreatedAt, x.ViewedAt, x.RespondedAt, x.CompletedAt,
                phoneVisible ? provider!.Phone : null, isProvider, !isProvider && x.Status == RequestStatus.Completed && reviewId is null, reviewId);
        }).ToList();
    }
}
