using System.Text.Encodings.Web;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Feed.Application;
using BeninConnect.Modules.Feed.Domain;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Feed.Api;

/// <summary>Liens publics, magasins d'applications et liens universels (ouverture directe dans l'app).</summary>
public sealed class ShareSettings
{
    public const string Section = "Share";

    public string SiteUrl { get; set; } = "https://monbenin.com";
    public string ApiUrl { get; set; } = "https://api.monbenin.com";
    public string AppStoreUrl { get; set; } = "https://apps.apple.com/fr/app/b%C3%A9ninconnect/id6832112483";
    public string PlayStoreUrl { get; set; } = "https://play.google.com/store/apps/details?id=com.hbatech.beninconnect.rn";
    public string AppleAppId { get; set; } = "6832112483";
    public string AppScheme { get; set; } = "beninconnect";
}

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
                var author = post is null ? null : (await identity.GetUsersAsync([post.AuthorId], ct)).GetValueOrDefault(post.AuthorId);
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
                var user = (await identity.GetUsersAsync([id], ct)).GetValueOrDefault(id);
                var site = share.SiteUrl.TrimEnd('/');
                if (user is null)
                {
                    return Results.Content(Layout(share, site, "Membre introuvable · BéninConnect", "Ce profil n'existe plus.", $"{site}/img/og.jpg",
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
                return Results.Content(Layout(share, $"{site}/user/{id}", $"{name} sur BéninConnect",
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

        return Layout(s, url, title, description, image, deepLink, card, p?.Image is not null);
    }

    /// <summary>Gabarit commun : aperçu Open Graph, carte, bouton d'ouverture dans l'app et liens des stores.</summary>
    private static string Layout(ShareSettings s, string url, string title, string description, string image, string deepLink, string card, bool largeImage)
    {
        var e = HtmlEncoder.Default;
        var site = s.SiteUrl.TrimEnd('/');
        return $$"""
            <!doctype html>
            <html lang="fr">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{e.Encode(title)}}</title>
            <meta name="description" content="{{e.Encode(description)}}">
            <meta name="theme-color" content="#087F5B">
            <meta name="apple-itunes-app" content="app-id={{e.Encode(s.AppleAppId)}}, app-argument={{e.Encode(url)}}">
            <meta property="og:site_name" content="BéninConnect">
            <meta property="og:type" content="article">
            <meta property="og:url" content="{{e.Encode(url)}}">
            <meta property="og:title" content="{{e.Encode(title)}}">
            <meta property="og:description" content="{{e.Encode(description)}}">
            <meta property="og:image" content="{{e.Encode(image)}}">
            <meta name="twitter:card" content="{{(largeImage ? "summary_large_image" : "summary")}}">
            <link rel="canonical" href="{{e.Encode(url)}}">
            <link rel="icon" href="{{site}}/img/icon-192.png">
            <style>
            *{box-sizing:border-box}body{margin:0;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;background:#f3f7f5;color:#1b2421}
            .top{background:linear-gradient(135deg,#0b9e71,#087f5b 55%,#043d2c);color:#fff;padding:18px 20px 64px}
            .brand{display:flex;align-items:center;gap:10px;color:#fff;text-decoration:none;font-weight:700;font-size:18px;max-width:640px;margin:auto}
            .brand img{width:34px;height:34px;border-radius:9px}
            main{max-width:640px;margin:-44px auto 40px;padding:0 16px}
            .card{background:#fff;border-radius:18px;padding:20px;box-shadow:0 20px 40px -24px rgba(4,61,44,.45)}
            .card header{display:flex;gap:12px;align-items:center;margin-bottom:14px}.card header div{display:flex;flex-direction:column}
            .card header span{color:#5b6b66;font-size:14px}
            .avatar{width:48px;height:48px;border-radius:50%;object-fit:cover;background:#087f5b;color:#fff;display:grid;place-items:center;font-weight:700}
            .content{font-size:17px;line-height:1.55;margin:0 0 14px;overflow-wrap:anywhere}
            .photo{width:100%;border-radius:12px;display:block}.video{color:#087f5b;font-weight:600}
            .empty h1{font-size:22px;margin:0 0 8px}
            .profile{text-align:center}.profile h1{margin:12px 0 4px;font-size:24px}.profile p{margin:4px 0;color:#5b6b66}
            .avatar.big{width:96px;height:96px;font-size:32px;margin:0 auto;display:grid}.muted{font-size:14px}.empty p{color:#5b6b66;line-height:1.5}
            .actions{margin-top:20px;display:grid;gap:10px}
            .open{display:block;text-align:center;background:#087f5b;color:#fff;text-decoration:none;font-weight:700;padding:15px;border-radius:12px;font-size:16px}
            .stores{display:grid;grid-template-columns:1fr 1fr;gap:10px}
            .store{display:flex;align-items:center;justify-content:center;gap:8px;background:#111;color:#fff;text-decoration:none;padding:11px;border-radius:12px;font-size:14px;font-weight:600}
            .store svg{width:20px;height:20px;fill:#fff}
            .note{text-align:center;color:#5b6b66;font-size:14px;margin-top:18px}.note a{color:#087f5b}
            </style>
            </head>
            <body>
            <div class="top"><a class="brand" href="{{site}}/"><img src="{{site}}/img/icon-192.png" alt="">BéninConnect</a></div>
            <main>
            {{card}}
            <div class="actions">
              <a class="open" href="{{e.Encode(deepLink)}}">Ouvrir dans l'application</a>
              <div class="stores">
                <a class="store" href="{{e.Encode(s.AppStoreUrl)}}"><svg viewBox="0 0 24 24"><path d="M16.37 12.6c-.02-2.18 1.78-3.23 1.86-3.28-1.01-1.48-2.59-1.69-3.15-1.71-1.34-.14-2.62.79-3.3.79-.68 0-1.73-.77-2.84-.75-1.46.02-2.81.85-3.56 2.16-1.52 2.63-.39 6.52 1.09 8.66.72 1.04 1.58 2.22 2.71 2.17 1.09-.04 1.5-.7 2.82-.7 1.31 0 1.69.7 2.84.68 1.17-.02 1.92-1.06 2.63-2.11.83-1.21 1.17-2.38 1.19-2.44-.03-.01-2.28-.87-2.29-3.47zM14.2 6.19c.6-.73 1-1.74.89-2.75-.86.04-1.9.57-2.52 1.3-.55.64-1.04 1.67-.91 2.66.96.07 1.94-.49 2.54-1.21z"/></svg>App Store</a>
                <a class="store" href="{{e.Encode(s.PlayStoreUrl)}}"><svg viewBox="0 0 24 24"><path d="M3.6 2.3c-.2.2-.3.5-.3.9v17.6c0 .4.1.7.3.9l.1.1 9.9-9.9v-.2L3.7 2.2l-.1.1zm13.3 13.1-3.3-3.3v-.2l3.3-3.3.1.1 3.9 2.2c1.1.6 1.1 1.7 0 2.3l-3.9 2.2h-.1zm.1-.1-3.4-3.4-10 10c.4.4 1 .4 1.7.1l11.7-6.7m0-6.6L5.3 2c-.7-.4-1.3-.3-1.7.1l10 10 3.4-3.4z"/></svg>Google Play</a>
              </div>
            </div>
            <p class="note">Rejoignez la communauté de la diaspora béninoise sur <a href="{{site}}/">monbenin.com</a></p>
            </main>
            </body>
            </html>
            """;
    }
}
