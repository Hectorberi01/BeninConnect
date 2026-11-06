using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Communities.Domain;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Communities.Application;

public sealed record AdminCommunityDto(
    Guid Id, string Name, string Category, string City, string Country, CommunityVisibility Visibility, string OwnerName,
    int MemberCount, int PostCount, int PendingRequests, DateTimeOffset CreatedAt);

public sealed record GetAdminCommunitiesQuery(string? Search) : IQuery<IReadOnlyList<AdminCommunityDto>>;

public sealed record DeleteCommunityCommand(Guid Id) : ICommand<bool>;

public sealed class AdminCommunityHandlers(ICommunitiesDb db, IIdentityModule identity, IFeedModule feed, IAdminSession admin, IEventBus bus) :
    IRequestHandler<GetAdminCommunitiesQuery, IReadOnlyList<AdminCommunityDto>>,
    IRequestHandler<DeleteCommunityCommand, bool>
{
    public async Task<IReadOnlyList<AdminCommunityDto>> Handle(GetAdminCommunitiesQuery request, CancellationToken ct)
    {
        var communities = db.Communities.AsNoTracking();
        if (Text.Like(request.Search) is { } q)
        {
            communities = communities.Where(c => c.Name.ToLower().Contains(q) || c.City.ToLower().Contains(q));
        }

        var list = await communities.OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Category, c.City, c.Country, c.Visibility, c.OwnerId, Members = c.Members.Count, Requests = c.JoinRequests.Count, c.CreatedAt })
            .ToListAsync(ct);
        var owners = await identity.GetNamesAsync(list.Select(c => c.OwnerId), ct);
        var posts = await feed.CountPostsByCommunityAsync(ct);
        return list.Select(c => new AdminCommunityDto(c.Id, c.Name, c.Category, c.City, c.Country, c.Visibility,
            owners.GetValueOrDefault(c.OwnerId) ?? "—", c.Members, posts.GetValueOrDefault(c.Id), c.Requests, c.CreatedAt)).ToList();
    }

    public async Task<bool> Handle(DeleteCommunityCommand request, CancellationToken ct)
    {
        var community = await db.Communities.FirstOrDefaultAsync(c => c.Id == request.Id, ct) ?? throw new NotFoundException("Communauté");
        db.Communities.Remove(community);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new CommunityDeleted(community.Id), ct);
        await admin.LogAsync("Communauté supprimée", "Community", community.Id, community.Name, ct);
        return true;
    }
}
