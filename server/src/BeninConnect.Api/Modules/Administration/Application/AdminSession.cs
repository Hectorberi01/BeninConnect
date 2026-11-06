using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Administration.Domain;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Administration.Application;

public interface IAdministrationDb
{
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<AuditEntry> AuditEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Administrateur authentifié (jeton de la console, distinct de celui des membres).</summary>
public interface ICurrentAdmin
{
    Guid Id { get; }
}

public interface IAdminTokenService
{
    string CreateToken(AdminUser admin);
}

public sealed record AdminMeDto(Guid Id, string Email, string DisplayName, AdminRole Role, IReadOnlyList<Permission> Permissions);

public sealed record AdminAuthResponse(string Token, AdminMeDto Admin);

public sealed record AdminAccountDto(Guid Id, string Email, string DisplayName, AdminRole Role, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);

public sealed record AuditEntryDto(Guid Id, string AdminName, string Action, string TargetType, Guid? TargetId, string Details, DateTimeOffset At);

/// <summary>Implémente IAdminSession pour tous les modules : administrateur courant + journal d'audit.</summary>
public sealed class AdminSession(IAdministrationDb db, ICurrentAdmin current, TimeProvider clock) : IAdminSession
{
    private AdminUser? _admin;

    public async Task<AdminUser> GetAccountAsync(CancellationToken ct) =>
        _admin ??= await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == current.Id && a.IsActive, ct)
                   ?? throw new UnauthorizedAccessException("Session administrateur expirée.");

    public async Task<AdminInfo> GetAsync(CancellationToken ct)
    {
        var a = await GetAccountAsync(ct);
        return new AdminInfo(a.Id, a.DisplayName, a.Role.ToString());
    }

    public async Task RequireAsync(Permission permission, CancellationToken ct)
    {
        if (!RolePermissions.Has((await GetAccountAsync(ct)).Role, permission))
        {
            throw new ForbiddenException("Votre rôle ne permet pas cette action.");
        }
    }

    public async Task LogAsync(string action, string targetType, Guid? targetId, string details, CancellationToken ct)
    {
        db.AuditEntries.Add(AuditEntry.Create(await GetAccountAsync(ct), action, targetType, targetId, details, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
    }
}
