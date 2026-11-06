using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Administration.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Administration.Application;

public sealed record GetAdminAccountsQuery : IQuery<IReadOnlyList<AdminAccountDto>>;

public sealed record CreateAdminCommand(string Email, string DisplayName, AdminRole Role, string Password) : ICommand<AdminAccountDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Email(nameof(Email), Email)
            .Concat(Rules.Length(nameof(DisplayName), DisplayName, "Le nom", 2, 80))
            .Concat(AdminRules.Password(nameof(Password), Password));
}

public sealed record UpdateAdminCommand(Guid Id, string DisplayName, AdminRole Role, bool IsActive) : ICommand<AdminAccountDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Length(nameof(DisplayName), DisplayName, "Le nom", 2, 80);
}

public sealed record ResetAdminPasswordCommand(Guid Id, string Password) : ICommand<AdminAccountDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => AdminRules.Password(nameof(Password), Password);
}

public sealed record GetAuditQuery(string? Search, int Skip = 0, int Take = 50) : IQuery<PagedResult<AuditEntryDto>>;

public sealed class AdminAccountHandlers(IAdministrationDb db, AdminSession admin, IPasswordHasher hasher, TimeProvider clock) :
    IRequestHandler<GetAdminAccountsQuery, IReadOnlyList<AdminAccountDto>>,
    IRequestHandler<CreateAdminCommand, AdminAccountDto>,
    IRequestHandler<UpdateAdminCommand, AdminAccountDto>,
    IRequestHandler<ResetAdminPasswordCommand, AdminAccountDto>,
    IRequestHandler<GetAuditQuery, PagedResult<AuditEntryDto>>
{
    private static AdminAccountDto ToDto(AdminUser a) => new(a.Id, a.Email, a.DisplayName, a.Role, a.IsActive, a.CreatedAt, a.LastLoginAt);

    public async Task<IReadOnlyList<AdminAccountDto>> Handle(GetAdminAccountsQuery request, CancellationToken ct) =>
        (await db.AdminUsers.AsNoTracking().OrderBy(a => a.Role).ThenBy(a => a.DisplayName).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<AdminAccountDto> Handle(CreateAdminCommand request, CancellationToken ct)
    {
        var email = AdminUser.NormalizeEmail(request.Email);
        if (await db.AdminUsers.AnyAsync(a => a.Email == email, ct))
        {
            throw new ConflictException("Un compte administrateur existe déjà avec cette adresse email.");
        }

        var account = AdminUser.Create(Guid.NewGuid(), email, request.DisplayName, hasher.Hash(request.Password), request.Role, clock.GetUtcNow());
        db.AdminUsers.Add(account);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Compte admin créé", "Admin", account.Id, $"{account.DisplayName} ({account.Role})", ct);
        return ToDto(account);
    }

    public async Task<AdminAccountDto> Handle(UpdateAdminCommand request, CancellationToken ct)
    {
        var me = await admin.GetAccountAsync(ct);
        var account = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == request.Id, ct) ?? throw new NotFoundException("Compte administrateur");
        if (account.Id == me.Id && (request.Role != account.Role || !request.IsActive))
        {
            throw new DomainException("Vous ne pouvez pas modifier votre propre rôle ni désactiver votre compte.");
        }

        var removesSuperAdmin = account is { Role: AdminRole.SuperAdmin, IsActive: true } && (request.Role != AdminRole.SuperAdmin || !request.IsActive);
        if (removesSuperAdmin && await db.AdminUsers.CountAsync(a => a.Role == AdminRole.SuperAdmin && a.IsActive, ct) <= 1)
        {
            throw new DomainException("Il doit rester au moins un SuperAdmin actif.");
        }

        var before = $"{account.Role}, {(account.IsActive ? "actif" : "désactivé")}";
        account.Update(request.DisplayName, request.Role, request.IsActive);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Compte admin modifié", "Admin", account.Id,
            $"{account.DisplayName} : {before} → {account.Role}, {(account.IsActive ? "actif" : "désactivé")}", ct);
        return ToDto(account);
    }

    public async Task<AdminAccountDto> Handle(ResetAdminPasswordCommand request, CancellationToken ct)
    {
        var account = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == request.Id, ct) ?? throw new NotFoundException("Compte administrateur");
        account.ChangePassword(hasher.Hash(request.Password));
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Mot de passe admin réinitialisé", "Admin", account.Id, account.DisplayName, ct);
        return ToDto(account);
    }

    public async Task<PagedResult<AuditEntryDto>> Handle(GetAuditQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var entries = db.AuditEntries.AsNoTracking();
        if (Text.Like(request.Search) is { } q)
        {
            entries = entries.Where(e => e.Action.ToLower().Contains(q) || e.AdminName.ToLower().Contains(q) || e.Details.ToLower().Contains(q));
        }

        var total = await entries.CountAsync(ct);
        var items = await entries.OrderByDescending(e => e.At).Skip(skip).Take(take)
            .Select(e => new AuditEntryDto(e.Id, e.AdminName, e.Action, e.TargetType, e.TargetId, e.Details, e.At)).ToListAsync(ct);
        return new PagedResult<AuditEntryDto>(items, total, skip);
    }
}
