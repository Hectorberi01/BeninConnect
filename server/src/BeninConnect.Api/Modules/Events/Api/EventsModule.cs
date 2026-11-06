using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Events.Application;
using BeninConnect.Modules.Events.Contracts;
using BeninConnect.Modules.Events.Domain;
using BeninConnect.Modules.Events.Infrastructure;
using BeninConnect.Modules.Events.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Events.Api;

public sealed class EventsModule : IModule
{
    public string Name => "Events";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<EventsDbContext>(builder.Configuration, Name, EventsDbContext.SchemaName);
        services.AddScoped<IEventsDb>(sp => sp.GetRequiredService<EventsDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Events");
        services.AddScoped<EventsModuleService>();
        services.AddScoped<IEventsModule>(sp => sp.GetRequiredService<EventsModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<EventsModuleService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var events = app.MapGroup("/api/events").WithTags("Événements").RequireAuthorization();
        events.MapGet("", (string? search, string? category, string? city, EventPeriod? period, ISender s, CancellationToken ct) =>
            s.Send(new GetEventsQuery(search, category, city, period ?? EventPeriod.All), ct));
        events.MapGet("/categories", (ISender s, CancellationToken ct) => s.Send(new GetEventCategoriesQuery(), ct));
        events.MapGet("/cities", (ISender s, CancellationToken ct) => s.Send(new GetEventCitiesQuery(), ct));
        events.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetEventQuery(id), ct));
        events.MapPost("", async (CreateEventCommand c, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(c, ct);
            return Results.Created($"/api/events/{created.Id}", created);
        });
        events.MapPost("/{id:guid}/participation", (Guid id, ISender s, CancellationToken ct) => s.Send(new ToggleParticipationCommand(id), ct));

        app.MapGet("/api/communities/{id:guid}/events", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetCommunityEventsQuery(id), ct))
            .WithTags("Événements").RequireAuthorization();
        app.MapGet("/api/users/{id:guid}/events", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetParticipantEventsQuery(id), ct))
            .WithTags("Événements").RequireAuthorization();

        var admin = app.MapGroup("/api/admin/events").WithTags("Administration");
        admin.MapGet("", (string? search, bool? includePast, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminEventsQuery(search, includePast ?? false), ct)).RequirePermission(Permission.EventsManage);
        admin.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteEventCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permission.EventsManage);
    }
}
