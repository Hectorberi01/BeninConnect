using BeninConnect.Modules.AppVersionManagement.Domain;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.AppVersionManagement.Application;

/// <summary>Réponse à l'application : Status UP_TO_DATE, UPDATE_AVAILABLE, UPDATE_REQUIRED ou UNKNOWN.</summary>
public sealed record AppVersionStatusDto(
    string CurrentVersion, string? LatestVersion, string? MinimumSupportedVersion, string Status, bool ForceUpdate, string StoreUrl);

/// <summary>
/// Version installée, envoyée au démarrage, après connexion et au retour au premier plan. Le membre vient du jeton,
/// jamais du corps de la requête ; un appel anonyme est accepté (avant connexion).
/// </summary>
public sealed record ReportInstallationCommand(
    Guid InstallationId, string? AppCode, string Platform, string Version, string? BuildNumber, string? OsVersion, string? PushToken)
    : ICommand<AppVersionStatusDto>;

/// <summary>Ancienne route (applications d'avant le suivi des installations) : version minimale seulement.</summary>
public sealed record GetMinimumVersionQuery(string? Platform) : IQuery<string?>;

public sealed class InstallationHandlers(IAppVersionsDb db, AppVersionSettings settings, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<ReportInstallationCommand, AppVersionStatusDto>,
    IRequestHandler<GetMinimumVersionQuery, string?>
{
    public async Task<AppVersionStatusDto> Handle(ReportInstallationCommand c, CancellationToken ct)
    {
        if (c.InstallationId == Guid.Empty)
        {
            throw new Shared.Domain.DomainException("Identifiant d'installation manquant.");
        }

        var appCode = c.AppCode is null ? settings.AppCode : VersionRules.AppCode(c.AppCode);
        var platform = VersionRules.Platform(c.Platform);
        var version = VersionRules.Version(c.Version);
        var build = VersionRules.Build(c.BuildNumber);
        var os = VersionRules.Text(c.OsVersion, VersionRules.MaxOsLength);
        var push = VersionRules.Text(c.PushToken, AppInstallation.MaxPushTokenLength);
        Guid? userId = me.IsAuthenticated ? me.Id : null;
        var now = clock.GetUtcNow();

        for (var attempt = 0; ; attempt++)
        {
            var installation = await db.Installations.FirstOrDefaultAsync(i => i.InstallationId == c.InstallationId && i.AppCode == appCode, ct);
            if (installation is null)
            {
                installation = AppInstallation.Create(c.InstallationId, appCode, platform, now);
                db.Installations.Add(installation);
            }

            installation.Report(userId, platform, version, build, os, push, now);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Deux premiers appels simultanés : l'autre a créé la ligne, on la met à jour
                db.Installations.Entry(installation).State = EntityState.Detached;
            }
        }

        var release = (await AppVersionsData.ReleasesAsync(db, settings, ct))[platform];
        var status = release.StatusOf(version);
        return new AppVersionStatusDto(version, release.LatestVersion, release.MinimumSupportedVersion, AppVersionsData.Code(status),
            status == UpdateStatus.UpdateRequired, release.StoreUrl);
    }

    public async Task<string?> Handle(GetMinimumVersionQuery q, CancellationToken ct)
    {
        var releases = await AppVersionsData.ReleasesAsync(db, settings, ct);
        if (q.Platform?.Trim().ToLowerInvariant() is { } p && releases.TryGetValue(p, out var one))
        {
            return one.MinimumSupportedVersion;
        }

        // Plateforme inconnue : le plus petit minimum, pour ne jamais bloquer à tort l'autre plateforme
        return releases.Values.Any(r => r.MinimumSupportedVersion is null)
            ? null
            : releases.Values.Select(r => r.MinimumSupportedVersion!).OrderBy(v => v, Comparer<string>.Create(VersionRules.Compare)).First();
    }
}

/// <summary>Compte supprimé : ses installations et ses relances sont effacées.</summary>
public sealed class DeleteMemberInstallations(IAppVersionsDb db) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        await db.Installations.Where(i => i.UserId == e.UserId).ExecuteDeleteAsync(ct);
        await db.Recipients.Where(r => r.UserId == e.UserId).ExecuteDeleteAsync(ct);
    }
}
