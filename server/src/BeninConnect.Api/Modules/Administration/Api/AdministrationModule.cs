using System.Reflection;
using System.Security.Claims;
using System.Text;
using BeninConnect.Modules.Administration.Application;
using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Administration.Domain;
using BeninConnect.Modules.Administration.Infrastructure;
using BeninConnect.Modules.Administration.Infrastructure.Persistence;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BeninConnect.Modules.Administration.Api;

public sealed record PasswordRequest(string Password);

public sealed record AdminUpdateRequest(string DisplayName, AdminRole Role, bool IsActive);

public sealed record SystemInfoDto(
    string Version, string Environment, string Runtime, DateTimeOffset StartedAt, string Uptime, bool DatabaseOnline,
    string Database, long MediaFiles, long MediaBytes, IReadOnlyList<LabelCount> Tables, bool CanResetDemo);

public sealed class HttpCurrentAdmin(IHttpContextAccessor accessor) : ICurrentAdmin
{
    public Guid Id =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("sub"), out var id) && accessor.HttpContext!.User.Identity?.AuthenticationType == AdminPolicies.Scheme
            ? id
            : throw new UnauthorizedAccessException("Authentification administrateur requise.");
}

/// <summary>Console d'administration : comptes admin, rôles, audit, tableau de bord, exports. Schéma JWT séparé des membres.</summary>
public sealed class AdministrationModule : IModule
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public string Name => "Administration";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<AdministrationDbContext>(builder.Configuration, Name, AdministrationDbContext.SchemaName);
        services.AddScoped<IAdministrationDb>(sp => sp.GetRequiredService<AdministrationDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Administration");
        services.AddScoped<AdminSession>();
        services.AddScoped<IAdminSession>(sp => sp.GetRequiredService<AdminSession>());
        services.AddScoped<ICurrentAdmin, HttpCurrentAdmin>();
        services.AddSingleton<IAdminTokenService, AdminTokenService>();
        var backups = builder.Configuration.GetSection(BackupSettings.Section).Get<BackupSettings>() ?? new BackupSettings();
        services.AddSingleton(backups);
        services.AddScoped<IBackupStore, BackupStore>();
        // Copie des sauvegardes hors du serveur (S3 / R2) si Backups:Offsite:Bucket est défini
        if (backups.Offsite.Enabled)
        {
            services.AddSingleton<IOffsiteBackupStore>(_ => new S3OffsiteBackupStore(backups.Offsite));
        }
        else
        {
            services.AddSingleton<IOffsiteBackupStore, DisabledOffsiteStore>();
        }

        services.AddSingleton<IBackupMirror, BackupMirror>();
        services.AddSingleton<BackupScheduleState>();
        services.AddHostedService<BackupMirrorWorker>();
        services.AddHostedService<AutomaticBackupWorker>();

        var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication().AddJwtBearer(AdminPolicies.Scheme, o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.AdminAudience,
                IssuerSigningKey = jwt.Key(),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "name",
                RoleClaimType = AdminTokenService.RoleClaim
            };
            // Compte désactivé ou rôle changé : pris en compte immédiatement, sans attendre l'expiration du jeton.
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    var id = Guid.TryParse(ctx.Principal?.FindFirstValue("sub"), out var g) ? g : Guid.Empty;
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<AdministrationDbContext>();
                    var admin = await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
                    if (admin is not { IsActive: true })
                    {
                        ctx.Fail("Compte administrateur désactivé.");
                        return;
                    }

                    var role = AdminTokenService.RoleClaim;
                    var identity = new ClaimsIdentity(ctx.Principal!.Claims.Where(c => c.Type != role), AdminPolicies.Scheme, "name", role);
                    identity.AddClaim(new Claim(role, admin.Role.ToString()));
                    ctx.Principal = new ClaimsPrincipal(identity);
                }
            };
        });

        services.AddAuthorization(o =>
        {
            foreach (var permission in Enum.GetValues<Permission>())
            {
                o.AddPolicy(AdminPolicies.Policy(permission), p => p
                    .AddAuthenticationSchemes(AdminPolicies.Scheme)
                    .RequireAuthenticatedUser()
                    .RequireRole(RolePermissions.RolesWith(permission).Select(r => r.ToString())));
            }
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var isDevelopment = app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment();
        var admin = app.MapGroup("/api/admin").WithTags("Administration");

        admin.MapPost("/auth/login", (AdminLoginCommand c, ISender s, CancellationToken ct) => s.Send(c, ct)).AllowAnonymous()
            .RequireRateLimiting(RateLimits.Auth);
        admin.MapGet("/auth/me", (ISender s, CancellationToken ct) => s.Send(new GetAdminMeQuery(), ct)).RequirePermission(Permission.DashboardView);
        admin.MapPost("/auth/password", (ChangeAdminPasswordCommand c, ISender s, CancellationToken ct) => s.Send(c, ct))
            .RequirePermission(Permission.DashboardView);

        admin.MapGet("/stats", (ISender s, CancellationToken ct) => s.Send(new GetAdminStatsQuery(), ct)).RequirePermission(Permission.DashboardView);

        admin.MapGet("/exports/{kind}", async (string kind, ISender s, CancellationToken ct) =>
        {
            var file = await s.Send(new ExportCsvQuery(kind), ct);
            return Results.File(Encoding.UTF8.GetBytes(file.Content), "text/csv; charset=utf-8", file.FileName);
        }).RequirePermission(Permission.ExportsDownload);

        admin.MapGet("/admins", (ISender s, CancellationToken ct) => s.Send(new GetAdminAccountsQuery(), ct)).RequirePermission(Permission.AdminsManage);
        admin.MapPost("/admins", async (CreateAdminCommand c, ISender s, CancellationToken ct) =>
            Results.Created("/api/admin/admins", await s.Send(c, ct))).RequirePermission(Permission.AdminsManage);
        admin.MapPut("/admins/{id:guid}", (Guid id, AdminUpdateRequest r, ISender s, CancellationToken ct) =>
            s.Send(new UpdateAdminCommand(id, r.DisplayName, r.Role, r.IsActive), ct)).RequirePermission(Permission.AdminsManage);
        admin.MapPost("/admins/{id:guid}/password", (Guid id, PasswordRequest r, ISender s, CancellationToken ct) =>
            s.Send(new ResetAdminPasswordCommand(id, r.Password), ct)).RequirePermission(Permission.AdminsManage);

        // Sauvegardes de la base : liste, création immédiate, téléchargement, suppression des manuelles
        var backups = admin.MapGroup("/backups").RequirePermission(Permission.BackupsManage);
        backups.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetBackupsQuery(), ct));
        backups.MapPost("", (ISender s, CancellationToken ct) => s.Send(new CreateBackupCommand(), ct));
        backups.MapGet("/{kind}/{name}", async (string kind, string name, ISender s, CancellationToken ct) =>
        {
            var file = await s.Send(new DownloadBackupQuery(kind, name), ct);
            return Results.File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
        });
        backups.MapPost("/offsite/sync", (ISender s, CancellationToken ct) => s.Send(new SyncOffsiteCommand(), ct));
        backups.MapGet("/offsite/{kind}/{name}", async (string kind, string name, ISender s, CancellationToken ct) =>
        {
            var file = await s.Send(new DownloadOffsiteQuery(kind, name), ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });
        backups.MapDelete("/Manual/{name}", async (string name, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteBackupCommand(name), ct);
            return Results.NoContent();
        });

        admin.MapGet("/audit", (string? search, int? skip, int? take, Guid? targetId, ISender s, CancellationToken ct) =>
            s.Send(new GetAuditQuery(search, skip ?? 0, take ?? 50, targetId), ct)).RequirePermission(Permission.AuditView);

        admin.MapGet("/system", async (AdministrationDbContext db, ISender s, IMediaModule media, IWebHostEnvironment env, CancellationToken ct) =>
        {
            var online = await db.Database.CanConnectAsync(ct);
            var usage = await media.UsageAsync(ct);
            var uptime = DateTimeOffset.UtcNow - StartedAt;
            return new SystemInfoDto(
                Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0",
                env.EnvironmentName,
                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                StartedAt,
                $"{(int)uptime.TotalDays} j {uptime.Hours} h {uptime.Minutes} min",
                online,
                $"PostgreSQL · {db.Database.GetDbConnection().Database}",
                usage.Files,
                usage.Bytes,
                online ? await s.Send(new GetTableCountsQuery(), ct) : [],
                isDevelopment);
        }).RequirePermission(Permission.SystemView);

        if (isDevelopment)
        {
            admin.MapPost("/system/reset-demo", async (IDemoDataReset demo, CancellationToken ct) =>
            {
                await demo.ResetAsync(ct);
                return Results.NoContent();
            }).RequirePermission(Permission.SystemReset);
        }
    }
}
