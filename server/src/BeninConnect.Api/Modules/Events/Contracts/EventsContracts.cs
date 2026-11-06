namespace BeninConnect.Modules.Events.Contracts;

public sealed record EventStats(int Upcoming, int Participations);

public sealed record EventExportRow(
    string Title, string Category, string City, string Country, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int? Capacity, int Participants);

public interface IEventsModule
{
    Task<EventStats> GetStatsAsync(DateTimeOffset now, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, int>> CountByCommunityAsync(CancellationToken ct);

    Task<IReadOnlyList<EventExportRow>> GetExportRowsAsync(CancellationToken ct);
}
