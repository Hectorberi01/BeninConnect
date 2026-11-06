using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Communities.Application;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Communities.Infrastructure;
using BeninConnect.Modules.Communities.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Communities.Api;

public sealed class CommunitiesModule : IModule
{
    public string Name => "Communities";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<CommunitiesDbContext>(builder.Configuration, Name, CommunitiesDbContext.SchemaName);
        services.AddScoped<ICommunitiesDb>(sp => sp.GetRequiredService<CommunitiesDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Communities");
        services.AddScoped<CommunitiesModuleService>();
        services.AddScoped<ICommunitiesModule>(sp => sp.GetRequiredService<CommunitiesModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<CommunitiesModuleService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var communities = app.MapGroup("/api/communities").WithTags("Communautés").RequireAuthorization();
        communities.MapGet("", (string? search, ISender s, CancellationToken ct) => s.Send(new GetCommunitiesQuery(search), ct));
        communities.MapGet("/categories", (ISender s, CancellationToken ct) => s.Send(new GetCommunityCategoriesQuery(), ct));
        communities.MapGet("/join-requests/mine", (ISender s, CancellationToken ct) => s.Send(new GetMyJoinRequestsQuery(), ct));
        communities.MapPost("/batch", (IdsRequest r, ISender s, CancellationToken ct) => s.Send(new GetCommunitiesByIdsQuery(r.Ids), ct));
        communities.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetCommunityQuery(id), ct));
        communities.MapGet("/{id:guid}/members", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetCommunityMembersQuery(id), ct));
        communities.MapPost("", async (CreateCommunityCommand c, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(c, ct);
            return Results.Created($"/api/communities/{created.Id}", created);
        });
        communities.MapPost("/{id:guid}/join", (Guid id, ISender s, CancellationToken ct) => s.Send(new JoinCommunityCommand(id), ct));
        communities.MapDelete("/{id:guid}/membership", (Guid id, ISender s, CancellationToken ct) => s.Send(new LeaveCommunityCommand(id), ct));
        communities.MapDelete("/{id:guid}/join-request", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelJoinRequestCommand(id), ct));

        app.MapGet("/api/users/{id:guid}/communities", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetMemberCommunitiesQuery(id), ct))
            .WithTags("Communautés").RequireAuthorization();

        var admin = app.MapGroup("/api/admin/communities").WithTags("Administration");
        admin.MapGet("", (string? search, ISender s, CancellationToken ct) => s.Send(new GetAdminCommunitiesQuery(search), ct))
            .RequirePermission(Permission.CommunitiesManage);
        admin.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteCommunityCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permission.CommunitiesManage);
    }
}
