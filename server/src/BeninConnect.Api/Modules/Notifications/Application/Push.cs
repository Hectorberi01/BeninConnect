using BeninConnect.Modules.Notifications.Domain;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Notifications.Application;

/// <summary>Notification à pousser vers les téléphones du membre (traitée en arrière-plan).</summary>
public sealed record PushRequest(Guid NotificationId, Guid UserId, NotificationCategory Category, string Title, string Body, NotificationTarget Target, Guid? TargetId);

/// <summary>Message prêt à partir vers un téléphone.</summary>
public sealed record PushMessage(string Token, string Title, string Body, int Badge, IReadOnlyDictionary<string, string?> Data);

public interface IPushQueue
{
    void Enqueue(PushRequest request);
}

public interface IPushSender
{
    /// <summary>Envoie les messages ; retourne les jetons qui ne sont plus valides (application désinstallée...).</summary>
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct);
}

public sealed record RegisterPushDeviceCommand(string Token, string Platform, IReadOnlyList<NotificationCategory>? MutedCategories) : ICommand<bool>, IValidatable
{
    public IEnumerable<(string, string)> Validate()
    {
        if (!PushDevice.IsValidToken(Token))
        {
            yield return (nameof(Token), "Jeton de notification invalide.");
        }

        if (Platform is not ("ios" or "android"))
        {
            yield return (nameof(Platform), "Plateforme attendue : ios ou android.");
        }
    }
}

public sealed record UnregisterPushDeviceCommand(string Token) : ICommand<bool>;

public sealed class PushDeviceHandlers(INotificationsDb db, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<RegisterPushDeviceCommand, bool>,
    IRequestHandler<UnregisterPushDeviceCommand, bool>
{
    public async Task<bool> Handle(RegisterPushDeviceCommand request, CancellationToken ct)
    {
        var muted = request.MutedCategories ?? [];
        var device = await db.PushDevices.FirstOrDefaultAsync(d => d.Token == request.Token, ct);
        if (device is null)
        {
            db.PushDevices.Add(PushDevice.Register(Guid.NewGuid(), me.Id, request.Token, request.Platform, muted, clock.GetUtcNow()));
        }
        else
        {
            device.Refresh(me.Id, request.Platform, muted, clock.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Déconnexion : le téléphone ne reçoit plus les notifications de ce compte.</summary>
    public async Task<bool> Handle(UnregisterPushDeviceCommand request, CancellationToken ct)
    {
        var device = await db.PushDevices.FirstOrDefaultAsync(d => d.Token == request.Token && d.UserId == me.Id, ct);
        if (device is not null)
        {
            db.PushDevices.Remove(device);
            await db.SaveChangesAsync(ct);
        }

        return true;
    }
}

/// <summary>Construit les messages d'une notification : téléphones du membre qui acceptent la catégorie, pastille = non lues.</summary>
public sealed class PushComposer(INotificationsDb db, IPushSender sender)
{
    public async Task SendAsync(PushRequest request, CancellationToken ct)
    {
        var devices = (await db.PushDevices.Where(d => d.UserId == request.UserId).ToListAsync(ct))
            .Where(d => d.Accepts(request.Category)).ToList();
        if (devices.Count == 0)
        {
            return;
        }

        var unread = await db.Notifications.CountAsync(n => n.UserId == request.UserId && !n.IsRead, ct);
        var data = new Dictionary<string, string?>
        {
            ["notificationId"] = request.NotificationId.ToString(),
            ["category"] = request.Category.ToString(),
            ["target"] = request.Target.ToString(),
            ["targetId"] = request.TargetId?.ToString()
        };

        var invalid = await sender.SendAsync(devices.Select(d => new PushMessage(d.Token, request.Title, request.Body, unread, data)).ToList(), ct);
        if (invalid.Count > 0)
        {
            db.PushDevices.RemoveRange(devices.Where(d => invalid.Contains(d.Token)));
            await db.SaveChangesAsync(ct);
        }
    }
}
