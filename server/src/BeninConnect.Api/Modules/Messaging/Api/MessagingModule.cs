using BeninConnect.Modules.Messaging.Application;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Messaging.Infrastructure;
using BeninConnect.Modules.Messaging.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Messaging.Api;

public sealed record StartConversationRequest(Guid OtherUserId);

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
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var conversations = app.MapGroup("/api/conversations").WithTags("Messagerie").RequireAuthorization();
        conversations.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetConversationsQuery(), ct));
        conversations.MapGet("/unread-count", (ISender s, CancellationToken ct) => s.Send(new GetUnreadMessageCountQuery(), ct));
        conversations.MapPost("/direct", (StartConversationRequest r, ISender s, CancellationToken ct) => s.Send(new StartDirectConversationCommand(r.OtherUserId), ct));
        conversations.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetConversationQuery(id), ct));
        conversations.MapGet("/{id:guid}/messages", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetMessagesQuery(id), ct));
        conversations.MapPost("/{id:guid}/messages", async (Guid id, ContentRequest r, ISender s, CancellationToken ct) =>
        {
            var message = await s.Send(new SendMessageCommand(id, r.Content), ct);
            return Results.Created($"/api/conversations/{id}/messages", message);
        });
        conversations.MapPost("/{id:guid}/messages/{messageId:guid}/reactions", (Guid id, Guid messageId, ReactionRequest r, ISender s, CancellationToken ct) =>
            s.Send(new ReactToMessageCommand(id, messageId, r.Emoji), ct));
        conversations.MapPost("/{id:guid}/read", async (Guid id, ISender s, CancellationToken ct) =>
            Results.Ok(new CountDto(await s.Send(new MarkConversationReadCommand(id), ct))));

        if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            // Démonstration uniquement : réponse fictive de l'autre participant, toujours marquée IsSimulated.
            conversations.MapPost("/{id:guid}/demo-reply", (Guid id, ContentRequest r, ISender s, CancellationToken ct) =>
                s.Send(new SimulateReplyCommand(id, r.Content), ct));
        }
    }
}
