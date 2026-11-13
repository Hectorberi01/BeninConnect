using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Application;

/// <summary>Rappel d'avis au client, une fois, quelques heures après une prestation terminée.</summary>
public sealed class ReviewReminders(IServicesDb db, IEventBus bus, ServicesOptions options, TimeProvider clock)
{
    public async Task<int> SendAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = now.AddHours(-options.ReviewReminderHours);
        var reviewed = db.Reviews.Select(r => r.RequestId);
        var requests = await db.Requests
            .Where(r => r.Status == RequestStatus.Completed && r.CompletedAt <= due && r.ReviewReminderSentAt == null && !reviewed.Contains(r.Id))
            .Take(200).ToListAsync(ct);
        foreach (var request in requests)
        {
            request.MarkReminded(now);
        }

        await db.SaveChangesAsync(ct);
        foreach (var request in requests)
        {
            await ServicesData.NotifyAsync(bus, request.ClientId, "Comment s'est passée votre prestation ?",
                "Laissez un avis vérifié pour aider les autres membres.", request.ConversationId, ct);
        }

        return requests.Count;
    }
}

/// <summary>Compte supprimé : profil prestataire, offres, justificatifs, favoris et signalements effacés ; ses avis retirés.</summary>
public sealed class DeleteMemberServices(IServicesDb db, IEventBus bus) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        var images = new List<string>();
        var files = new List<string>();
        if (await db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == e.UserId, ct) is { } profile)
        {
            (images, files) = await ServicesData.DeleteProviderAsync(db, profile, ct);
        }

        var rated = await db.Reviews.Where(r => r.AuthorId == e.UserId).Select(r => r.ProviderId).Distinct().ToListAsync(ct);
        await db.Reviews.Where(r => r.AuthorId == e.UserId).ExecuteDeleteAsync(ct);
        await db.Requests.Where(r => r.ClientId == e.UserId).ExecuteDeleteAsync(ct);
        await db.Favorites.Where(f => f.UserId == e.UserId).ExecuteDeleteAsync(ct);
        await db.Reports.Where(r => r.AuthorId == e.UserId).ExecuteDeleteAsync(ct);
        foreach (var providerId in rated)
        {
            await ServicesData.RefreshRatingAsync(db, providerId, ct);
        }

        await db.SaveChangesAsync(ct);
        if (images.Count > 0)
        {
            await bus.PublishAsync(new ImagesReleased(images), ct);
        }

        if (files.Count > 0)
        {
            await bus.PublishAsync(new AttachmentsReleased(files), ct);
        }
    }
}
