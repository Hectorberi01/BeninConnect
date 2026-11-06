namespace BeninConnect.App.Shared.Models;

/// <summary>Membre de la communauté. Toutes les données de démonstration sont fictives.</summary>
public sealed record User
{
    public required Guid Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public string Profession { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string OriginCity { get; init; } = string.Empty;
    public string Bio { get; init; } = string.Empty;
    public IReadOnlyList<string> Interests { get; init; } = [];
    public string AvatarColor { get; init; } = "#087F5B";
    public string? CoverImage { get; init; }
    public DateTimeOffset JoinedAt { get; init; }
    public bool IsEmailVerified { get; init; } = true;

    public string DisplayName => $"{FirstName} {LastName}".Trim();

    public string Initials =>
        $"{FirstOrEmpty(FirstName)}{FirstOrEmpty(LastName)}".ToUpperInvariant();

    public string Location =>
        string.Join(", ", new[] { City, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string FirstOrEmpty(string value) => string.IsNullOrEmpty(value) ? string.Empty : value[..1];
}

public sealed record UpdateProfileRequest(
    Guid UserId,
    string FirstName,
    string LastName,
    string Profession,
    string Country,
    string City,
    string Bio,
    IReadOnlyList<string> Interests);

public sealed record UserUpdatedMessage(User User);
