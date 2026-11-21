using System.Text.Encodings.Web;
using BeninConnect.Modules.Communities.Application;
using BeninConnect.Modules.Communities.Domain;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Communities.Api;

/// <summary>Page publique d'une communauté partagée (https://monbenin.com/community/{id}) : présentation seulement, jamais le contenu.</summary>
public static class CommunitySharePage
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/community/{id:guid}", async (Guid id, ICommunitiesDb db, IMediaModule media, ShareSettings share, CancellationToken ct) =>
            {
                var e = HtmlEncoder.Default;
                var site = share.SiteUrl.TrimEnd('/');
                var community = await db.Communities.AsNoTracking().Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id, ct);
                if (community is null)
                {
                    return Results.Content(ShareLayout.Page(share, site, "Communauté introuvable · BéninConnect", "Cette communauté n'existe plus.",
                        $"{site}/img/og.jpg", $"{share.AppScheme}://",
                        """<div class="card empty"><h1>Communauté introuvable</h1><p>Elle a été supprimée. Découvrez les autres communautés dans BéninConnect.</p></div>""",
                        false), "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound);
                }

                var image = community.CoverImage is { } path && path.StartsWith("uploads/", StringComparison.Ordinal)
                    ? media.PublicUrl(path) ?? $"{share.ApiUrl.TrimEnd('/')}/{path}"
                    : null;
                var members = community.Members.Count;
                var place = string.Join(", ", new[] { community.City, community.Country }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var privacy = community.Visibility == CommunityVisibility.Private
                    ? "🔒 Communauté privée : les membres sont acceptés par les administrateurs"
                    : "🌍 Communauté publique : ouverte à tous";
                var excerpt = community.Description.Length > 180 ? $"{community.Description[..180].TrimEnd()}…" : community.Description;
                var card = $"""
                    <article class="card">
                      {(image is null ? "" : $"<img class=\"cover\" src=\"{e.Encode(image)}\" alt=\"\">")}
                      <span class="tag">{e.Encode(community.Category)}</span>
                      <h1>{e.Encode(community.Name)}</h1>
                      {(place.Length == 0 ? "" : $"<p class=\"meta\">📍 {e.Encode(place)}</p>")}
                      <p class="meta">👥 <b>{members} membre{(members > 1 ? "s" : "")}</b></p>
                      <p class="meta">{privacy}</p>
                      <p class="content">{e.Encode(community.Description).Replace("&#xA;", "<br>")}</p>
                    </article>
                    """;
                return Results.Content(ShareLayout.Page(share, $"{site}/community/{id}", $"{community.Name} · BéninConnect",
                    $"{members} membre{(members > 1 ? "s" : "")} · {excerpt}", image ?? $"{site}/img/og.jpg", $"{share.AppScheme}://community/{id}",
                    card, image is not null), "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
}
