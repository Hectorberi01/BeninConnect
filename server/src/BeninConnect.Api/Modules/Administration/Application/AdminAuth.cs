using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Administration.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Administration.Application;

public sealed record AdminLoginCommand(string Email, string Password) : ICommand<AdminAuthResponse>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Email(nameof(Email), Email).Concat(Rules.Required(nameof(Password), Password, "Le mot de passe"));
}

public sealed record GetAdminMeQuery : IQuery<AdminMeDto>;

public sealed record ChangeAdminPasswordCommand(string CurrentPassword, string NewPassword) : ICommand<AdminMeDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => AdminRules.Password(nameof(NewPassword), NewPassword);
}

internal static class AdminRules
{
    // Comptes à privilèges : 10 caractères minimum, avec lettres et chiffres.
    public static IEnumerable<(string, string)> Password(string field, string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 10 || !value.Any(char.IsLetter) || !value.Any(char.IsDigit))
        {
            yield return (field, "Le mot de passe doit contenir au moins 10 caractères, dont des lettres et des chiffres.");
        }
    }

    public static AdminMeDto ToMe(this AdminUser a) => new(a.Id, a.Email, a.DisplayName, a.Role, RolePermissions.For(a.Role));
}

public sealed class AdminAuthHandlers(IAdministrationDb db, IPasswordHasher hasher, IAdminTokenService tokens, AdminSession admin, TimeProvider clock) :
    IRequestHandler<AdminLoginCommand, AdminAuthResponse>,
    IRequestHandler<GetAdminMeQuery, AdminMeDto>,
    IRequestHandler<ChangeAdminPasswordCommand, AdminMeDto>
{
    public async Task<AdminAuthResponse> Handle(AdminLoginCommand request, CancellationToken ct)
    {
        var email = AdminUser.NormalizeEmail(request.Email);
        var account = await db.AdminUsers.FirstOrDefaultAsync(a => a.Email == email, ct);
        if (account is null || !hasher.Verify(account.PasswordHash, request.Password))
        {
            throw new DomainException("Email ou mot de passe incorrect.");
        }

        if (!account.IsActive)
        {
            throw new ForbiddenException("Ce compte administrateur est désactivé.");
        }

        account.RecordLogin(clock.GetUtcNow());
        db.AuditEntries.Add(AuditEntry.Create(account, "Connexion", "Admin", account.Id, "Connexion à la console", clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return new AdminAuthResponse(tokens.CreateToken(account), account.ToMe());
    }

    public async Task<AdminMeDto> Handle(GetAdminMeQuery request, CancellationToken ct) => (await admin.GetAccountAsync(ct)).ToMe();

    public async Task<AdminMeDto> Handle(ChangeAdminPasswordCommand request, CancellationToken ct)
    {
        var account = await admin.GetAccountAsync(ct);
        if (!hasher.Verify(account.PasswordHash, request.CurrentPassword))
        {
            throw new DomainException("Mot de passe actuel incorrect.");
        }

        account.ChangePassword(hasher.Hash(request.NewPassword));
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Mot de passe modifié", "Admin", account.Id, "Changement de son mot de passe", ct);
        return account.ToMe();
    }
}
