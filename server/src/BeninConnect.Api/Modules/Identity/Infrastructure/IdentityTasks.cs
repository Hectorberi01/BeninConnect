using BeninConnect.Modules.Identity.Application;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Infrastructure;

/// <summary>Section « Identity:Cleanup ».</summary>
public sealed class AccountCleanupSettings
{
    public const string Section = "Identity:Cleanup";

    /// <summary>Compte jamais vérifié supprimé après ce délai (0 : jamais).</summary>
    public int UnverifiedAccountDays { get; set; } = 30;
}

/// <summary>
/// Comptes : codes de réinitialisation expirés effacés, comptes jamais vérifiés supprimés après UnverifiedAccountDays
/// (même chemin que la suppression par le membre : MemberDeleted).
/// </summary>
public sealed class AccountsCleanupTask(IIdentityDb db, IEventBus bus, AccountCleanupSettings settings, TimeProvider clock) : IScheduledTask
{
    private const int Batch = 100;

    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var codes = await db.Users.Where(u => u.PasswordResetExpiresAt != null && u.PasswordResetExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.PasswordResetCode, (string?)null)
                .SetProperty(u => u.PasswordResetExpiresAt, (DateTimeOffset?)null)
                .SetProperty(u => u.PasswordResetAttempts, 0), ct);

        var removed = 0;
        if (settings.UnverifiedAccountDays > 0)
        {
            var limit = now.AddDays(-settings.UnverifiedAccountDays);
            var stale = await db.Users.Where(u => !u.IsEmailVerified && u.JoinedAt < limit).OrderBy(u => u.JoinedAt).Take(Batch).ToListAsync(ct);
            foreach (var user in stale)
            {
                await AccountRemoval.RemoveAsync(db, bus, user, ct);
                removed++;
            }
        }

        return $"{codes} code(s) de réinitialisation expiré(s) effacé(s), {removed} compte(s) non vérifié(s) supprimé(s).";
    }
}

/// <summary>Photos de profil et de couverture.</summary>
public sealed class IdentityMediaReferences(IIdentityDb db) : IMediaReferences
{
    public async Task<IReadOnlyCollection<string>> ImagePathsAsync(CancellationToken ct)
    {
        var rows = await db.Users.AsNoTracking().Where(u => u.AvatarImage != null || u.CoverImage != null)
            .Select(u => new { u.AvatarImage, u.CoverImage }).ToListAsync(ct);
        return rows.SelectMany(r => new[] { r.AvatarImage, r.CoverImage }).OfType<string>().ToList();
    }
}
