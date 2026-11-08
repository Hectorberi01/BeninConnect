using System.Security.Claims;
using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Identity.Infrastructure;
using BeninConnect.Modules.Identity.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BeninConnect.Modules.Identity.Api;

public sealed record EmailRequest(string Email);

/// <summary>Membres : inscription, connexion, profil, jeton membre (schéma d'authentification par défaut).</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;
        services.AddModuleDbContext<IdentityDbContext>(config, Name, IdentityDbContext.SchemaName);
        services.AddScoped<IIdentityDb>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Identity");
        services.AddScoped<IdentityModuleService>();
        services.AddScoped<IIdentityModule>(sp => sp.GetRequiredService<IdentityModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<IdentityModuleService>());

        services.AddSingleton(config.GetSection("Auth").Get<AuthSettings>() ?? new AuthSettings());
        var email = config.GetSection(EmailSettings.Section).Get<EmailSettings>() ?? new EmailSettings();
        services.AddSingleton(email);
        if (email.IsResend)
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>(c =>
            {
                c.BaseAddress = new Uri(ResendEmailSender.BaseAddress);
                c.Timeout = TimeSpan.FromSeconds(10);
            });
        }
        else if (email.IsSmtp)
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LogEmailSender>();
        }

        services.AddSingleton<IMemberTokenService, MemberTokenService>();

        var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = jwt.Key(),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            // Membre suspendu par la modération : ses jetons en cours sont refusés immédiatement.
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    var sub = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Principal?.FindFirstValue("sub");
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<IdentityDbContext>();
                    if (Guid.TryParse(sub, out var id) && await db.Users.AnyAsync(u => u.Id == id && u.IsSuspended))
                    {
                        ctx.Fail("Compte suspendu.");
                    }
                }
            };
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentification").RequireRateLimiting(RateLimits.Auth);
        auth.MapPost("/login", (LoginCommand c, ISender s, CancellationToken ct) => s.Send(c, ct)).AllowAnonymous();
        auth.MapPost("/register", async (RegisterCommand c, ISender s, CancellationToken ct) =>
            Results.Created("/api/auth/me", await s.Send(c, ct))).AllowAnonymous();
        auth.MapPost("/verify-email", (VerifyEmailCommand c, ISender s, CancellationToken ct) => s.Send(c, ct)).AllowAnonymous();
        auth.MapPost("/resend-code", async (EmailRequest r, ISender s, CancellationToken ct) =>
        {
            await s.Send(new ResendVerificationCodeCommand(r.Email), ct);
            return Results.NoContent();
        }).AllowAnonymous();
        auth.MapPost("/forgot-password", async (EmailRequest r, ISender s, CancellationToken ct) =>
        {
            await s.Send(new ForgotPasswordCommand(r.Email), ct);
            return Results.NoContent();
        }).AllowAnonymous();
        auth.MapPost("/reset-password", async (ResetPasswordCommand c, ISender s, CancellationToken ct) =>
        {
            await s.Send(c, ct);
            return Results.NoContent();
        }).AllowAnonymous();
        auth.MapGet("/me", (ISender s, CancellationToken ct) => s.Send(new GetMeQuery(), ct)).RequireAuthorization();

        var users = app.MapGroup("/api/users").WithTags("Membres").RequireAuthorization();
        users.MapGet("", (string? search, ISender s, CancellationToken ct) => s.Send(new SearchUsersQuery(search), ct));
        users.MapGet("/interests", (ISender s, CancellationToken ct) => s.Send(new GetInterestsQuery(), ct));
        users.MapPost("/batch", (IdsRequest r, ISender s, CancellationToken ct) => s.Send(new GetUsersByIdsQuery(r.Ids), ct));
        users.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetUserQuery(id), ct));
        users.MapPut("/me", (UpdateProfileCommand c, ISender s, CancellationToken ct) => s.Send(c, ct));
        // POST : le mot de passe de confirmation voyage dans le corps
        users.MapPost("/me/delete", async (DeleteAccountCommand c, ISender s, CancellationToken ct) =>
        {
            await s.Send(c, ct);
            return Results.NoContent();
        }).RequireRateLimiting(RateLimits.Auth);

        var admin = app.MapGroup("/api/admin/members").WithTags("Administration");
        admin.MapGet("", (string? search, string? status, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminMembersQuery(search, status, skip ?? 0, take ?? 25), ct)).RequirePermission(Permission.MembersView);
        admin.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAdminMemberQuery(id), ct))
            .RequirePermission(Permission.MembersView);
        admin.MapPost("/{id:guid}/suspend", (Guid id, ReasonRequest r, ISender s, CancellationToken ct) =>
            s.Send(new SuspendMemberCommand(id, r.Reason ?? string.Empty), ct)).RequirePermission(Permission.MembersSuspend);
        admin.MapPost("/{id:guid}/reactivate", (Guid id, ISender s, CancellationToken ct) => s.Send(new ReactivateMemberCommand(id), ct))
            .RequirePermission(Permission.MembersSuspend);
    }
}
