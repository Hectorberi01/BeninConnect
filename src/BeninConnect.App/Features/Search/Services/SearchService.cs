using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.Features.Search.Services;

public enum SearchScope
{
    All,
    People,
    Posts,
    Communities,
    Events
}

public sealed record SearchResults(
    IReadOnlyList<User> People,
    IReadOnlyList<Post> Posts,
    IReadOnlyList<Community> Communities,
    IReadOnlyList<CommunityEvent> Events)
{
    public static readonly SearchResults Empty = new([], [], [], []);

    public int TotalCount => People.Count + Posts.Count + Communities.Count + Events.Count;
}

public interface ISearchService
{
    Task<SearchResults> SearchAsync(string query, SearchScope scope, Guid viewerId, CancellationToken cancellationToken = default);
}

/// <summary>Recherche globale composée à partir des repositories (fonctionne à l'identique avec l'API).</summary>
public sealed class SearchService(
    IUserRepository users,
    IPostRepository posts,
    ICommunityRepository communities,
    IEventRepository events) : ISearchService
{
    public async Task<SearchResults> SearchAsync(string query, SearchScope scope, Guid viewerId, CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        if (q.Length < 2)
        {
            return SearchResults.Empty;
        }

        bool Want(SearchScope s) => scope == SearchScope.All || scope == s;

        var peopleTask = Want(SearchScope.People) ? users.SearchAsync(q, cancellationToken) : Task.FromResult<IReadOnlyList<User>>([]);
        var postsTask = Want(SearchScope.Posts) ? posts.SearchAsync(q, viewerId, cancellationToken) : Task.FromResult<IReadOnlyList<Post>>([]);
        var communitiesTask = Want(SearchScope.Communities) ? communities.SearchAsync(q, cancellationToken) : Task.FromResult<IReadOnlyList<Community>>([]);
        var eventsTask = Want(SearchScope.Events) ? events.GetUpcomingAsync(new EventFilter(q), cancellationToken) : Task.FromResult<IReadOnlyList<CommunityEvent>>([]);

        await Task.WhenAll(peopleTask, postsTask, communitiesTask, eventsTask);

        var limit = scope == SearchScope.All ? 5 : 50;
        return new SearchResults(
            (await peopleTask).Where(u => u.Id != viewerId).Take(limit).ToList(),
            (await postsTask).Take(limit).ToList(),
            (await communitiesTask).Take(limit).ToList(),
            (await eventsTask).Take(limit).ToList());
    }
}
