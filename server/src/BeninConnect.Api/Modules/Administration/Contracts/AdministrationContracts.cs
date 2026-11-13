using Microsoft.AspNetCore.Builder;

namespace BeninConnect.Modules.Administration.Contracts;

/// <summary>Droits de la console (vérifiés par l'API, transmis à la console pour adapter les menus).</summary>
public enum Permission
{
    DashboardView,
    MembersView,
    MembersSuspend,
    ContentModerate,
    ReportsManage,
    CommunitiesManage,
    EventsManage,
    ExportsDownload,
    AdminsManage,
    AuditView,
    SystemView,
    SystemReset,
    BackupsManage,
    EditorialManage,
    JobsManage,
    ServicesManage,
    ServicesDelete // SuperAdmin uniquement
}

public sealed record AdminInfo(Guid Id, string DisplayName, string Role);

/// <summary>Administrateur de la requête courante, vérification de droits et journal d'audit.</summary>
public interface IAdminSession
{
    Task<AdminInfo> GetAsync(CancellationToken ct);

    Task RequireAsync(Permission permission, CancellationToken ct);

    /// <summary>Enregistre immédiatement l'entrée (à appeler après la modification).</summary>
    Task LogAsync(string action, string targetType, Guid? targetId, string details, CancellationToken ct);
}

public static class AdminPolicies
{
    public const string Scheme = "Admin";

    public static string Policy(Permission permission) => $"admin:{permission}";

    /// <summary>Route de la console : jeton admin + rôle disposant de la permission.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, Permission permission) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(Policy(permission));
}
