using System.Text.Encodings.Web;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Feed.Application;
using BeninConnect.Modules.Feed.Domain;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using BeninConnect.Shared.Api;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Feed.Api;

/// <summary>
/// Page d'une publication partagée (https://monbenin.com/post/{id}, relayée par Caddy) :
/// aperçu Open Graph pour WhatsApp, Facebook, X…, bouton d'ouverture dans l'app et liens de téléchargement.
/// Seules les publications publiques et visibles sont affichées.
/// </summary>
public static class SharePage
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/post/{id:guid}", async (Guid id, IFeedDb db, IIdentityModule identity, ICommunitiesModule communities, IMediaModule media,
                ShareSettings share, CancellationToken ct) =>
            {
                var post = await db.Posts.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsHidden && p.Visibility == PostVisibility.Public, ct);
                var author = post is null ? null : (await identity.GetUsersForViewerAsync(null, [post.AuthorId], ct)).GetValueOrDefault(post.AuthorId);
                if (post is null || author is null)
                {
                    return Results.Content(Render(share, null), "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound);
                }

                var community = post.CommunityId is { } cid ? (await communities.GetInfosAsync([cid], ct)).GetValueOrDefault(cid)?.Name : null;
                // Photos envoyées seulement (uploads/…) : URL du bucket, sinon relayée par l'API
                string? Image(string? path) => path is null || !path.StartsWith("uploads/", StringComparison.Ordinal)
                    ? null
                    : media.PublicUrl(path) ?? $"{share.ApiUrl.TrimEnd('/')}/{path}";
                var view = new SharedPost(id, $"{author.FirstName} {author.LastName}", author.City, post.Content, Image(post.ImagePath),
                    Image(author.AvatarImage), post.VideoPath is not null, community, post.CreatedAt);
                return Results.Content(Render(share, view), "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Profil (QR code) : prénom, initiale du nom, ville et photo seulement
        app.MapGet("/user/{id:guid}", async (Guid id, IIdentityModule identity, IMediaModule media, ShareSettings share, CancellationToken ct) =>
            {
                var user = (await identity.GetUsersForViewerAsync(null, [id], ct)).GetValueOrDefault(id);
                var site = share.SiteUrl.TrimEnd('/');
                if (user is null)
                {
                    return Results.Content(ShareLayout.Page(share, site, "Membre introuvable · BéninConnect", "Ce profil n'existe plus.", $"{site}/img/og.jpg",
                        $"{share.AppScheme}://", """<div class="card empty"><h1>Membre introuvable</h1><p>Ce profil a été supprimé.</p></div>""", false),
                        "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound);
                }

                var e = HtmlEncoder.Default;
                var name = $"{user.FirstName} {(user.LastName.Length > 0 ? user.LastName[0] + "." : "")}".Trim();
                var avatar = user.AvatarImage is { } path && path.StartsWith("uploads/", StringComparison.Ordinal)
                    ? media.PublicUrl(path) ?? $"{share.ApiUrl.TrimEnd('/')}/{path}"
                    : null;
                var initials = $"{user.FirstName.FirstOrDefault()}{user.LastName.FirstOrDefault()}";
                var card = $"""
                    <div class="card profile">
                      {(avatar is null ? $"<span class=\"avatar big\" style=\"background:{e.Encode(user.AvatarColor)}\">{e.Encode(initials)}</span>" : $"<img class=\"avatar big\" src=\"{e.Encode(avatar)}\" alt=\"\">")}
                      <h1>{e.Encode(name)}</h1>
                      <p>{e.Encode(user.City)}{(user.Country.Length > 0 ? $", {e.Encode(user.Country)}" : "")}</p>
                      <p class="muted">Membre de BéninConnect</p>
                    </div>
                    """;
                return Results.Content(ShareLayout.Page(share, $"{site}/user/{id}", $"{name} sur BéninConnect",
                    $"Retrouvez {user.FirstName} sur BéninConnect, la communauté de la diaspora béninoise.", avatar ?? $"{site}/img/og.jpg",
                    $"{share.AppScheme}://user/{id}", card, false), "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private sealed record SharedPost(
        Guid Id, string Author, string City, string Content, string? Image, string? Avatar, bool HasVideo, string? Community, DateTimeOffset CreatedAt);

    private static string Render(ShareSettings s, SharedPost? p)
    {
        var e = HtmlEncoder.Default;
        var site = s.SiteUrl.TrimEnd('/');
        var url = p is null ? site : $"{site}/post/{p.Id}";
        var excerpt = p is null ? string.Empty : p.Content.Length > 180 ? $"{p.Content[..180].TrimEnd()}…" : p.Content;
        var title = p is null ? "Publication introuvable · BéninConnect" : $"{p.Author} sur BéninConnect";
        var description = p is null
            ? "Cette publication n'existe plus ou n'est visible que dans l'application."
            : excerpt.Length > 0 ? excerpt : "Une publication de la communauté BéninConnect.";
        var image = p?.Image ?? $"{site}/img/og.jpg";
        var deepLink = p is null ? $"{s.AppScheme}://" : $"{s.AppScheme}://post/{p.Id}";
        var date = p?.CreatedAt.ToOffset(TimeSpan.FromHours(1)).ToString("d MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
        var initials = p is null ? "" : string.Concat(p.Author.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => w[0]));

        var card = p is null
            ? """<div class="card empty"><h1>Publication introuvable</h1><p>Elle a été supprimée ou n'est visible que par les membres d'une communauté. Ouvrez BéninConnect pour la retrouver.</p></div>"""
            : $"""
              <article class="card">
                <header>
                  {(p.Avatar is null ? $"<span class=\"avatar\">{e.Encode(initials)}</span>" : $"<img class=\"avatar\" src=\"{e.Encode(p.Avatar)}\" alt=\"\">")}
                  <div><strong>{e.Encode(p.Author)}</strong><span>{e.Encode(p.City)} · {e.Encode(date ?? "")}{(p.Community is null ? "" : $" · {e.Encode(p.Community)}")}</span></div>
                </header>
                <p class="content">{e.Encode(p.Content).Replace("&#xA;", "<br>")}</p>
                {(p.Image is null ? "" : $"<img class=\"photo\" src=\"{e.Encode(p.Image)}\" alt=\"Photo de la publication\">")}
                {(p.HasVideo ? "<p class=\"video\">▶ Vidéo à regarder dans l'application</p>" : "")}
              </article>
              """;

        return ShareLayout.Page(s, url, title, description, image, deepLink, card, p?.Image is not null);
    }
}
