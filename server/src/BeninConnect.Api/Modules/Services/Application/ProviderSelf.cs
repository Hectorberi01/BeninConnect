using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Application;

public sealed record GetMyProviderQuery : IQuery<MeDto>;

public sealed record SaveMyProviderCommand(ProviderDetails Details, bool QualificationDeclared) : ICommand<MyProviderDto>;

/// <summary>Envoi en vérification (première publication ou après un refus).</summary>
public sealed record SubmitMyProviderCommand : ICommand<MyProviderDto>;

public sealed record SaveOfferingCommand(Guid? Id, OfferingDetails Details) : ICommand<OfferingDto>;

public sealed record DeleteOfferingCommand(Guid Id) : ICommand<CountDto>;

public sealed record UploadServiceImageCommand(Stream Content, long Length) : ICommand<UploadedImageDto>;

public sealed record UploadDocumentCommand(DocumentKind Kind, Stream Content, long Length, string? FileName) : ICommand<DocumentDto>;

public sealed record DeleteDocumentCommand(Guid Id) : ICommand<CountDto>;

/// <summary>Espace prestataire : profil, offres, portfolio, justificatifs.</summary>
public sealed class ProviderSelfHandlers(IServicesDb db, IMediaModule media, IEventBus bus, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<GetMyProviderQuery, MeDto>,
    IRequestHandler<SaveMyProviderCommand, MyProviderDto>,
    IRequestHandler<SubmitMyProviderCommand, MyProviderDto>,
    IRequestHandler<SaveOfferingCommand, OfferingDto>,
    IRequestHandler<DeleteOfferingCommand, CountDto>,
    IRequestHandler<UploadServiceImageCommand, UploadedImageDto>,
    IRequestHandler<UploadDocumentCommand, DocumentDto>,
    IRequestHandler<DeleteDocumentCommand, CountDto>
{
    public async Task<MeDto> Handle(GetMyProviderQuery request, CancellationToken ct)
    {
        var profile = await db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == me.Id, ct);
        return profile is null ? new MeDto(false, null) : new MeDto(true, await DescribeAsync(profile, ct));
    }

    public async Task<MyProviderDto> Handle(SaveMyProviderCommand request, CancellationToken ct)
    {
        await ServicesData.EnsureCatalogAsync(db, ct);
        var now = clock.GetUtcNow();
        var profile = await db.Providers.FirstOrDefaultAsync(p => p.UserId == me.Id, ct);
        var oldPhoto = profile?.PhotoPath;
        if (profile is null)
        {
            profile = ProviderProfile.Create(me.Id, request.Details, request.QualificationDeclared, now);
            db.Providers.Add(profile);
        }
        else
        {
            profile.Update(request.Details, request.QualificationDeclared, now);
            // Sans incidence sur les offres : leur devise suit le pays du profil
            foreach (var offering in await db.Offerings.Where(o => o.ProviderId == profile.Id).ToListAsync(ct))
            {
                offering.Update(Details(offering), profile.Currency, offering.UpdatedAt);
            }
        }

        // Profil publié : la présentation passe le filtre de mots-clés
        var settings = await ServicesData.SettingsAsync(db, ct);
        if (profile.Status == ProviderStatus.Active && profile.HiddenReason == HiddenReason.None &&
            ServiceRules.FindAlert(settings.AlertKeywords, profile.DisplayName, profile.Bio) is { } keyword)
        {
            profile.FlagAlert(keyword);
        }

        await db.SaveChangesAsync(ct);
        if (oldPhoto is not null && oldPhoto != profile.PhotoPath)
        {
            await bus.PublishAsync(new ImagesReleased([oldPhoto]), ct);
        }

        return await DescribeAsync(profile, ct);
    }

    public async Task<MyProviderDto> Handle(SubmitMyProviderCommand request, CancellationToken ct)
    {
        var profile = await MineAsync(ct);
        var offerings = await db.Offerings.Where(o => o.ProviderId == profile.Id && o.IsActive).ToListAsync(ct);
        var categories = offerings.Select(o => o.CategoryId).ToList();
        var regulated = await db.Categories.AnyAsync(c => categories.Contains(c.Id) && c.RequiresQualification, ct);
        profile.Submit(offerings.Count, regulated, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await DescribeAsync(profile, ct);
    }

    public async Task<OfferingDto> Handle(SaveOfferingCommand request, CancellationToken ct)
    {
        var profile = await MineAsync(ct);
        if (profile.Status == ProviderStatus.Suspended)
        {
            throw new DomainException("Profil suspendu : contactez l'équipe BéninConnect.");
        }

        var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.Details.CategoryId && c.IsActive, ct)
                       ?? throw new DomainException("Choisissez une catégorie.");
        if (await db.Categories.AnyAsync(c => c.ParentId == category.Id, ct))
        {
            throw new DomainException($"Choisissez une sous-catégorie de « {category.Name} ».");
        }

        foreach (var image in request.Details.Images)
        {
            if (Rules.ImagePath("Images", image).Any())
            {
                throw new DomainException("Photo invalide.");
            }
        }

        var now = clock.GetUtcNow();
        ServiceOffering offering;
        var released = new List<string>();
        if (request.Id is { } id)
        {
            offering = await db.Offerings.FirstOrDefaultAsync(o => o.Id == id && o.ProviderId == profile.Id, ct) ?? throw new NotFoundException("Offre");
            released.AddRange(offering.Images.Except(request.Details.Images));
            offering.Update(request.Details, profile.Currency, now);
            offering.ClearAlert();
        }
        else
        {
            if (await db.Offerings.CountAsync(o => o.ProviderId == profile.Id, ct) >= 30)
            {
                throw new DomainException("30 offres au plus par prestataire.");
            }

            offering = ServiceOffering.Create(profile.Id, request.Details, profile.Currency, now);
            db.Offerings.Add(offering);
        }

        // Publiée directement, sauf mot-clé d'alerte : masquée et placée dans la file de la console
        var settings = await ServicesData.SettingsAsync(db, ct);
        if (!offering.IsHidden && ServiceRules.FindAlert(settings.AlertKeywords, offering.Title, offering.Description) is { } keyword)
        {
            offering.FlagAlert(keyword);
        }

        await db.SaveChangesAsync(ct);
        if (released.Count > 0)
        {
            await bus.PublishAsync(new ImagesReleased(released), ct);
        }

        return offering.ToDto(new Dictionary<Guid, ServiceCategory> { [category.Id] = category });
    }

    /// <summary>Offre déjà demandée : désactivée plutôt que supprimée (historique des demandes et des avis).</summary>
    public async Task<CountDto> Handle(DeleteOfferingCommand request, CancellationToken ct)
    {
        var profile = await MineAsync(ct);
        var offering = await db.Offerings.FirstOrDefaultAsync(o => o.Id == request.Id && o.ProviderId == profile.Id, ct) ?? throw new NotFoundException("Offre");
        if (await db.Requests.AnyAsync(r => r.OfferingId == offering.Id, ct))
        {
            throw new DomainException("Cette offre a déjà reçu des demandes : désactivez-la plutôt que de la supprimer.");
        }

        db.Offerings.Remove(offering);
        await db.SaveChangesAsync(ct);
        if (offering.Images.Count > 0)
        {
            await bus.PublishAsync(new ImagesReleased(offering.Images), ct);
        }

        return new CountDto(1);
    }

    public async Task<UploadedImageDto> Handle(UploadServiceImageCommand request, CancellationToken ct) =>
        new(await media.SaveCleanImageAsync(request.Content, request.Length, ServiceRules.MaxImageBytes, ct));

    /// <summary>Justificatif privé : visible seulement par l'équipe de modération.</summary>
    public async Task<DocumentDto> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        var profile = await MineAsync(ct);
        if (await db.Documents.CountAsync(d => d.ProviderId == profile.Id && d.Status == DocumentStatus.Pending, ct) >= 5)
        {
            throw new DomainException("5 justificatifs au plus en attente de vérification.");
        }

        var stored = await media.SaveAttachmentAsync(request.Content, request.Length, ct);
        if (stored.Kind == "Audio")
        {
            await bus.PublishAsync(new AttachmentsReleased([stored.Name]), ct);
            throw new DomainException("Justificatif : photo ou PDF uniquement.");
        }

        var document = ProviderDocument.Create(profile.Id, request.Kind, stored.Name, request.FileName ?? string.Empty, stored.ContentType, stored.Size,
            clock.GetUtcNow());
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);
        return document.ToDto();
    }

    public async Task<CountDto> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        var profile = await MineAsync(ct);
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == request.Id && d.ProviderId == profile.Id, ct)
                       ?? throw new NotFoundException("Justificatif");
        if (document.Status == DocumentStatus.Approved)
        {
            throw new DomainException("Un justificatif validé ne peut pas être supprimé.");
        }

        db.Documents.Remove(document);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new AttachmentsReleased([document.FileName]), ct);
        return new CountDto(1);
    }

    private async Task<ProviderProfile> MineAsync(CancellationToken ct) =>
        await db.Providers.FirstOrDefaultAsync(p => p.UserId == me.Id, ct)
        ?? throw new DomainException("Créez d'abord votre profil prestataire.");

    private async Task<MyProviderDto> DescribeAsync(ProviderProfile profile, CancellationToken ct)
    {
        var offerings = await db.Offerings.AsNoTracking().Where(o => o.ProviderId == profile.Id).OrderBy(o => o.CreatedAt).ToListAsync(ct);
        var ids = offerings.Select(o => o.CategoryId).Distinct().ToList();
        var categories = await db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var documents = await db.Documents.AsNoTracking().Where(d => d.ProviderId == profile.Id).OrderByDescending(d => d.UploadedAt).ToListAsync(ct);
        return profile.ToMine(offerings.Select(o => o.ToDto(categories)).ToList(), documents.Select(d => d.ToDto()).ToList());
    }

    private static OfferingDetails Details(ServiceOffering o) =>
        new(o.CategoryId, o.Title, o.Description, o.PriceType, o.PriceAmount, o.DurationMinutes, o.Images, o.IsActive);
}
