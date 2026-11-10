using BeninConnect.Modules.Messaging.Application;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Messaging.Infrastructure;
using System.Text.Json.Serialization;
using BeninConnect.Modules.Messaging.Infrastructure.Persistence;
using BeninConnect.Modules.Messaging.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Messaging.Api;

public sealed record StartConversationRequest(Guid OtherUserId);

/// <summary>PostId : partager une publication (Content facultatif).</summary>
public sealed record SendMessageRequest(string? Content, Guid? PostId = null);

public sealed class MessagingModule : IModule
{
    public string Name => "Messaging";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<MessagingDbContext>(builder.Configuration, Name, MessagingDbContext.SchemaName);
        services.AddScoped<IMessagingDb>(sp => sp.GetRequiredService<MessagingDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Messaging");
        services.AddScoped<MessagingModuleService>();
        services.AddScoped<IMessagingModule>(sp => sp.GetRequiredService<MessagingModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<MessagingModuleService>());

        // Temps réel : une seule instance de l'API, pas de fond de panier (Redis) nécessaire.
        services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSingleton<IUserIdProvider, MemberIdProvider>();
        services.AddScoped<IChatNotifier, SignalRChatNotifier>();
        services.AddSingleton<PresenceTracker>();
        services.AddSingleton<IPresence>(sp => sp.GetRequiredService<PresenceTracker>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var conversations = app.MapGroup("/api/conversations").WithTags("Messagerie").RequireAuthorization();
        conversations.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetConversationsQuery(), ct));
        conversations.MapGet("/unread-count", (ISender s, CancellationToken ct) => s.Send(new GetUnreadMessageCountQuery(), ct));
        conversations.MapPost("/direct", (StartConversationRequest r, ISender s, CancellationToken ct) => s.Send(new StartDirectConversationCommand(r.OtherUserId), ct));
        conversations.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetConversationQuery(id), ct));
        conversations.MapGet("/{id:guid}/messages", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetMessagesQuery(id), ct));
        conversations.MapPost("/{id:guid}/messages", async (Guid id, SendMessageRequest r, ISender s, CancellationToken ct) =>
        {
            var message = await s.Send(new SendMessageCommand(id, r.Content ?? string.Empty, r.PostId), ct);
            return Results.Created($"/api/conversations/{id}/messages", message);
        });
        conversations.MapPost("/{id:guid}/messages/{messageId:guid}/reactions", (Guid id, Guid messageId, ReactionRequest r, ISender s, CancellationToken ct) =>
            s.Send(new ReactToMessageCommand(id, messageId, r.Emoji), ct));
        conversations.MapPost("/{id:guid}/attachments", async (Guid id, HttpRequest http, ISender s, CancellationToken ct) =>
            {
                var form = await http.ReadFormAsync(ct);
                var file = form.Files.GetFile("file");
                await using var content = file?.OpenReadStream() ?? Stream.Null;
                var duration = int.TryParse(form["durationMs"], out var ms) ? ms : (int?)null;
                var message = await s.Send(new SendAttachmentCommand(id, content, file?.Length ?? 0, file?.FileName, form["content"], duration), ct);
                return Results.Created($"/api/conversations/{id}/messages", message);
            })
            .RequireRateLimiting(RateLimits.Uploads)
            .DisableAntiforgery();
        conversations.MapPut("/{id:guid}/messages/{messageId:guid}", (Guid id, Guid messageId, ContentRequest r, ISender s, CancellationToken ct) =>
            s.Send(new EditMessageCommand(id, messageId, r.Content), ct));
        conversations.MapDelete("/{id:guid}/messages/{messageId:guid}", (Guid id, Guid messageId, ISender s, CancellationToken ct) =>
            s.Send(new DeleteMessageCommand(id, messageId), ct));
        conversations.MapPost("/{id:guid}/read", async (Guid id, ISender s, CancellationToken ct) =>
            Results.Ok(new CountDto(await s.Send(new MarkConversationReadCommand(id), ct))));

        // Pièce jointe : participants uniquement. Jeton accepté dans l'URL (images, lecteur audio, PDF ouverts par le téléphone).
        app.MapGet("/api/messages/{messageId:guid}/attachment", async (Guid messageId, ISender s, HttpContext http, CancellationToken ct) =>
            {
                var file = await s.Send(new GetAttachmentQuery(messageId), ct);
                http.Response.Headers.CacheControl = "private, max-age=86400";
                http.Response.Headers.ContentDisposition = new System.Net.Mime.ContentDisposition { Inline = true, FileName = file.FileName }.ToString();
                return Results.Stream(file.Content, file.ContentType, enableRangeProcessing: file.Content.CanSeek);
            })
            .WithTags("Messagerie")
            .RequireAuthorization();

        app.MapHub<ChatHub>("/hubs/chat");

        if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            // Démonstration uniquement : réponse fictive de l'autre participant, toujours marquée IsSimulated.
            conversations.MapPost("/{id:guid}/demo-reply", (Guid id, ContentRequest r, ISender s, CancellationToken ct) =>
                s.Send(new SimulateReplyCommand(id, r.Content), ct));
        }
    }
}
