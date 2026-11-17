using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Notifications.Contracts;
using BeninConnect.Modules.Notifications.Domain;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Notifications.Application;

/// <summary>Transforme les événements des autres modules en notifications (textes identiques à l'application mobile).</summary>
public sealed class NotificationSubscribers(INotificationsDb db, IIdentityModule identity, IPushQueue push, TimeProvider clock) :
    IIntegrationEventHandler<MemberVerified>,
    IIntegrationEventHandler<CommunityJoined>,
    IIntegrationEventHandler<PostLiked>,
    IIntegrationEventHandler<PostCommented>,
    IIntegrationEventHandler<CommentReacted>,
    IIntegrationEventHandler<CommentReplied>,
    IIntegrationEventHandler<MembersMentioned>,
    IIntegrationEventHandler<MessageSent>,
    IIntegrationEventHandler<OfficialPostPublished>,
    IIntegrationEventHandler<NotificationRequested>
{
    public Task Handle(MemberVerified e, CancellationToken ct) =>
        AddAsync(ct, (e.UserId, NotificationCategory.System, "Bienvenue sur BéninConnect",
            "Complétez votre profil et rejoignez vos premières communautés.", NotificationTarget.Profile, e.UserId));

    public Task Handle(CommunityJoined e, CancellationToken ct) =>
        AddAsync(ct, (e.UserId, NotificationCategory.Communities, $"Bienvenue dans {e.CommunityName}",
            "Présentez-vous aux membres et découvrez les prochains événements.", NotificationTarget.Community, e.CommunityId));

    public async Task Handle(PostLiked e, CancellationToken ct) =>
        await AddAsync(ct, (e.PostAuthorId, NotificationCategory.Social, $"{await NameAsync(e.ActorId, ct)} a aimé votre publication",
            e.Excerpt, NotificationTarget.Post, e.PostId));

    public async Task Handle(PostCommented e, CancellationToken ct) =>
        await AddAsync(ct, (e.PostAuthorId, NotificationCategory.Social, $"{await NameAsync(e.ActorId, ct)} a commenté votre publication",
            $"« {e.CommentExcerpt} »", NotificationTarget.Post, e.PostId));

    public async Task Handle(CommentReacted e, CancellationToken ct) =>
        await AddAsync(ct, (e.CommentAuthorId, NotificationCategory.Social, $"{await NameAsync(e.ActorId, ct)} a réagi {e.Emoji} à votre commentaire",
            $"« {e.CommentExcerpt} »", NotificationTarget.Post, e.PostId));

    public async Task Handle(CommentReplied e, CancellationToken ct) =>
        await AddAsync(ct, (e.ParentAuthorId, NotificationCategory.Social, $"{await NameAsync(e.ActorId, ct)} a répondu à votre commentaire",
            $"« {e.Excerpt} »", NotificationTarget.Post, e.PostId));

    public async Task Handle(MembersMentioned e, CancellationToken ct)
    {
        var actor = await NameAsync(e.ActorId, ct);
        var title = e.InComment ? $"{actor} vous a mentionné dans un commentaire" : $"{actor} vous a mentionné dans une publication";
        await AddAsync(ct, e.UserIds.Where(id => id != e.ActorId)
            .Select(id => (id, NotificationCategory.Social, title, $"« {e.Excerpt} »", NotificationTarget.Post, (Guid?)e.PostId)).ToArray());
    }

    public async Task Handle(MessageSent e, CancellationToken ct)
    {
        var sender = await NameAsync(e.SenderId, ct);
        await AddAsync(ct, e.RecipientIds
            .Select(r => (r, NotificationCategory.Messages, $"Nouveau message de {sender}", e.Excerpt, NotificationTarget.Conversation, (Guid?)e.ConversationId))
            .ToArray());
    }

    public async Task Handle(OfficialPostPublished e, CancellationToken ct)
    {
        // Tous les membres, par vagues (tâche notifications.broadcasts) ; un seul push par jour
        var now = clock.GetUtcNow();
        var today = Broadcasts.StartOfDay(now);
        var pushedToday = await db.Broadcasts.AnyAsync(b => b.SendPush && b.CreatedAt >= today, ct);
        db.Broadcasts.Add(Broadcast.Create(Guid.NewGuid(), NotificationCategory.System, $"{await NameAsync(e.AuthorId, ct)} a publié", e.Excerpt,
            NotificationTarget.Post, e.PostId, !pushedToday, now));
        await db.SaveChangesAsync(ct);
    }

    public Task Handle(NotificationRequested e, CancellationToken ct) =>
        AddAsync(ct, (e.UserId, Enum.Parse<NotificationCategory>(e.Category), e.Title, e.Body, Enum.Parse<NotificationTarget>(e.Target), e.TargetId));

    private async Task<string> NameAsync(Guid userId, CancellationToken ct) =>
        (await identity.GetNamesAsync([userId], ct)).GetValueOrDefault(userId) ?? "Un membre";

    private async Task AddAsync(CancellationToken ct, params (Guid UserId, NotificationCategory Category, string Title, string Body, NotificationTarget Target, Guid? TargetId)[] items)
    {
        var created = items
            .Select(n => Notification.Create(Guid.NewGuid(), n.UserId, n.Category, n.Title, n.Body, clock.GetUtcNow(), n.Target, n.TargetId))
            .ToList();
        db.Notifications.AddRange(created);
        await db.SaveChangesAsync(ct);

        // Push après enregistrement, en arrière-plan : n'allonge pas la requête du membre qui a agi.
        foreach (var n in created)
        {
            push.Enqueue(new PushRequest(n.Id, n.UserId, n.Category, n.Title, n.Body, n.Target, n.TargetId));
        }
    }
}

/// <summary>Envoi par vagues des notifications à tous les membres.</summary>
public sealed class Broadcasts(INotificationsDb db, IIdentityModule identity, IPushQueue push, TimeProvider clock)
{
    private static readonly TimeZoneInfo Paris = FindParis();

    /// <summary>Minuit, heure de Paris, en UTC.</summary>
    public static DateTimeOffset StartOfDay(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Paris);
        return new DateTimeOffset(local.Date, local.Offset).ToUniversalTime();
    }

    /// <summary>Une vague : au plus waveSize membres, toutes diffusions confondues.</summary>
    public async Task<int> SendWaveAsync(int waveSize, CancellationToken ct)
    {
        var sent = 0;
        var pending = await db.Broadcasts.Where(b => b.CompletedAt == null).OrderBy(b => b.CreatedAt).ToListAsync(ct);
        foreach (var broadcast in pending)
        {
            while (sent < waveSize)
            {
                var take = Math.Min(500, waveSize - sent);
                var ids = await identity.GetActiveMemberIdsAsync(broadcast.LastUserId, take, ct);
                var now = clock.GetUtcNow();
                var created = ids.Select(id => Notification.Create(Guid.NewGuid(), id, broadcast.Category, broadcast.Title, broadcast.Body, now,
                    broadcast.Target, broadcast.TargetId)).ToList();
                db.Notifications.AddRange(created);
                broadcast.Advance(ids, ids.Count < take, now);
                await db.SaveChangesAsync(ct);

                if (broadcast.SendPush)
                {
                    foreach (var n in created)
                    {
                        push.Enqueue(new PushRequest(n.Id, n.UserId, n.Category, n.Title, n.Body, n.Target, n.TargetId));
                    }
                }

                sent += ids.Count;
                if (broadcast.CompletedAt is not null)
                {
                    break;
                }
            }

            if (sent >= waveSize)
            {
                break;
            }
        }

        return sent;
    }

    private static TimeZoneInfo FindParis()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
