using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Communities.Domain;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Communities.Application;

public interface ICommunitiesDb
{
    DbSet<Community> Communities { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record CommunityDto(
    Guid Id, string Name, string Description, string Category, string Country, string City,
    CommunityVisibility Visibility, string? CoverImage, Guid OwnerId, IReadOnlyList<Guid> ModeratorIds,
    DateTimeOffset CreatedAt, IReadOnlyList<Guid> MemberIds);

public sealed record CommunityMemberDto(UserDto User, CommunityRole Role);

public sealed record MembershipDto(MembershipStatus Status);

public static class CommunitiesCatalog
{
    public static readonly string[] Categories =
        ["Villes & régions", "Études", "Entrepreneuriat", "Culture", "Emploi", "Entraide", "Tech", "Gastronomie", "Famille"];

    /// <summary>Nécessite l'inclusion de Members.</summary>
    public static CommunityDto ToDto(this Community c) => new(
        c.Id, c.Name, c.Description, c.Category, c.Country, c.City, c.Visibility, c.CoverImage, c.OwnerId,
        c.Members.Where(m => m.Role == CommunityRole.Moderator).Select(m => m.UserId).ToList(),
        c.CreatedAt, c.Members.Select(m => m.UserId).ToList());
}

public sealed record GetCommunitiesQuery(string? Search) : IQuery<IReadOnlyList<CommunityDto>>;

public sealed record GetCommunityQuery(Guid Id) : IQuery<CommunityDto>;

public sealed record GetCommunitiesByIdsQuery(IReadOnlyList<Guid> Ids) : IQuery<IReadOnlyList<CommunityDto>>;

public sealed record GetMemberCommunitiesQuery(Guid UserId) : IQuery<IReadOnlyList<CommunityDto>>;

public sealed record GetCommunityCategoriesQuery : IQuery<IReadOnlyList<string>>;

public sealed record GetCommunityMembersQuery(Guid CommunityId) : IQuery<IReadOnlyList<CommunityMemberDto>>;

public sealed record GetMyJoinRequestsQuery : IQuery<IReadOnlyList<Guid>>;

public sealed record CreateCommunityCommand(
    string Name, string Description, string Category, string Country, string City, CommunityVisibility Visibility, string? CoverImage)
    : ICommand<CommunityDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Length(nameof(Name), Name, "Le nom", 3, 60)
            .Concat(Rules.Length(nameof(Description), Description, "La description", 10, 500))
            .Concat(CommunitiesCatalog.Categories.Contains(Category) ? [] : new[] { (nameof(Category), "Choisissez une catégorie.") })
            .Concat(Rules.Required(nameof(Country), Country, "Le pays"))
            .Concat(Rules.Required(nameof(City), City, "La ville"));
}

public sealed record JoinCommunityCommand(Guid CommunityId) : ICommand<MembershipDto>;

public sealed record LeaveCommunityCommand(Guid CommunityId) : ICommand<MembershipDto>;

public sealed record CancelJoinRequestCommand(Guid CommunityId) : ICommand<MembershipDto>;

public sealed class CommunityHandlers(ICommunitiesDb db, IIdentityModule identity, IEventBus bus, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<GetCommunitiesQuery, IReadOnlyList<CommunityDto>>,
    IRequestHandler<GetCommunityQuery, CommunityDto>,
    IRequestHandler<GetCommunitiesByIdsQuery, IReadOnlyList<CommunityDto>>,
    IRequestHandler<GetMemberCommunitiesQuery, IReadOnlyList<CommunityDto>>,
    IRequestHandler<GetCommunityCategoriesQuery, IReadOnlyList<string>>,
    IRequestHandler<GetCommunityMembersQuery, IReadOnlyList<CommunityMemberDto>>,
    IRequestHandler<GetMyJoinRequestsQuery, IReadOnlyList<Guid>>,
    IRequestHandler<CreateCommunityCommand, CommunityDto>,
    IRequestHandler<JoinCommunityCommand, MembershipDto>,
    IRequestHandler<LeaveCommunityCommand, MembershipDto>,
    IRequestHandler<CancelJoinRequestCommand, MembershipDto>
{
    private IQueryable<Community> Read => db.Communities.AsNoTracking().Include(c => c.Members);

    public async Task<IReadOnlyList<CommunityDto>> Handle(GetCommunitiesQuery request, CancellationToken ct)
    {
        var query = Read;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(q) || c.Description.ToLower().Contains(q)
                                     || c.City.ToLower().Contains(q) || c.Country.ToLower().Contains(q) || c.Category.ToLower().Contains(q));
        }

        var list = await query.ToListAsync(ct);
        return list.OrderByDescending(c => c.Members.Count).Select(c => c.ToDto()).ToList();
    }

    public async Task<CommunityDto> Handle(GetCommunityQuery request, CancellationToken ct) =>
        (await Read.FirstOrDefaultAsync(c => c.Id == request.Id, ct) ?? throw new NotFoundException("Communauté")).ToDto();

    public async Task<IReadOnlyList<CommunityDto>> Handle(GetCommunitiesByIdsQuery request, CancellationToken ct)
    {
        var ids = request.Ids.Distinct().Take(500).ToList();
        return (await Read.Where(c => ids.Contains(c.Id)).ToListAsync(ct)).Select(c => c.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<CommunityDto>> Handle(GetMemberCommunitiesQuery request, CancellationToken ct) =>
        (await Read.Where(c => c.Members.Any(m => m.UserId == request.UserId)).OrderBy(c => c.Name).ToListAsync(ct))
        .Select(c => c.ToDto()).ToList();

    public Task<IReadOnlyList<string>> Handle(GetCommunityCategoriesQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(CommunitiesCatalog.Categories);

    public async Task<IReadOnlyList<CommunityMemberDto>> Handle(GetCommunityMembersQuery request, CancellationToken ct)
    {
        var community = await Read.FirstOrDefaultAsync(c => c.Id == request.CommunityId, ct) ?? throw new NotFoundException("Communauté");
        if (community.IsPrivate && !community.IsMember(me.Id))
        {
            throw new ForbiddenException("Cette communauté est privée.");
        }

        var roles = community.Members.ToDictionary(m => m.UserId, m => m.UserId == community.OwnerId ? CommunityRole.Owner : m.Role);
        var users = await identity.GetUsersAsync(roles.Keys, ct);
        return users.Values
            .Select(u => new CommunityMemberDto(u, roles[u.Id]))
            .OrderByDescending(m => m.Role).ThenBy(m => m.User.FirstName)
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> Handle(GetMyJoinRequestsQuery request, CancellationToken ct) =>
        await db.Communities.AsNoTracking()
            .SelectMany(c => c.JoinRequests).Where(r => r.UserId == me.Id).Select(r => r.CommunityId)
            .ToListAsync(ct);

    public async Task<CommunityDto> Handle(CreateCommunityCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await db.Communities.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct))
        {
            throw new ConflictException("Une communauté porte déjà ce nom.");
        }

        var community = Community.Create(
            Guid.NewGuid(), name, request.Description, request.Category, request.Country, request.City,
            request.Visibility, request.CoverImage ?? "Covers/community_01.jpg", me.Id, clock.GetUtcNow());

        db.Communities.Add(community);
        await db.SaveChangesAsync(ct);
        return community.ToDto();
    }

    public async Task<MembershipDto> Handle(JoinCommunityCommand request, CancellationToken ct)
    {
        var community = await LoadAsync(request.CommunityId, ct);
        var before = community.StatusFor(me.Id);
        var status = community.Join(me.Id, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        if (status == MembershipStatus.Member && before == MembershipStatus.None)
        {
            await bus.PublishAsync(new CommunityJoined(community.Id, community.Name, me.Id), ct);
        }

        return new MembershipDto(status);
    }

    public async Task<MembershipDto> Handle(LeaveCommunityCommand request, CancellationToken ct)
    {
        var community = await LoadAsync(request.CommunityId, ct);
        community.Leave(me.Id);
        await db.SaveChangesAsync(ct);
        return new MembershipDto(MembershipStatus.None);
    }

    public async Task<MembershipDto> Handle(CancelJoinRequestCommand request, CancellationToken ct)
    {
        var community = await LoadAsync(request.CommunityId, ct);
        community.CancelJoinRequest(me.Id);
        await db.SaveChangesAsync(ct);
        return new MembershipDto(community.StatusFor(me.Id));
    }

    private async Task<Community> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Communities.Include(c => c.Members).Include(c => c.JoinRequests).FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Communauté");
}
