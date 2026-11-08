using System.Security.Claims;
using BeninConnect.Modules.Messaging.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BeninConnect.Modules.Messaging.Infrastructure.Realtime;

/// <summary>
/// Canal temps réel de la messagerie (/hubs/chat). Le serveur pousse ; le client n'appelle aucune méthode.
/// Événements : messageCreated(message), messageUpdated(message), conversationRead(conversationId, readerId).
/// </summary>
[Authorize]
public sealed class ChatHub : Hub;

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
