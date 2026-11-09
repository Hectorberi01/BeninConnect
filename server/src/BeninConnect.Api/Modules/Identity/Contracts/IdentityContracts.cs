using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Identity.Contracts;

// Contrat public du module Identity : seule partie visible des autres modules.

public sealed record UserDto(
    Guid Id, string FirstName, string LastName, string Email, string Profession, string Country, string City,
    string OriginCity, string Bio, IReadOnlyList<string> Interests, string AvatarColor, string? CoverImage,
    DateTimeOffset JoinedAt, bool IsEmailVerified, string? AvatarImage);

public sealed record MemberStats(int Total, int Verified, int Suspended, IReadOnlyList<DateTimeOffset> JoinedSince, IReadOnlyList<LabelCount> TopCountries);

public sealed record MemberExportRow(
    Guid Id, string FirstName, string LastName, string Email, string Country, string City, DateTimeOffset JoinedAt, bool IsEmailVerified, bool IsSuspended);

public interface IIdentityModule
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, UserDto>> GetUsersAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Prénom + nom ; les membres absents ne figurent pas dans le résultat.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Suspension décidée par la modération ; retourne le nom du membre.</summary>
    Task<string> SuspendAsync(Guid userId, string reason, CancellationToken ct);

    Task<MemberStats> GetStatsAsync(DateTimeOffset since, CancellationToken ct);

    Task<IReadOnlyList<DateTimeOffset>> GetJoinDatesAsync(DateTimeOffset since, CancellationToken ct);

    Task<IReadOnlyList<MemberExportRow>> GetExportRowsAsync(CancellationToken ct);

    /// <summary>Membres que userId a bloqués ou qui l'ont bloqué : contenus et messages masqués dans les deux sens.</summary>
    Task<IReadOnlySet<Guid>> GetBlockedRelationsAsync(Guid userId, CancellationToken ct);
}

/// <summary>Email vérifié pour la première fois (message de bienvenue).</summary>
public sealed record MemberVerified(Guid UserId) : IIntegrationEvent;

/// <summary>Compte supprimé par son titulaire : chaque module efface les données du membre.</summary>
public sealed record MemberDeleted(Guid UserId) : IIntegrationEvent;
