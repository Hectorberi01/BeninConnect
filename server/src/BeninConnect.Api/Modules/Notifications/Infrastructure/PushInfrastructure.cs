using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using BeninConnect.Modules.Notifications.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BeninConnect.Modules.Notifications.Infrastructure;

/// <summary>Section « Push ». Provider : expo (par défaut) ou log (aucun envoi, journal seulement).</summary>
public sealed class PushSettings
{
    public const string Section = "Push";

    public string Provider { get; set; } = "expo";

    /// <summary>Jeton d'accès Expo, obligatoire si « Enhanced Security for Push Notifications » est activé sur le projet.</summary>
    public string? ExpoAccessToken { get; set; }

    public bool IsExpo => Provider.Equals("expo", StringComparison.OrdinalIgnoreCase);
}

/// <summary>File en mémoire : les notifications créées pendant une requête partent juste après, en arrière-plan.</summary>
public sealed class ChannelPushQueue : IPushQueue
{
    private readonly Channel<PushRequest> _channel = Channel.CreateBounded<PushRequest>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<PushRequest> Reader => _channel.Reader;

    public void Enqueue(PushRequest request) => _channel.Writer.TryWrite(request);
}

public sealed class PushDispatcher(ChannelPushQueue queue, IServiceScopeFactory scopes, ILogger<PushDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PushComposer>().SendAsync(request, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Notification push {NotificationId} non envoyée", request.NotificationId);
            }
        }
    }
}

public sealed class LogPushSender(ILogger<LogPushSender> logger) : IPushSender
{
    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        foreach (var m in messages)
        {
            logger.LogInformation("Push (non envoyé) vers {Token} : {Title} — {Body}", m.Token, m.Title, m.Body);
        }

        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}

/// <summary>Service push d'Expo (relaie vers APNs et FCM). https://docs.expo.dev/push-notifications/sending-notifications/</summary>
public sealed class ExpoPushSender(HttpClient http, PushSettings settings, ILogger<ExpoPushSender> logger) : IPushSender
{
    public const string BaseAddress = "https://exp.host/--/api/v2/";
    private const int BatchSize = 100; // limite Expo par requête

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        var invalid = new List<string>();
        foreach (var batch in messages.Chunk(BatchSize))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "push/send")
            {
                Content = JsonContent.Create(batch.Select(m => new
                {
                    to = m.Token,
                    title = m.Title,
                    body = m.Body,
                    data = m.Data,
                    badge = m.Badge,
                    sound = "default",
                    channelId = "default",
                    priority = "high"
                }))
            };
            if (!string.IsNullOrWhiteSpace(settings.ExpoAccessToken))
            {
                request.Headers.Authorization = new("Bearer", settings.ExpoAccessToken);
            }

            try
            {
                using var response = await http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Expo push refusé : {Status} {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<ExpoResponse>(ct);
                foreach (var (ticket, message) in (result?.Data ?? []).Zip(batch))
                {
                    if (ticket.Status != "error")
                    {
                        continue;
                    }

                    if (ticket.Details?.Error == "DeviceNotRegistered")
                    {
                        invalid.Add(message.Token);
                    }
                    else
                    {
                        logger.LogWarning("Push refusé pour {Token} : {Error} {Message}", message.Token, ticket.Details?.Error, ticket.Message);
                    }
                }
            }
            catch (HttpRequestException e)
            {
                logger.LogError(e, "Service push Expo injoignable");
            }
        }

        return invalid;
    }

    private sealed record ExpoResponse([property: JsonPropertyName("data")] List<ExpoTicket>? Data);

    private sealed record ExpoTicket(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("details")] ExpoTicketDetails? Details);

    private sealed record ExpoTicketDetails([property: JsonPropertyName("error")] string? Error);
}
