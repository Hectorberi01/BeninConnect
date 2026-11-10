using System.Security.Claims;
using BeninConnect.Modules.Messaging.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Infrastructure.Realtime;

/// <summary>
/// Canal temps réel de la messagerie (/hubs/chat).
/// Serveur → client : messageCreated(message), messageUpdated(message), conversationRead(conversationId, readerId),
/// typing(conversationId, userId). Client → serveur : Typing(conversationId), au plus toutes les 3 secondes.
/// </summary>
[Authorize]
public sealed class ChatHub(IMessagingDb db, PresenceTracker presence) : Hub
{
    public override Task OnConnectedAsync()
    {
        if (UserGuid is { } id)
        {
            presence.Connected(id);
        }

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (UserGuid is { } id)
        {
            presence.Disconnected(id);
        }

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>« En train d'écrire » : relayé aux autres participants de la discussion.</summary>
    public async Task Typing(Guid conversationId)
    {
        if (UserGuid is not { } me)
        {
            return;
        }

        var others = await db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId && c.Participants.Any(p => p.UserId == me))
            .SelectMany(c => c.Participants).Where(p => p.UserId != me).Select(p => p.UserId.ToString()).ToListAsync();
        if (others.Count > 0)
        {
            await Clients.Users(others).SendAsync("typing", conversationId, me);
        }
    }

    private Guid? UserGuid => Guid.TryParse(Context.UserIdentifier, out var id) ? id : null;
}

/// <summary>Présence en mémoire (une seule instance de l'API) : nombre de connexions et dernière déconnexion par membre.</summary>
public sealed class PresenceTracker(TimeProvider clock) : IPresence
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> _connections = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTimeOffset> _lastSeen = new();

    public void Connected(Guid userId) => _connections.AddOrUpdate(userId, 1, (_, n) => n + 1);

    public void Disconnected(Guid userId)
    {
        if (_connections.AddOrUpdate(userId, 0, (_, n) => Math.Max(0, n - 1)) == 0)
        {
            _connections.TryRemove(new KeyValuePair<Guid, int>(userId, 0));
        }

        _lastSeen[userId] = clock.GetUtcNow();
    }

    public bool IsOnline(Guid userId) => _connections.TryGetValue(userId, out var n) && n > 0;

    public DateTimeOffset? LastSeen(Guid userId) => _lastSeen.TryGetValue(userId, out var at) ? at : null;
}

/// <summary>Identifiant SignalR d'une connexion : l'id du membre porté par le jeton (sub).</summary>
public sealed class MemberIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? connection.User.FindFirstValue("sub");
}

public sealed class SignalRChatNotifier(IHubContext<ChatHub> hub) : IChatNotifier
{
    public Task MessageCreatedAsync(IReadOnlyCollection<Guid> userIds, MessageDto message, CancellationToken ct) =>
        To(userIds).SendAsync("messageCreated", message, ct);

    public Task MessageUpdatedAsync(IReadOnlyCollection<Guid> userIds, MessageDto message, CancellationToken ct) =>
        To(userIds).SendAsync("messageUpdated", message, ct);

    public Task ConversationReadAsync(IReadOnlyCollection<Guid> userIds, Guid conversationId, Guid readerId, CancellationToken ct) =>
        To(userIds).SendAsync("conversationRead", conversationId, readerId, ct);

    private IClientProxy To(IReadOnlyCollection<Guid> userIds) => hub.Clients.Users(userIds.Select(id => id.ToString()).ToList());
}
