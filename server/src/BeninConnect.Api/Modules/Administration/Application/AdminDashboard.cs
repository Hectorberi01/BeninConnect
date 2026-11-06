using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Events.Contracts;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Administration.Application;

public sealed record DailyPoint(DateOnly Day, int Count);

public sealed record AdminStatsDto(
    int Members, int NewMembers7Days, int NewMembers30Days, int VerifiedMembers, int SuspendedMembers,
    int Posts, int HiddenPosts, int Comments, int Communities, int PrivateCommunities,
    int UpcomingEvents, int EventParticipations, int Messages, int OpenReports,
    IReadOnlyList<DailyPoint> Signups, IReadOnlyList<DailyPoint> PostsPerDay,
    IReadOnlyList<LabelCount> TopCommunities, IReadOnlyList<LabelCount> MembersByCountry);

public sealed record GetAdminStatsQuery : IQuery<AdminStatsDto>;

/// <summary>Tableau de bord : agrège les statistiques publiées par chaque module.</summary>
public sealed class AdminDashboardHandler(
    IIdentityModule identity, IFeedModule feed, ICommunitiesModule communities, IEventsModule events,
    IMessagingModule messaging, IModerationModule moderation, TimeProvider clock) : IRequestHandler<GetAdminStatsQuery, AdminStatsDto>
{
    private const int Days = 30;

    public async Task<AdminStatsDto> Handle(GetAdminStatsQuery request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var since = now.AddDays(-Days);
        var members = await identity.GetStatsAsync(since, ct);
        var posts = await feed.GetStatsAsync(since, ct);
        var groups = await communities.GetStatsAsync(ct);
        var agenda = await events.GetStatsAsync(now, ct);

        return new AdminStatsDto(
            Members: members.Total,
            NewMembers7Days: members.JoinedSince.Count(d => d >= now.AddDays(-7)),
            NewMembers30Days: members.JoinedSince.Count,
            VerifiedMembers: members.Verified,
            SuspendedMembers: members.Suspended,
            Posts: posts.Posts,
            HiddenPosts: posts.HiddenPosts,
            Comments: posts.Comments,
            Communities: groups.Total,
            PrivateCommunities: groups.Private,
            UpcomingEvents: agenda.Upcoming,
            EventParticipations: agenda.Participations,
            Messages: await messaging.CountMessagesAsync(ct),
            OpenReports: await moderation.CountOpenAsync(ct),
            Signups: Series(members.JoinedSince, now),
            PostsPerDay: Series(posts.PostedSince, now),
            TopCommunities: groups.TopByMembers,
            MembersByCountry: members.TopCountries);
    }

    private static List<DailyPoint> Series(IReadOnlyList<DateTimeOffset> dates, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var counts = dates.GroupBy(d => DateOnly.FromDateTime(d.UtcDateTime)).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, Days)
            .Select(i => today.AddDays(i - Days + 1))
            .Select(day => new DailyPoint(day, counts.GetValueOrDefault(day)))
            .ToList();
    }
}
