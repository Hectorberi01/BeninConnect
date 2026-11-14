using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Voyages.Application;

public sealed record GetRouteAlertsQuery : IQuery<IReadOnlyList<RouteAlertDto>>;

public sealed record CreateRouteAlertCommand(string FromCountry, string? FromCity, string ToCountry, string? ToCity, DateOnly? After, DateOnly? Before)
    : ICommand<RouteAlertDto>;

public sealed record DeleteRouteAlertCommand(Guid Id) : ICommand<bool>;

/// <summary>Alertes de trajet : au plus 5 par membre.</summary>
public sealed class AlertHandlers(IVoyagesDb db, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<GetRouteAlertsQuery, IReadOnlyList<RouteAlertDto>>,
    IRequestHandler<CreateRouteAlertCommand, RouteAlertDto>,
    IRequestHandler<DeleteRouteAlertCommand, bool>
{
    public async Task<IReadOnlyList<RouteAlertDto>> Handle(GetRouteAlertsQuery q, CancellationToken ct) =>
        (await db.Alerts.AsNoTracking().Where(a => a.UserId == me.Id).OrderBy(a => a.CreatedAt).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<RouteAlertDto> Handle(CreateRouteAlertCommand c, CancellationToken ct)
    {
        if (await db.Alerts.CountAsync(a => a.UserId == me.Id, ct) >= VoyageRules.MaxAlertsPerMember)
        {
            throw new DomainException($"{VoyageRules.MaxAlertsPerMember} alertes au plus : supprimez-en une.");
        }

        var alert = RouteAlert.Create(me.Id, c.FromCountry, c.FromCity, c.ToCountry, c.ToCity, c.After, c.Before, clock.GetUtcNow());
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(ct);
        return ToDto(alert);
    }

    public async Task<bool> Handle(DeleteRouteAlertCommand c, CancellationToken ct)
    {
        var deleted = await db.Alerts.Where(a => a.Id == c.Id && a.UserId == me.Id).ExecuteDeleteAsync(ct);
        return deleted > 0 ? true : throw new NotFoundException("Alerte");
    }

    private static RouteAlertDto ToDto(RouteAlert a) => new(a.Id, a.FromCountry, a.FromCity, a.ToCountry, a.ToCity, a.After, a.Before, a.CreatedAt);
}
