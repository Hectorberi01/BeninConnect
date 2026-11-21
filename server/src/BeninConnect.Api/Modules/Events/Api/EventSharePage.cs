using System.Globalization;
using System.Text.Encodings.Web;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Events.Application;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Events.Api;

/// <summary>Page publique d'un événement partagé (https://monbenin.com/events/{id}) ; pas ceux des communautés privées.</summary>
public static class EventSharePage
{
    private static readonly CultureInfo French = new("fr-FR");

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/events/{id:guid}", async (Guid id, IEventsDb db, ICommunitiesModule communities, IMediaModule media, ShareSettings share,
                CancellationToken ct) =>
            {
                var e = HtmlEncoder.Default;
                var site = share.SiteUrl.TrimEnd('/');
                var ev = await db.Events.AsNoTracking().Include(x => x.Participants).FirstOrDefaultAsync(x => x.Id == id, ct);
                var community = ev?.CommunityId is { } cid ? (await communities.GetInfosAsync([cid], ct)).GetValueOrDefault(cid) : null;
                if (ev is null || community?.IsPrivate == true)
                {
                    return Results.Content(ShareLayout.Page(share, site, "Événement introuvable · BéninConnect",
                        "Cet événement n'existe plus ou est réservé aux membres d'une communauté.", $"{site}/img/og.jpg", $"{share.AppScheme}://",
                        """<div class="card empty"><h1>Événement introuvable</h1><p>Il a été supprimé ou n'est visible que par les membres d'une communauté. Ouvrez BéninConnect pour le retrouver.</p></div>""",
                        false), "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound);
                }

                var image = ev.ImagePath is { } path && path.StartsWith("uploads/", StringComparison.Ordinal)
                    ? media.PublicUrl(path) ?? $"{share.ApiUrl.TrimEnd('/')}/{path}"
                    : null;
                var paris = TimeZoneInfo.ConvertTime(ev.StartsAt, Paris());
                var when = paris.ToString("dddd d MMMM yyyy 'à' HH'h'mm", French);
                when = char.ToUpper(when[0], French) + when[1..];
                var place = string.Join(", ", new[] { ev.City, ev.Country }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var excerpt = ev.Description.Length > 180 ? $"{ev.Description[..180].TrimEnd()}…" : ev.Description;
                var card = $"""
                    <article class="card">
                      {(image is null ? "" : $"<img class=\"cover\" src=\"{e.Encode(image)}\" alt=\"\">")}
                      <span class="tag">{e.Encode(ev.Category)}</span>
                      <h1>{e.Encode(ev.Title)}</h1>
                      <p class="meta">📅 <b>{e.Encode(when)}</b></p>
                      <p class="meta">📍 {e.Encode(place)}</p>
                      {(community is null ? "" : $"<p class=\"meta\">👥 {e.Encode(community.Name)}</p>")}
                      <p class="meta">✋ {ev.Participants.Count} participant{(ev.Participants.Count > 1 ? "s" : "")}</p>
                      <p class="content">{e.Encode(ev.Description).Replace("&#xA;", "<br>")}</p>
                    </article>
                    """;
                return Results.Content(ShareLayout.Page(share, $"{site}/events/{id}", $"{ev.Title} · BéninConnect",
                    $"{when} · {place}. {excerpt}", image ?? $"{site}/img/og.jpg", $"{share.AppScheme}://events/{id}", card, image is not null),
                    "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

    private static TimeZoneInfo Paris()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
