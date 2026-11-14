using BeninConnect.Modules.Messaging.Application;
using BeninConnect.Modules.Messaging.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Infrastructure;

/// <summary>
/// Chiffre par lots les messages encore en clair (texte et nom de pièce jointe), en SQL direct :
/// la conversion EF déchiffrerait les valeurs lues. Une valeur modifiée entre-temps n'est pas écrasée.
/// </summary>
public sealed class MessageEncryptionJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<MessageEncryptionJob> log) : IMessageEncryptionJob
{
    private const int BatchSize = 200;
    private const string NeedsWork =
        "(\"Content\" <> '' AND \"Content\" NOT LIKE 'enc1:%') OR (\"Attachment_FileName\" IS NOT NULL AND \"Attachment_FileName\" NOT LIKE 'enc1:%')";

    private readonly Lock gate = new();
    private bool running;
    private int processed;
    private DateTimeOffset? startedAt;
    private DateTimeOffset? finishedAt;
    private string? error;
    private string? startedBy;

    private sealed record PlainRow(Guid Id, string Content, string? FileName);

    public async Task<MessageEncryptionStatusDto> StatusAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
        var total = await db.Messages.CountAsync(ct);
        var plain = await db.Database.SqlQueryRaw<int>($"SELECT count(*)::int AS \"Value\" FROM messaging.messages WHERE {NeedsWork}").SingleAsync(ct);
        lock (gate)
        {
            return new MessageEncryptionStatusDto(total, plain, running, processed, startedAt, finishedAt, error, startedBy);
        }
    }

    public bool TryStart(string by)
    {
        lock (gate)
        {
            if (running)
            {
                return false;
            }

            running = true;
            processed = 0;
            startedAt = clock.GetUtcNow();
            finishedAt = null;
            error = null;
            startedBy = by;
        }

        _ = Task.Run(RunAsync);
        return true;
    }

    /// <summary>Exécution complète (appelée en tâche de fond ; utilisable directement dans les tests).</summary>
    public async Task RunAsync()
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            var cipher = MessageCipher.Current;
            while (true)
            {
                var rows = await db.Database.SqlQueryRaw<PlainRow>(
                        $"SELECT \"Id\", \"Content\", \"Attachment_FileName\" AS \"FileName\" FROM messaging.messages WHERE {NeedsWork} ORDER BY \"Id\" LIMIT {BatchSize}")
                    .ToListAsync();
                if (rows.Count == 0)
                {
                    break;
                }

                var changed = 0;
                foreach (var row in rows)
                {
                    changed += await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE messaging.messages SET "Content" = {cipher.Protect(row.Content)}, "Attachment_FileName" = {(row.FileName is null ? null : cipher.Protect(row.FileName))}
                        WHERE "Id" = {row.Id} AND "Content" = {row.Content} AND "Attachment_FileName" IS NOT DISTINCT FROM {row.FileName}
                        """);
                }

                lock (gate)
                {
                    processed += changed;
                }

                if (changed == 0)
                {
                    break; // lignes modifiées entre-temps : reprises au prochain lancement
                }
            }

            log.LogInformation("Messagerie : {Count} message(s) chiffré(s)", processed);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Messagerie : chiffrement des messages existants en échec");
            lock (gate)
            {
                error = "Échec du chiffrement : consultez les journaux de l'API.";
            }
        }
        finally
        {
            lock (gate)
            {
                running = false;
                finishedAt = clock.GetUtcNow();
            }
        }
    }
}
