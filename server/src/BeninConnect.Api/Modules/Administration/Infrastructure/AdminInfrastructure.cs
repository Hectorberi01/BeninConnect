using System.Security.Claims;
using BeninConnect.Modules.Administration.Application;
using BeninConnect.Modules.Administration.Domain;
using BeninConnect.Modules.Administration.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BeninConnect.Modules.Administration.Infrastructure;

public sealed class AdminTokenService(JwtOptions options, TimeProvider clock) : IAdminTokenService
{
    public const string RoleClaim = "role";

    public string CreateToken(AdminUser admin) => options.CreateToken(options.AdminAudience, clock.GetUtcNow().UtcDateTime,
        TimeSpan.FromHours(options.AdminLifetimeHours),
    [
        new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, admin.Email),
        new Claim(JwtRegisteredClaimNames.Name, admin.DisplayName),
        new Claim(RoleClaim, admin.Role.ToString())
    ]);
}

/// <summary>Premier SuperAdmin hors démonstration : créé depuis la configuration si aucun compte n'existe.</summary>
public static class AdminBootstrap
{
    public static async Task EnsureAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        var email = config["Admin:BootstrapEmail"];
        var password = config["Admin:BootstrapPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var db = services.GetRequiredService<AdministrationDbContext>();
        if (await db.AdminUsers.AnyAsync(ct))
        {
            return;
        }

        var hasher = services.GetRequiredService<IPasswordHasher>();
        db.AdminUsers.Add(AdminUser.Create(Guid.NewGuid(), email, "Super administrateur", hasher.Hash(password), AdminRole.SuperAdmin,
            services.GetRequiredService<TimeProvider>().GetUtcNow()));
        await db.SaveChangesAsync(ct);
    }
}
