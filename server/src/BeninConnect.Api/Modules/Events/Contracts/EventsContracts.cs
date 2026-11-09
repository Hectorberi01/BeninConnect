namespace BeninConnect.Modules.Events.Contracts;

public sealed record EventStats(int Upcoming, int Participations);

/// <summary>Événement organisé par un membre ou auquel il est inscrit (console).</summary>
public sealed record MemberEvent(Guid Id, string Title, string City, DateTimeOffset StartsAt, bool IsOrganizer, int Participants);

public sealed record EventExportRow(
    string Title, string Category, string City, string Country, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int? Capacity, int Participants);

public interface IEventsModule
{
    Task<EventStats> GetStatsAsync(DateTimeOffset now, CancellationToken ct);

    /// <summary>Événements organisés ou suivis par un membre, les plus récents d'abord.</summary>
    Task<IReadOnlyList<MemberEvent>> GetMemberEventsAsync(Guid userId, int take, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, int>> CountByCommunityAsync(CancellationToken ct);

    Task<IReadOnlyList<EventExportRow>> GetExportRowsAsync(CancellationToken ct);
}
