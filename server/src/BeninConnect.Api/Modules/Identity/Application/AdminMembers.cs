using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Domain;
using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Application;

public sealed record AdminMemberDto(
    Guid Id, string FirstName, string LastName, string Email, string Country, string City, string AvatarColor, string? AvatarImage,
    DateTimeOffset JoinedAt, bool IsEmailVerified, bool IsSuspended, string? SuspensionReason, int PostCount, int CommentCount, int ReportCount);

public sealed record AdminMemberDetailDto(
    AdminMemberDto Member, string Profession, string Bio, IReadOnlyList<string> Communities, IReadOnlyList<AdminPostDto> RecentPosts);

/// <summary>Status : all, active, suspended, unverified.</summary>
public sealed record GetAdminMembersQuery(string? Search, string? Status, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminMemberDto>>;

public sealed record GetAdminMemberQuery(Guid Id) : IQuery<AdminMemberDetailDto>;

public sealed record SuspendMemberCommand(Guid Id, string Reason) : ICommand<AdminMemberDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Length(nameof(Reason), Reason, "Le motif", 5, 300);
}

public sealed record ReactivateMemberCommand(Guid Id) : ICommand<AdminMemberDto>;

/// <summary>Console : membres ; compteurs fournis par Feed et Moderation via leurs contrats.</summary>
public sealed class AdminMemberHandlers(IIdentityDb db, IFeedModule feed, IModerationModule moderation, ICommunitiesModule communities,
    IAdminSession admin, TimeProvider clock) :
    IRequestHandler<GetAdminMembersQuery, PagedResult<AdminMemberDto>>,
    IRequestHandler<GetAdminMemberQuery, AdminMemberDetailDto>,
    IRequestHandler<SuspendMemberCommand, AdminMemberDto>,
    IRequestHandler<ReactivateMemberCommand, AdminMemberDto>
{
    public async Task<PagedResult<AdminMemberDto>> Handle(GetAdminMembersQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var users = db.Users.AsNoTracking();
        if (Text.Like(request.Search) is { } q)
        {
            users = users.Where(u => (u.FirstName + " " + u.LastName).ToLower().Contains(q) || u.Email.Contains(q) || u.City.ToLower().Contains(q));
        }

        users = request.Status switch
        {
            "active" => users.Where(u => !u.IsSuspended && u.IsEmailVerified),
            "suspended" => users.Where(u => u.IsSuspended),
            "unverified" => users.Where(u => !u.IsEmailVerified),
            _ => users
        };

        var total = await users.CountAsync(ct);
        var page = await users.OrderByDescending(u => u.JoinedAt).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminMemberDto>(await DescribeAsync(page, ct), total, skip);
    }

    public async Task<AdminMemberDetailDto> Handle(GetAdminMemberQuery request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.Id, ct) ?? throw new NotFoundException("Membre");
        var member = (await DescribeAsync([user], ct))[0];
        var names = await communities.GetCommunityNamesOfMemberAsync(user.Id, ct);
        var posts = await feed.GetRecentPostsForAdminAsync(user.Id, 10, ct);
        return new AdminMemberDetailDto(member, user.Profession, user.Bio, names, posts);
    }

    public async Task<AdminMemberDto> Handle(SuspendMemberCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, ct) ?? throw new NotFoundException("Membre");
        user.Suspend(request.Reason, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Membre suspendu", "User", user.Id, $"{user.DisplayName} : {request.Reason.Trim()}", ct);
        return (await DescribeAsync([user], ct))[0];
    }

    public async Task<AdminMemberDto> Handle(ReactivateMemberCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, ct) ?? throw new NotFoundException("Membre");
        user.Reactivate();
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Membre réactivé", "User", user.Id, user.DisplayName, ct);
        return (await DescribeAsync([user], ct))[0];
    }

    private async Task<List<AdminMemberDto>> DescribeAsync(IReadOnlyList<User> users, CancellationToken ct)
    {
        var ids = users.Select(u => u.Id).ToList();
        var posts = await feed.CountPostsByAuthorAsync(ids, ct);
        var comments = await feed.CountCommentsByAuthorAsync(ids, ct);
        var reports = await moderation.CountReportsAsync(ReportTarget.User, ids, ct);
        return users.Select(u => new AdminMemberDto(
            u.Id, u.FirstName, u.LastName, u.Email, u.Country, u.City, u.AvatarColor, u.AvatarImage, u.JoinedAt, u.IsEmailVerified,
            u.IsSuspended, u.SuspensionReason, posts.GetValueOrDefault(u.Id), comments.GetValueOrDefault(u.Id), reports.GetValueOrDefault(u.Id))).ToList();
    }
}
