using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Feed.Application;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Feed.Domain;
using BeninConnect.Modules.Feed.Infrastructure;
using BeninConnect.Modules.Feed.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Feed.Api;

public sealed record UpdatePostRequest(string Content, string? ImagePath, PostVisibility Visibility, string? VideoPath = null);

/// <summary>Publications, commentaires, j'aime et réactions.</summary>
public sealed class FeedModule : IModule
{
    public string Name => "Feed";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<FeedDbContext>(builder.Configuration, Name, FeedDbContext.SchemaName);
        services.AddScoped<IFeedDb>(sp => sp.GetRequiredService<FeedDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Feed");
        services.AddScoped<PostAccess>();
        services.AddSingleton(builder.Configuration.GetSection(ShareSettings.Section).Get<ShareSettings>() ?? new ShareSettings());
        services.AddScoped<AdminPostReader>();
        services.AddScoped<FeedModuleService>();
        services.AddScoped<IFeedModule>(sp => sp.GetRequiredService<FeedModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<FeedModuleService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        SharePage.Map(app); // page publique d'une publication partagée

        var feed = app.MapGroup("/api").WithTags("Publications").RequireAuthorization();
        feed.MapGet("/feed", (int? skip, int? take, ISender s, CancellationToken ct) => s.Send(new GetFeedQuery(skip ?? 0, take ?? 10), ct));
        feed.MapGet("/posts", (Guid? authorId, Guid? communityId, string? search, ISender s, CancellationToken ct) =>
            s.Send(new GetPostsQuery(authorId, communityId, search), ct));
        feed.MapGet("/posts/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetPostQuery(id), ct));
        feed.MapPost("/posts", async (CreatePostCommand c, ISender s, CancellationToken ct) =>
        {
            var post = await s.Send(c, ct);
            return Results.Created($"/api/posts/{post.Id}", post);
        });
        feed.MapPut("/posts/{id:guid}", (Guid id, UpdatePostRequest r, ISender s, CancellationToken ct) =>
            s.Send(new UpdatePostCommand(id, r.Content, r.ImagePath, r.Visibility, r.VideoPath), ct));
        feed.MapDelete("/posts/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteMyPostCommand(id), ct);
            return Results.NoContent();
        });
        feed.MapPost("/posts/{id:guid}/like", (Guid id, ISender s, CancellationToken ct) => s.Send(new ToggleLikeCommand(id), ct));
        feed.MapPost("/posts/{id:guid}/share", (Guid id, ISender s, CancellationToken ct) => s.Send(new SharePostCommand(id), ct));
        feed.MapGet("/posts/{id:guid}/comments", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetCommentsQuery(id), ct));
        feed.MapPost("/posts/{id:guid}/comments", async (Guid id, ContentRequest r, ISender s, CancellationToken ct) =>
        {
            var comment = await s.Send(new AddCommentCommand(id, r.Content), ct);
            return Results.Created($"/api/posts/{id}/comments", comment);
        });
        feed.MapPost("/posts/{id:guid}/comments/{commentId:guid}/reactions", (Guid id, Guid commentId, ReactionRequest r, ISender s, CancellationToken ct) =>
            s.Send(new ReactToCommentCommand(id, commentId, r.Emoji), ct));

        var admin = app.MapGroup("/api/admin").WithTags("Administration");
        admin.MapGet("/posts", (string? search, string? status, Guid? authorId, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminPostsQuery(search, status, authorId, skip ?? 0, take ?? 25), ct)).RequirePermission(Permission.ContentModerate);
        admin.MapGet("/posts/{id:guid}/comments", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAdminCommentsQuery(id), ct))
            .RequirePermission(Permission.ContentModerate);
        admin.MapPost("/posts/{id:guid}/hide", (Guid id, ReasonRequest r, ISender s, CancellationToken ct) => s.Send(new HidePostCommand(id, r.Reason), ct))
            .RequirePermission(Permission.ContentModerate);
        admin.MapPost("/posts/{id:guid}/unhide", (Guid id, ISender s, CancellationToken ct) => s.Send(new UnhidePostCommand(id), ct))
            .RequirePermission(Permission.ContentModerate);
        admin.MapDelete("/posts/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeletePostCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permission.ContentModerate);
        admin.MapDelete("/comments/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteCommentCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permission.ContentModerate);
    }
}
