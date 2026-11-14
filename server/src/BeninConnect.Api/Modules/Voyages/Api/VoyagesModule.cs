using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Voyages.Application;
using BeninConnect.Modules.Voyages.Contracts;
using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Modules.Voyages.Infrastructure;
using BeninConnect.Modules.Voyages.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Voyages.Api;

public sealed record BookingBody(decimal WeightKg, ParcelCategory Category, string Description, bool Certified);

public sealed record DeliverBody(string Code);

public sealed record VoyageReviewBody(int Rating, string? Comment);

public sealed record VoyageReportBody(VoyageReportTarget TargetType, Guid TargetId, string Reason);

public sealed record RouteAlertBody(string FromCountry, string? FromCity, string ToCountry, string? ToCity, DateOnly? After, DateOnly? Before);

public sealed record ResolveBody(DisputeOutcome Outcome, string Note);

public sealed record VoyageVisibilityBody(string? Reason);

/// <summary>« Qui part au pays ? » : des voyageurs proposent des kilos, des expéditeurs les réservent. Mise en relation seulement, aucun paiement.</summary>
public sealed class VoyagesModule : IModule
{
    public string Name => "Voyages";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<VoyagesDbContext>(builder.Configuration, Name, VoyagesDbContext.SchemaName);
        services.AddScoped<IVoyagesDb>(sp => sp.GetRequiredService<VoyagesDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Voyages");
        services.AddSingleton(builder.Configuration.GetSection(VoyagesOptions.Section).Get<VoyagesOptions>() ?? new VoyagesOptions());
        services.AddScoped<TripLifecycle>();
        services.AddHostedService<VoyagesWorker>();
        services.AddScoped<VoyagesModuleService>();
        services.AddScoped<IVoyagesModule>(sp => sp.GetRequiredService<VoyagesModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<VoyagesModuleService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var trips = app.MapGroup("/api/trips").WithTags("Voyages").RequireAuthorization();
        trips.MapGet("", (string? from, string? to, string? fromCity, string? toCity, DateOnly? after, DateOnly? before, ParcelCategory? category,
                decimal? minKg, string? cursor, int? take, ISender s, CancellationToken ct) =>
            s.Send(new SearchTripsQuery(from, to, fromCity, toCity, after, before, category, minKg, cursor, take ?? 20), ct));
        trips.MapGet("/info", (ISender s, CancellationToken ct) => s.Send(new GetVoyagesInfoQuery(), ct));
        trips.MapGet("/mine", (ISender s, CancellationToken ct) => s.Send(new GetMyTripsQuery(), ct));
        trips.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetTripQuery(id), ct));
        trips.MapPost("", (TripDetails r, ISender s, CancellationToken ct) => s.Send(new PublishTripCommand(r), ct));
        trips.MapPut("/{id:guid}", (Guid id, TripDetails r, ISender s, CancellationToken ct) => s.Send(new UpdateTripCommand(id, r), ct));
        trips.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelTripCommand(id), ct));
        trips.MapPost("/{id:guid}/bookings", (Guid id, BookingBody r, ISender s, CancellationToken ct) =>
            s.Send(new RequestBookingCommand(id, r.WeightKg, r.Category, r.Description, r.Certified), ct));
        trips.MapGet("/users/{id:guid}/stats", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetVoyageStatsQuery(id), ct));
        trips.MapGet("/users/{id:guid}/reviews", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetVoyageReviewsQuery(id), ct));
        trips.MapGet("/alerts", (ISender s, CancellationToken ct) => s.Send(new GetRouteAlertsQuery(), ct));
        trips.MapPost("/alerts", (RouteAlertBody r, ISender s, CancellationToken ct) =>
            s.Send(new CreateRouteAlertCommand(r.FromCountry, r.FromCity, r.ToCountry, r.ToCity, r.After, r.Before), ct));
        trips.MapDelete("/alerts/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteRouteAlertCommand(id), ct);
            return Results.NoContent();
        });
        trips.MapPost("/reports", (VoyageReportBody r, ISender s, CancellationToken ct) => s.Send(new ReportVoyageCommand(r.TargetType, r.TargetId, r.Reason), ct));

        var bookings = app.MapGroup("/api/bookings").WithTags("Voyages").RequireAuthorization();
        bookings.MapGet("/mine", (ISender s, CancellationToken ct) => s.Send(new GetMyBookingsQuery(), ct));
        bookings.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetBookingQuery(id), ct));
        bookings.MapPost("/{id:guid}/accept", (Guid id, ISender s, CancellationToken ct) => s.Send(new AcceptBookingCommand(id), ct));
        bookings.MapPost("/{id:guid}/decline", (Guid id, ReasonRequest r, ISender s, CancellationToken ct) => s.Send(new DeclineBookingCommand(id, r.Reason), ct));
        bookings.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelBookingCommand(id), ct));
        bookings.MapPost("/{id:guid}/code", (Guid id, ISender s, CancellationToken ct) => s.Send(new IssueDeliveryCodeCommand(id), ct));
        bookings.MapPost("/{id:guid}/deliver", (Guid id, DeliverBody r, ISender s, CancellationToken ct) => s.Send(new DeliverBookingCommand(id, r.Code), ct))
            .RequireRateLimiting(RateLimits.Auth); // essais de code limités aussi par IP
        bookings.MapPost("/{id:guid}/dispute", (Guid id, ReasonRequest r, ISender s, CancellationToken ct) =>
            s.Send(new DisputeBookingCommand(id, r.Reason ?? ""), ct));
        bookings.MapPost("/{id:guid}/review", (Guid id, VoyageReviewBody r, ISender s, CancellationToken ct) =>
            s.Send(new ReviewBookingCommand(id, r.Rating, r.Comment), ct));

        MapAdmin(app.MapGroup("/api/admin/trips").WithTags("Administration"));
    }

    private static void MapAdmin(RouteGroupBuilder admin)
    {
        const Permission p = Permission.ContentModerate;
        admin.MapGet("/overview", (ISender s, CancellationToken ct) => s.Send(new GetVoyagesOverviewQuery(), ct)).RequirePermission(p);
        admin.MapGet("", (AdminTripView? view, string? q, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminTripsQuery(view ?? AdminTripView.All, q, skip ?? 0, take ?? 25), ct)).RequirePermission(p);
        admin.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAdminTripQuery(id), ct)).RequirePermission(p);
        admin.MapPost("/{id:guid}/hide", (Guid id, VoyageVisibilityBody r, ISender s, CancellationToken ct) =>
            s.Send(new HideTripCommand(id, r.Reason ?? ""), ct)).RequirePermission(p);
        admin.MapPost("/{id:guid}/show", (Guid id, ISender s, CancellationToken ct) => s.Send(new ShowTripCommand(id), ct)).RequirePermission(p);
        admin.MapGet("/disputes", (bool? resolved, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetDisputesQuery(resolved ?? false, skip ?? 0, take ?? 25), ct)).RequirePermission(p);
        admin.MapPost("/bookings/{id:guid}/resolve", (Guid id, ResolveBody r, ISender s, CancellationToken ct) =>
            s.Send(new ResolveDisputeCommand(id, r.Outcome, r.Note), ct)).RequirePermission(p);
        admin.MapGet("/reports", (bool? handled, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminVoyageReportsQuery(handled ?? false, skip ?? 0, take ?? 25), ct)).RequirePermission(p);
        admin.MapPost("/reports/{id:guid}/close", (Guid id, VoyageVisibilityBody r, ISender s, CancellationToken ct) =>
            s.Send(new CloseVoyageReportCommand(id, r.Reason), ct)).RequirePermission(p);
        admin.MapPost("/reviews/{id:guid}/hide", (Guid id, VoyageVisibilityBody r, ISender s, CancellationToken ct) =>
            s.Send(new SetVoyageReviewVisibilityCommand(id, false, r.Reason), ct)).RequirePermission(p);
        admin.MapPost("/reviews/{id:guid}/show", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new SetVoyageReviewVisibilityCommand(id, true, null), ct)).RequirePermission(p);
    }
}
