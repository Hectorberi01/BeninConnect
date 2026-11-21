using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Infrastructure;

/// <summary>Profil privé : une discussion en commun fait de deux membres des contacts.</summary>
public sealed class MessagingContacts(IMessagingDb db) : IContactDirectory
{
    public async Task<IReadOnlySet<Guid>> ContactsAmongAsync(Guid viewerId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.ToList();
        var shared = await db.Conversations.AsNoTracking()
            .Where(c => c.Participants.Any(p => p.UserId == viewerId))
            .SelectMany(c => c.Participants).Where(p => ids.Contains(p.UserId)).Select(p => p.UserId).Distinct().ToListAsync(ct);
        return shared.ToHashSet();
    }
}
