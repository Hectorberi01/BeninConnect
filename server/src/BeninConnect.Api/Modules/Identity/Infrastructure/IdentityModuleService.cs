using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Identity.Domain;
using BeninConnect.Modules.Identity.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Infrastructure;

/// <summary>Implémentation du contrat public (appel direct en monolithe, client HTTP/gRPC après découpage).</summary>
public sealed class IdentityModuleService(IdentityDbContext db, TimeProvider clock, IPasswordHasher hasher, IEmailSender email, UserViews views)
    : IIdentityModule, IModuleStatistics
{
    public async Task<bool> SendEmailAsync(Guid userId, string subject, string textBody, CancellationToken ct)
    {
        var to = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsEmailVerified && !u.IsSuspended).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (to is null)
        {
            return false;
        }

        await email.SendAsync(to, subject, textBody, ct);
        return true;
    }

    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => db.Users.AnyAsync(u => u.Id == userId, ct);

    public async Task<IReadOnlyDictionary<Guid, UserDto>> GetUsersAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return (await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToListAsync(ct)).ToDictionary(u => u.Id, u => u.ToDto());
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct); // compte officiel : sans nom de famille
    }

    public async Task<string> SuspendAsync(Guid userId, string reason, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("Membre");
        user.Suspend(reason, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return user.DisplayName;
    }

    public async Task<MemberStats> GetStatsAsync(DateTimeOffset since, CancellationToken ct)
    {
        var countries = (await db.Users.AsNoTracking()
                .GroupBy(u => u.Country).Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(8).ToListAsync(ct))
            .Select(x => new LabelCount(x.Key, x.Count)).ToList();

        return new MemberStats(
            await db.Users.CountAsync(ct),
            await db.Users.CountAsync(u => u.IsEmailVerified, ct),
            await db.Users.CountAsync(u => u.IsSuspended, ct),
            await GetJoinDatesAsync(since, ct),
            countries);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> GetJoinDatesAsync(DateTimeOffset since, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.JoinedAt >= since).Select(u => u.JoinedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<MemberExportRow>> GetExportRowsAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.JoinedAt)
            .Select(u => new MemberExportRow(u.Id, u.FirstName, u.LastName, u.Email, u.Country, u.City, u.JoinedAt, u.IsEmailVerified, u.IsSuspended))
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> GetBlockedRelationsAsync(Guid userId, CancellationToken ct)
    {
        var ids = await db.Blocks.AsNoTracking()
            .Where(b => b.BlockerId == userId || b.BlockedId == userId)
            .Select(b => b.BlockerId == userId ? b.BlockedId : b.BlockerId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task<Guid> EnsureOfficialAccountAsync(string name, string email, string bio, CancellationToken ct)
    {
        var normalized = User.NormalizeEmail(email);
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (existing is not null)
        {
            return existing.IsOfficial
                ? existing.Id
                : throw new DomainException($"{normalized} est déjà le compte d'un membre : choisissez une autre adresse pour le compte officiel.");
        }

        // Mot de passe aléatoire jamais communiqué : la connexion est de toute façon refusée
        var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var user = User.CreateOfficial(Guid.NewGuid(), name, normalized, hasher.Hash(password), bio, clock.GetUtcNow());
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    public async Task<IReadOnlyDictionary<Guid, UserDto>> GetUsersForViewerAsync(Guid? viewerId, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToListAsync(ct);
        return (await views.ForAsync(viewerId, users, ct)).ToDictionary(u => u.Id);
    }

    public Task<bool> AcceptsNewMessagesAsync(Guid userId, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.AllowMessages).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(Guid? after, int take, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.IsEmailVerified && !u.IsSuspended && !u.IsOfficial && (after == null || u.Id.CompareTo(after.Value) > 0))
            .OrderBy(u => u.Id).Take(take).Select(u => u.Id).ToListAsync(ct);

    public async Task<Guid> GetOfficialAccountAsync(CancellationToken ct)
    {
        var existing = await db.Users.AsNoTracking().Where(u => u.IsOfficial).OrderBy(u => u.JoinedAt).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        return existing ?? await EnsureOfficialAccountAsync("BéninConnect", "officiel@monbenin.com",
            "Compte officiel de BéninConnect : actualités, culture et vie de la diaspora béninoise. Publications vérifiées par notre équipe.", ct);
    }

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) => [new("Membres", await db.Users.CountAsync(ct))];
}
