using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.Features.Authentication.Contracts;

public sealed record AuthResult(bool Succeeded, User? User = null, string? Error = null)
{
    public static AuthResult Success(User user) => new(true, user);

    public static AuthResult Failure(string error) => new(false, Error: error);
}

public sealed record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Country,
    string City,
    string Password);

public interface IAuthService
{
    /// <summary>Code affiché à l'écran en mode démonstration (aucun email réel n'est envoyé).</summary>
    string DemoVerificationCode { get; }

    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> VerifyEmailAsync(string email, string code, CancellationToken cancellationToken = default);

    Task ResendVerificationCodeAsync(string email, CancellationToken cancellationToken = default);

    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Restaure la session du compte mémorisé (option « Rester connecté »).</summary>
    Task<User?> RestoreSessionAsync(string email, CancellationToken cancellationToken = default);
}
