using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Application;

public interface IIdentityDb
{
    DbSet<User> Users { get; }
    DbSet<UserBlock> Blocks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IMemberTokenService
{
    string CreateToken(User user);
}

/// <summary>Envoi d'emails transactionnels (SMTP en production, journal en développement).</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string textBody, CancellationToken ct);
}

public sealed class AuthSettings
{
    /// <summary>Code fixe (développement uniquement : refusé en production). null = code aléatoire.</summary>
    public string? FixedVerificationCode { get; set; }

    public int PasswordResetMinutes { get; set; } = 30;
}

public sealed record AuthResponse(string Token, UserDto User);

public static class IdentityCatalog
{
    public static readonly string[] Interests =
    [
        "Entrepreneuriat", "Tech & numérique", "Cuisine béninoise", "Musique", "Football", "Études",
        "Vie associative", "Voyages", "Patrimoine & culture", "Mode", "Littérature", "Investissement",
        "Santé & bien-être", "Photographie", "Danse", "Cinéma", "Agriculture", "Immobilier"
    ];

    public static readonly string[] AvatarColors =
        ["#087F5B", "#0E7490", "#B45309", "#7C3AED", "#BE123C", "#1D4ED8", "#15803D", "#C2410C", "#4338CA", "#0F766E", "#A21CAF", "#9A3412"];
}

public static class UserMapping
{
    public static UserDto ToDto(this User u) => new(
        u.Id, u.FirstName, u.LastName, u.Email, u.Profession, u.Country, u.City, u.OriginCity, u.Bio,
        u.Interests.ToList(), u.AvatarColor, u.CoverImage, u.JoinedAt, u.IsEmailVerified, u.AvatarImage);
}
