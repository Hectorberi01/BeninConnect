using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Administration.Domain;

namespace BeninConnect.Modules.Administration.Application;

/// <summary>Matrice rôle → permissions.</summary>
public static class RolePermissions
{
    private static readonly Dictionary<AdminRole, Permission[]> Matrix = new()
    {
        [AdminRole.SuperAdmin] = Enum.GetValues<Permission>(),
        [AdminRole.Admin] =
        [
            Permission.DashboardView, Permission.MembersView, Permission.MembersSuspend, Permission.ContentModerate,
            Permission.ReportsManage, Permission.CommunitiesManage, Permission.EventsManage, Permission.ExportsDownload, Permission.AuditView,
            Permission.EditorialManage, Permission.JobsManage, Permission.ServicesManage
        ],
        [AdminRole.Moderator] =
        [
            Permission.DashboardView, Permission.MembersView, Permission.MembersSuspend, Permission.ContentModerate, Permission.ReportsManage,
            Permission.EditorialManage, Permission.JobsManage, Permission.ServicesManage
        ],
        [AdminRole.Accountant] = [Permission.DashboardView, Permission.ExportsDownload],
        [AdminRole.Developer] = [Permission.DashboardView, Permission.AuditView, Permission.SystemView, Permission.SystemReset, Permission.BackupsManage]
    };

    public static IReadOnlyList<Permission> For(AdminRole role) => Matrix[role];

    public static bool Has(AdminRole role, Permission permission) => Matrix[role].Contains(permission);

    public static IEnumerable<AdminRole> RolesWith(Permission permission) => Matrix.Where(kv => kv.Value.Contains(permission)).Select(kv => kv.Key);
}
