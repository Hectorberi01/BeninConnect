using System.Security.Cryptography;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Identity.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Application;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthResponse>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Email(nameof(Email), Email).Concat(Rules.Required(nameof(Password), Password, "Le mot de passe"));
}

public sealed class LoginHandler(IIdentityDb db, IPasswordHasher hasher, IMemberTokenService tokens) : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        // Compte officiel : jamais de connexion (publie seulement via le module éditorial)
        if (user is null || user.IsOfficial || !hasher.Verify(user.PasswordHash, request.Password))
        {
            throw new DomainException("Email ou mot de passe incorrect.");
        }

        if (!user.IsEmailVerified)
        {
            throw new DomainException("Vérifiez d'abord votre adresse email.");
        }

        if (user.IsSuspended)
        {
            throw new ForbiddenException("Votre compte est suspendu. Contactez l'équipe BéninConnect.");
        }

        return new AuthResponse(tokens.CreateToken(user), user.ToDto());
    }
}

public sealed record RegisterCommand(string FirstName, string LastName, string Email, string Country, string City, string Password)
    : ICommand<UserDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Length(nameof(FirstName), FirstName, "Le prénom", 2, 40)
            .Concat(Rules.Length(nameof(LastName), LastName, "Le nom", 2, 40))
            .Concat(Rules.Email(nameof(Email), Email))
            .Concat(Rules.Required(nameof(Country), Country, "Le pays"))
            .Concat(Rules.Required(nameof(City), City, "La ville"))
            .Concat(Rules.Password(nameof(Password), Password));
}

internal static class AuthEmails
{
    public static string NewCode(AuthSettings settings) => settings.FixedVerificationCode ?? RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static Task SendVerificationAsync(IEmailSender email, User user, string code, CancellationToken ct) =>
        email.SendAsync(user.Email, $"{code} est votre code BéninConnect",
            $"Bonjour {user.FirstName},\n\nVotre code de vérification BéninConnect : {code}\n\n" +
            "Saisissez-le dans l'application pour activer votre compte.\nSi vous n'êtes pas à l'origine de cette inscription, ignorez cet email.\n\nL'équipe BéninConnect", ct);

    public static Task SendResetAsync(IEmailSender email, User user, string code, int minutes, CancellationToken ct) =>
        email.SendAsync(user.Email, "Réinitialisation de votre mot de passe BéninConnect",
            $"Bonjour {user.FirstName},\n\nVotre code de réinitialisation : {code} (valable {minutes} minutes).\n\n" +
            "Si vous n'avez rien demandé, ignorez cet email : votre mot de passe reste inchangé.\n\nL'équipe BéninConnect", ct);
}

public sealed class RegisterHandler(IIdentityDb db, IPasswordHasher hasher, AuthSettings settings, IEmailSender mailer, TimeProvider clock)
    : IRequestHandler<RegisterCommand, UserDto>
{
    public async Task<UserDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("Un compte existe déjà avec cette adresse email.");
        }

        var code = AuthEmails.NewCode(settings);
        var user = User.Register(
            Guid.NewGuid(), request.FirstName, request.LastName, email, hasher.Hash(request.Password),
            request.Country, request.City, clock.GetUtcNow(),
            IdentityCatalog.AvatarColors[Math.Abs(email.GetHashCode()) % IdentityCatalog.AvatarColors.Length], code);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await AuthEmails.SendVerificationAsync(mailer, user, code, ct);
        return user.ToDto();
    }
}

public sealed record VerifyEmailCommand(string Email, string Code) : ICommand<AuthResponse>, IValidatable
{
    public IEnumerable<(string, string)> Validate()
    {
        if (Code?.Trim() is not { Length: 6 } c || !c.All(char.IsDigit))
        {
            yield return (nameof(Code), "Le code contient 6 chiffres.");
        }
    }
}

public sealed class VerifyEmailHandler(IIdentityDb db, IMemberTokenService tokens, IEventBus bus) : IRequestHandler<VerifyEmailCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct) ?? throw new NotFoundException("Compte");

        var wasVerified = user.IsEmailVerified;
        user.VerifyEmail(request.Code);
        await db.SaveChangesAsync(ct);

        if (!wasVerified)
        {
            await bus.PublishAsync(new MemberVerified(user.Id), ct);
        }

        return new AuthResponse(tokens.CreateToken(user), user.ToDto());
    }
}

/// <summary>Renvoi du code / mot de passe oublié : réponse identique que le compte existe ou non.</summary>
public sealed record ResendVerificationCodeCommand(string Email) : ICommand<bool>;

public sealed record ForgotPasswordCommand(string Email) : ICommand<bool>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Email(nameof(Email), Email);
}

public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword) : ICommand<bool>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Email(nameof(Email), Email).Concat(Rules.Password(nameof(NewPassword), NewPassword));
}

/// <summary>Réponse identique que le compte existe ou non (pas d'énumération des emails).</summary>
public sealed class AccountEmailHandlers(IIdentityDb db, IPasswordHasher hasher, AuthSettings settings, IEmailSender email, TimeProvider clock) :
    IRequestHandler<ResendVerificationCodeCommand, bool>,
    IRequestHandler<ForgotPasswordCommand, bool>,
    IRequestHandler<ResetPasswordCommand, bool>
{
    public async Task<bool> Handle(ResendVerificationCodeCommand request, CancellationToken ct)
    {
        var normalized = User.NormalizeEmail(request.Email ?? string.Empty);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is { IsEmailVerified: false })
        {
            var code = AuthEmails.NewCode(settings);
            user.RenewVerificationCode(code);
            await db.SaveChangesAsync(ct);
            await AuthEmails.SendVerificationAsync(email, user, code, ct);
        }

        return true;
    }

    public async Task<bool> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var normalized = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is { IsSuspended: false, IsOfficial: false })
        {
            var code = AuthEmails.NewCode(settings);
            user.RequestPasswordReset(code, clock.GetUtcNow().AddMinutes(settings.PasswordResetMinutes));
            await db.SaveChangesAsync(ct);
            await AuthEmails.SendResetAsync(email, user, code, settings.PasswordResetMinutes, ct);
        }

        return true;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var normalized = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct)
                   ?? throw new DomainException("Code expiré : demandez un nouveau code.");
        try
        {
            user.ResetPassword(request.Code, hasher.Hash(request.NewPassword), clock.GetUtcNow());
        }
        finally
        {
            await db.SaveChangesAsync(ct); // garde le compteur d'essais même en cas d'échec
        }

        return true;
    }
}

public sealed record GetMeQuery : IQuery<UserDto>;

public sealed class GetMeHandler(IIdentityDb db, ICurrentUser me) : IRequestHandler<GetMeQuery, UserDto>
{
    public async Task<UserDto> Handle(GetMeQuery request, CancellationToken ct) =>
        (await db.Users.FindAsync([me.Id], ct) ?? throw new NotFoundException("Compte")).ToDto();
}
