using System.Globalization;
using System.Text;
using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Events.Contracts;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Administration.Application;

public sealed record CsvFile(string FileName, string Content);

/// <summary>Kind : members, communities, events, activity.</summary>
public sealed record ExportCsvQuery(string Kind) : IQuery<CsvFile>;

public sealed record GetTableCountsQuery : IQuery<IReadOnlyList<LabelCount>>;

/// <summary>Exports CSV (Comptable) : lignes fournies par les contrats des modules.</summary>
public sealed class AdminExportHandlers(
    IAdministrationDb db, IAdminSession admin, IEnumerable<IModuleStatistics> statistics, IIdentityModule identity, IFeedModule feed,
    ICommunitiesModule communities, IEventsModule events, IMessagingModule messaging, TimeProvider clock) :
    IRequestHandler<ExportCsvQuery, CsvFile>,
    IRequestHandler<GetTableCountsQuery, IReadOnlyList<LabelCount>>
{
    public async Task<CsvFile> Handle(ExportCsvQuery request, CancellationToken ct)
    {
        var rows = request.Kind switch
        {
            "members" => await MembersAsync(ct),
            "communities" => await CommunitiesAsync(ct),
            "events" => await EventsAsync(ct),
            "activity" => await ActivityAsync(ct),
            _ => throw new DomainException("Export inconnu.")
        };

        await admin.LogAsync("Export CSV", "Export", null, $"{request.Kind} ({rows.Count - 1} lignes)", ct);
        return new CsvFile($"beninconnect-{request.Kind}-{clock.GetUtcNow():yyyy-MM-dd}.csv", ToCsv(rows));
    }

    public async Task<IReadOnlyList<LabelCount>> Handle(GetTableCountsQuery request, CancellationToken ct)
    {
        var counts = new List<LabelCount>();
        foreach (var module in statistics)
        {
            counts.AddRange(await module.CountTablesAsync(ct));
        }

        counts.Add(new("Comptes admin", await db.AdminUsers.CountAsync(ct)));
        counts.Add(new("Journal d'audit", await db.AuditEntries.CountAsync(ct)));
        return counts;
    }

    // Point-virgule + BOM UTF-8 : ouverture directe dans Excel en français.
    private static string ToCsv(List<string[]> rows)
    {
        var sb = new StringBuilder("﻿");
        foreach (var row in rows)
        {
            sb.AppendJoin(';', row.Select(Escape)).Append("\r\n");
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        // Neutralise les formules (=, +, -, @) interprétées par les tableurs.
        var safe = value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value;
        return safe.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
    }

    private static string Date(DateTimeOffset d) => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private async Task<List<string[]>> MembersAsync(CancellationToken ct)
    {
        var users = await identity.GetExportRowsAsync(ct);
        var posts = await feed.CountPostsByAuthorAsync(null, ct);
        var memberships = await communities.CountMembershipsByUserAsync(ct);
        return [["Prénom", "Nom", "Email", "Pays", "Ville", "Inscription", "Email vérifié", "Suspendu", "Publications", "Communautés"],
            .. users.Select(u => new[] { u.FirstName, u.LastName, u.Email, u.Country, u.City, Date(u.JoinedAt), u.IsEmailVerified ? "oui" : "non",
                u.IsSuspended ? "oui" : "non", posts.GetValueOrDefault(u.Id).ToString(), memberships.GetValueOrDefault(u.Id).ToString() })];
    }

    private async Task<List<string[]>> CommunitiesAsync(CancellationToken ct)
    {
        var list = await communities.GetExportRowsAsync(ct);
        var posts = await feed.CountPostsByCommunityAsync(ct);
        var agenda = await events.CountByCommunityAsync(ct);
        return [["Communauté", "Catégorie", "Pays", "Ville", "Visibilité", "Membres", "Publications", "Événements", "Création"],
            .. list.Select(c => new[] { c.Name, c.Category, c.Country, c.City, c.Visibility, c.Members.ToString(),
                posts.GetValueOrDefault(c.Id).ToString(), agenda.GetValueOrDefault(c.Id).ToString(), Date(c.CreatedAt) })];
    }

    private async Task<List<string[]>> EventsAsync(CancellationToken ct)
    {
        var list = await events.GetExportRowsAsync(ct);
        return [["Événement", "Catégorie", "Ville", "Pays", "Début", "Fin", "Capacité", "Participants", "Taux de remplissage"],
            .. list.Select(e => new[] { e.Title, e.Category, e.City, e.Country, Date(e.StartsAt), Date(e.EndsAt), e.Capacity?.ToString() ?? "illimitée",
                e.Participants.ToString(), e.Capacity is > 0 ? $"{100 * e.Participants / e.Capacity}%" : "" })];
    }

    private async Task<List<string[]>> ActivityAsync(CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-90);
        var signups = await identity.GetJoinDatesAsync(since, ct);
        var posts = (await feed.GetStatsAsync(since, ct)).PostedSince;
        var comments = await feed.GetCommentDatesAsync(since, ct);
        var messages = await messaging.GetMessageDatesAsync(since, ct);

        static Dictionary<DateOnly, int> ByDay(IReadOnlyList<DateTimeOffset> d) =>
            d.GroupBy(x => DateOnly.FromDateTime(x.UtcDateTime)).ToDictionary(g => g.Key, g => g.Count());
        var (s, p, c, m) = (ByDay(signups), ByDay(posts), ByDay(comments), ByDay(messages));
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        return [["Jour", "Inscriptions", "Publications", "Commentaires", "Messages"],
            .. Enumerable.Range(0, 90).Select(i => today.AddDays(-i)).Select(day => new[]
            {
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), s.GetValueOrDefault(day).ToString(), p.GetValueOrDefault(day).ToString(),
                c.GetValueOrDefault(day).ToString(), m.GetValueOrDefault(day).ToString()
            })];
    }
}
