using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Messaging.Models;
using BeninConnect.App.Features.Notifications.Contracts;
using BeninConnect.App.Features.Notifications.Models;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Infrastructure.Api;

// Implémentations HTTP des contrats. Elles publient les mêmes messages que les mocks
// pour que tous les écrans restent synchronisés. Les paramètres « userId » des contrats
// sont ignorés : l'API utilise l'utilisateur du jeton JWT.

public sealed class ApiAuthService(ApiClient api) : IAuthService
{
    public string DemoVerificationCode => "123456";

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        try
        {
            var response = await api.PostAsync<AuthResponseDto>("api/auth/login", new { email, password }, ct);
            api.SetToken(response.Token);
            return AuthResult.Success(response.User.ToModel());
        }
        catch (Core.Utilities.DomainException ex)
        {
            return AuthResult.Failure(ex.Message);
        }
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        try
        {
            var user = await api.PostAsync<UserDto>("api/auth/register", request, ct);
            return AuthResult.Success(user.ToModel());
        }
        catch (Core.Utilities.DomainException ex)
        {
            return AuthResult.Failure(ex.Message);
        }
    }

    public async Task<AuthResult> VerifyEmailAsync(string email, string code, CancellationToken ct = default)
    {
        try
        {
            var response = await api.PostAsync<AuthResponseDto>("api/auth/verify-email", new { email, code }, ct);
            api.SetToken(response.Token);
            return AuthResult.Success(response.User.ToModel());
        }
        catch (Core.Utilities.DomainException ex)
        {
            return AuthResult.Failure(ex.Message);
        }
    }

    public Task ResendVerificationCodeAsync(string email, CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Post, "api/auth/resend-code", new { email }, ct);

    public Task RequestPasswordResetAsync(string email, CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Post, "api/auth/forgot-password", new { email }, ct);

    public async Task<User?> RestoreSessionAsync(string email, CancellationToken ct = default)
    {
        if (api.Token is null)
        {
            return null;
        }

        try
        {
            var me = (await api.GetAsync<UserDto>("api/auth/me", ct)).ToModel();
            return string.Equals(me.Email, email, StringComparison.OrdinalIgnoreCase) ? me : null;
        }
        catch (Core.Utilities.DomainException)
        {
            api.SetToken(null);
            return null;
        }
    }
}

public sealed class ApiUserRepository(ApiClient api, IMessenger messenger) : IUserRepository
{
    public IReadOnlyList<string> AvailableInterests => ReferenceData.Interests;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await api.GetAsync<UserDto>($"api/users/{id}", ct)).ToModel();

    public async Task<IReadOnlyDictionary<Guid, User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<Guid, User>();
        }

        var users = await api.PostAsync<List<UserDto>>("api/users/batch", new { ids = list }, ct);
        return users.Select(u => u.ToModel()).ToDictionary(u => u.Id);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        (await api.GetAsync<List<UserDto>>("api/users", ct)).Select(u => u.ToModel()).ToList();

    public async Task<IReadOnlyList<User>> SearchAsync(string query, CancellationToken ct = default) =>
        (await api.GetAsync<List<UserDto>>($"api/users?search={Uri.EscapeDataString(query)}", ct)).Select(u => u.ToModel()).ToList();

    public async Task<User> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = (await api.PutAsync<UserDto>("api/users/me", new
        {
            request.FirstName,
            request.LastName,
            request.Profession,
            request.Country,
            request.City,
            request.Bio,
            request.Interests
        }, ct)).ToModel();
        messenger.Send(new UserUpdatedMessage(user));
        return user;
    }
}

public sealed class ApiPostRepository(ApiClient api, IMessenger messenger) : IPostRepository
{
    public async Task<PagedResult<Post>> GetFeedAsync(Guid viewerId, int skip, int take, CancellationToken ct = default)
    {
        var page = await api.GetAsync<PagedDto<PostDto>>($"api/feed?skip={skip}&take={take}", ct);
        return new PagedResult<Post>(page.Items.Select(p => p.ToModel()).ToList(), page.TotalCount, page.Skip);
    }

    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await api.GetAsync<PostDto>($"api/posts/{id}", ct)).ToModel();

    public Task<IReadOnlyList<Post>> GetByAuthorAsync(Guid authorId, CancellationToken ct = default) => ListAsync($"api/posts?authorId={authorId}", ct);

    public Task<IReadOnlyList<Post>> GetByCommunityAsync(Guid communityId, CancellationToken ct = default) => ListAsync($"api/posts?communityId={communityId}", ct);

    public Task<IReadOnlyList<Post>> SearchAsync(string query, Guid viewerId, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(query)
            ? Task.FromResult<IReadOnlyList<Post>>([])
            : ListAsync($"api/posts?search={Uri.EscapeDataString(query.Trim())}", ct);

    public async Task<Post> CreateAsync(CreatePostRequest request, CancellationToken ct = default)
    {
        var post = (await api.PostAsync<PostDto>("api/posts", new
        {
            request.CommunityId,
            request.Content,
            request.ImagePath,
            request.Visibility
        }, ct)).ToModel();
        messenger.Send(new PostCreatedMessage(post));
        return post;
    }

    public async Task ToggleLikeAsync(Guid postId, Guid userId, CancellationToken ct = default) =>
        messenger.Send(new PostChangedMessage((await api.PostAsync<PostDto>($"api/posts/{postId}/like", null, ct)).ToModel()));

    public async Task ShareAsync(Guid postId, Guid userId, CancellationToken ct = default) =>
        messenger.Send(new PostChangedMessage((await api.PostAsync<PostDto>($"api/posts/{postId}/share", null, ct)).ToModel()));

    private async Task<IReadOnlyList<Post>> ListAsync(string path, CancellationToken ct) =>
        (await api.GetAsync<List<PostDto>>(path, ct)).Select(p => p.ToModel()).ToList();
}

public sealed class ApiCommentRepository(ApiClient api, IMessenger messenger) : ICommentRepository
{
    public async Task<IReadOnlyList<Comment>> GetForPostAsync(Guid postId, CancellationToken ct = default) =>
        (await api.GetAsync<List<CommentDto>>($"api/posts/{postId}/comments", ct)).Select(c => c.ToModel()).ToList();

    public async Task<Comment> AddAsync(CreateCommentRequest request, CancellationToken ct = default)
    {
        var comment = (await api.PostAsync<CommentDto>($"api/posts/{request.PostId}/comments", new { request.Content }, ct)).ToModel();
        messenger.Send(new CommentAddedMessage(comment));

        // Compteur de commentaires à jour sur toutes les cartes.
        var post = await api.GetAsync<PostDto>($"api/posts/{request.PostId}", ct);
        messenger.Send(new PostChangedMessage(post.ToModel()));
        return comment;
    }
}

public sealed class ApiCommunityRepository(ApiClient api, IMessenger messenger) : ICommunityRepository
{
    public Task<IReadOnlyList<Community>> GetAllAsync(CancellationToken ct = default) => ListAsync("api/communities", ct);

    public async Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await api.GetAsync<CommunityDto>($"api/communities/{id}", ct)).ToModel();

    public async Task<IReadOnlyDictionary<Guid, Community>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<Guid, Community>();
        }

        return (await api.PostAsync<List<CommunityDto>>("api/communities/batch", new { ids = list }, ct))
            .Select(c => c.ToModel()).ToDictionary(c => c.Id);
    }

    public Task<IReadOnlyList<Community>> GetForMemberAsync(Guid userId, CancellationToken ct = default) => ListAsync($"api/users/{userId}/communities", ct);

    public Task<IReadOnlyList<Community>> SearchAsync(string query, CancellationToken ct = default) =>
        ListAsync($"api/communities?search={Uri.EscapeDataString(query.Trim())}", ct);

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        await api.GetAsync<List<string>>("api/communities/categories", ct);

    public async Task<IReadOnlyList<CommunityMember>> GetMembersAsync(Guid communityId, CancellationToken ct = default) =>
        (await api.GetAsync<List<CommunityMemberDto>>($"api/communities/{communityId}/members", ct))
        .Select(m => new CommunityMember(m.User.ToModel(), m.Role)).ToList();

    public async Task<IReadOnlySet<Guid>> GetPendingRequestsAsync(Guid userId, CancellationToken ct = default) =>
        (await api.GetAsync<List<Guid>>("api/communities/join-requests/mine", ct)).ToHashSet();

    public async Task<Community> CreateAsync(CreateCommunityRequest request, CancellationToken ct = default)
    {
        var created = (await api.PostAsync<CommunityDto>("api/communities", new
        {
            request.Name,
            request.Description,
            request.Category,
            request.Country,
            request.City,
            request.Visibility,
            request.CoverImage
        }, ct)).ToModel();
        messenger.Send(new CommunityCreatedMessage(created));
        messenger.Send(new MembershipChangedMessage(created.Id, request.OwnerId, MembershipStatus.Owner));
        return created;
    }

    public async Task<MembershipStatus> JoinAsync(Guid communityId, Guid userId, CancellationToken ct = default)
    {
        var result = await api.PostAsync<MembershipDto>($"api/communities/{communityId}/join", null, ct);
        await PublishAsync(communityId, userId, result.Status, ct);
        if (result.Status == MembershipStatus.Member)
        {
            messenger.Send(new NotificationsChangedMessage(userId));
        }

        return result.Status;
    }

    public async Task LeaveAsync(Guid communityId, Guid userId, CancellationToken ct = default)
    {
        var result = await api.DeleteAsync<MembershipDto>($"api/communities/{communityId}/membership", ct);
        await PublishAsync(communityId, userId, result.Status, ct);
    }

    public async Task CancelJoinRequestAsync(Guid communityId, Guid userId, CancellationToken ct = default)
    {
        var result = await api.DeleteAsync<MembershipDto>($"api/communities/{communityId}/join-request", ct);
        await PublishAsync(communityId, userId, result.Status, ct);
    }

    private async Task PublishAsync(Guid communityId, Guid userId, MembershipStatus status, CancellationToken ct)
    {
        var community = await GetByIdAsync(communityId, ct);
        if (community is not null)
        {
            messenger.Send(new CommunityChangedMessage(community));
        }

        messenger.Send(new MembershipChangedMessage(communityId, userId, status));
    }

    private async Task<IReadOnlyList<Community>> ListAsync(string path, CancellationToken ct) =>
        (await api.GetAsync<List<CommunityDto>>(path, ct)).Select(c => c.ToModel()).ToList();
}

public sealed class ApiConversationRepository(ApiClient api, IMessenger messenger) : IConversationRepository
{
    public async Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(Guid userId, CancellationToken ct = default) =>
        (await api.GetAsync<List<ConversationSummaryDto>>("api/conversations", ct)).Select(s => s.ToModel()).ToList();

    public async Task<ConversationSummary?> GetSummaryAsync(Guid conversationId, Guid userId, CancellationToken ct = default) =>
        (await api.GetAsync<ConversationSummaryDto>($"api/conversations/{conversationId}", ct)).ToModel();

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(Guid conversationId, CancellationToken ct = default) =>
        (await api.GetAsync<List<MessageDto>>($"api/conversations/{conversationId}/messages", ct)).Select(m => m.ToModel()).ToList();

    public async Task<Message> SendAsync(SendMessageRequest request, CancellationToken ct = default)
    {
        // La réponse simulée passe par l'endpoint de démonstration (disponible en environnement Development).
        var path = request.IsSimulated
            ? $"api/conversations/{request.ConversationId}/demo-reply"
            : $"api/conversations/{request.ConversationId}/messages";
        var message = (await api.PostAsync<MessageDto>(path, new { request.Content }, ct)).ToModel();
        messenger.Send(new ConversationChangedMessage(request.ConversationId));
        return message;
    }

    public async Task MarkAsReadAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var result = await api.PostAsync<CountDto>($"api/conversations/{conversationId}/read", null, ct);
        if (result.Count > 0)
        {
            messenger.Send(new ConversationChangedMessage(conversationId));
        }
    }

    public async Task<Conversation> GetOrCreateDirectAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        var conversation = (await api.PostAsync<ConversationDto>("api/conversations/direct", new { otherUserId }, ct)).ToModel();
        messenger.Send(new ConversationChangedMessage(conversation.Id));
        return conversation;
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        (await api.GetAsync<CountDto>("api/conversations/unread-count", ct)).Count;
}

public sealed class ApiEventRepository(ApiClient api, IMessenger messenger) : IEventRepository
{
    public Task<IReadOnlyList<CommunityEvent>> GetUpcomingAsync(EventFilter filter, CancellationToken ct = default)
    {
        var query = new List<string> { $"period={filter.Period}" };
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (filter.Category is not null)
        {
            query.Add($"category={Uri.EscapeDataString(filter.Category)}");
        }

        if (filter.City is not null)
        {
            query.Add($"city={Uri.EscapeDataString(filter.City)}");
        }

        return ListAsync($"api/events?{string.Join('&', query)}", ct);
    }

    public async Task<CommunityEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await api.GetAsync<EventDto>($"api/events/{id}", ct)).ToModel();

    public Task<IReadOnlyList<CommunityEvent>> GetForCommunityAsync(Guid communityId, CancellationToken ct = default) =>
        ListAsync($"api/communities/{communityId}/events", ct);

    public Task<IReadOnlyList<CommunityEvent>> GetForParticipantAsync(Guid userId, CancellationToken ct = default) =>
        ListAsync($"api/users/{userId}/events", ct);

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        await api.GetAsync<List<string>>("api/events/categories", ct);

    public async Task<IReadOnlyList<string>> GetCitiesAsync(CancellationToken ct = default) =>
        await api.GetAsync<List<string>>("api/events/cities", ct);

    public async Task<CommunityEvent> CreateAsync(CreateEventRequest request, CancellationToken ct = default)
    {
        var created = (await api.PostAsync<EventDto>("api/events", new
        {
            request.Title,
            request.Description,
            request.Category,
            request.City,
            request.Country,
            request.Address,
            StartsAt = request.StartsAt.ToUniversalTime(),
            EndsAt = request.EndsAt.ToUniversalTime(),
            request.Capacity,
            request.CommunityId,
            request.ImagePath
        }, ct)).ToModel();
        messenger.Send(new EventCreatedMessage(created));
        return created;
    }

    public async Task<bool> ToggleParticipationAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var result = await api.PostAsync<ParticipationDto>($"api/events/{eventId}/participation", null, ct);
        messenger.Send(new EventChangedMessage(result.Event.ToModel()));
        return result.IsParticipating;
    }

    private async Task<IReadOnlyList<CommunityEvent>> ListAsync(string path, CancellationToken ct) =>
        (await api.GetAsync<List<EventDto>>(path, ct)).Select(e => e.ToModel()).ToList();
}

public sealed class ApiNotificationRepository(ApiClient api, IMessenger messenger) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, NotificationCategory? category = null, CancellationToken ct = default) =>
        (await api.GetAsync<List<NotificationDto>>(category is null ? "api/notifications" : $"api/notifications?category={category}", ct))
        .Select(n => n.ToModel()).ToList();

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        (await api.GetAsync<CountDto>("api/notifications/unread-count", ct)).Count;

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        var n = await api.PostAsync<NotificationDto>($"api/notifications/{notificationId}/read", null, ct);
        messenger.Send(new NotificationsChangedMessage(n.UserId));
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await api.PostAsync<CountDto>("api/notifications/read-all", null, ct);
        messenger.Send(new NotificationsChangedMessage(userId));
    }
}
