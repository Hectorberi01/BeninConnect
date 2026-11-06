using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Features.Authentication.Models;
using BeninConnect.App.Features.Notifications.Models;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

/// <summary>Authentification 100 % locale. Aucun email n'est envoyé, aucun mot de passe n'est persisté.</summary>
public sealed class MockAuthService(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
    : MockRepositoryBase(store, messenger, developer, clock), IAuthService
{
    public const string DemoEmail = DemoAccount.Email;
    public const string DemoPassword = DemoAccount.Password;

    public string DemoVerificationCode => "123456";

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var normalized = email.Trim();

        lock (Store.SyncRoot)
        {
            if (!Store.DemoCredentials.TryGetValue(normalized, out var expected) || expected != password)
            {
                return AuthResult.Failure("Email ou mot de passe incorrect.");
            }

            if (Store.PendingVerifications.Contains(normalized))
            {
                return AuthResult.Failure("Vérifiez d'abord votre adresse email.");
            }

            var user = Store.Users.FirstOrDefault(u => string.Equals(u.Email, normalized, StringComparison.OrdinalIgnoreCase));
            return user is null ? AuthResult.Failure("Compte introuvable.") : AuthResult.Success(user);
        }
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var email = request.Email.Trim();

        lock (Store.SyncRoot)
        {
            if (Store.DemoCredentials.ContainsKey(email) || Store.Users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                return AuthResult.Failure("Un compte existe déjà avec cette adresse email.");
            }

            var user = new User
            {
                Id = NewId(),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                Country = request.Country.Trim(),
                City = request.City.Trim(),
                AvatarColor = "#0E7490",
                CoverImage = "Profile/cover_02.jpg",
                JoinedAt = Now,
                IsEmailVerified = false
            };

            Store.Users.Add(user);
            Store.DemoCredentials[email] = request.Password; // mémoire de session uniquement
            Store.PendingVerifications.Add(email);
            return AuthResult.Success(user);
        }
    }

    public async Task<AuthResult> VerifyEmailAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        if (code.Trim() != DemoVerificationCode)
        {
            return AuthResult.Failure("Code incorrect. En démonstration, le code est 123456.");
        }

        User user;
        lock (Store.SyncRoot)
        {
            Store.PendingVerifications.Remove(email.Trim());
            user = MockDataStore.Replace(
                Store.Users,
                u => string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase),
                u => u with { IsEmailVerified = true },
                "Compte");

            Store.Notifications.Add(new Notification
            {
                Id = NewId(),
                UserId = user.Id,
                Category = NotificationCategory.System,
                Title = "Bienvenue sur BéninConnect",
                Body = "Complétez votre profil et rejoignez vos premières communautés.",
                CreatedAt = Now,
                Target = NotificationTarget.Profile,
                TargetId = user.Id
            });
        }

        return AuthResult.Success(user);
    }

    public async Task ResendVerificationCodeAsync(string email, CancellationToken cancellationToken = default) =>
        await SimulateNetworkAsync(cancellationToken);

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default) =>
        await SimulateNetworkAsync(cancellationToken);

    public async Task<User?> RestoreSessionAsync(string email, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Users.FirstOrDefault(u =>
                u.IsEmailVerified && string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        }
    }
}
