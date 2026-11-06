using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Messaging.Models;
using BeninConnect.App.Features.Notifications.Models;
using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.Infrastructure.Api;

// Contrat JSON de l'API (server/src/BeninConnect.Application/Common/Dtos.cs).
// Les dates arrivent en UTC et sont converties en heure locale de l'appareil.

public sealed record UserDto(
    Guid Id, string FirstName, string LastName, string Email, string? Profession, string? Country, string? City,
    string? OriginCity, string? Bio, List<string>? Interests, string? AvatarColor, string? CoverImage,
    DateTimeOffset JoinedAt, bool IsEmailVerified);

public sealed record AuthResponseDto(string Token, UserDto User);

public sealed record PostDto(
    Guid Id, Guid AuthorId, Guid? CommunityId, string Content, string? ImagePath, DateTimeOffset CreatedAt,
    PostVisibility Visibility, List<Guid>? LikedBy, int CommentCount, int ShareCount);

public sealed record CommentDto(Guid Id, Guid PostId, Guid AuthorId, string Content, DateTimeOffset CreatedAt);

public sealed record PagedDto<T>(List<T> Items, int TotalCount, int Skip);

public sealed record CommunityDto(
    Guid Id, string Name, string Description, string Category, string? Country, string? City,
    CommunityVisibility Visibility, string? CoverImage, Guid OwnerId, List<Guid>? ModeratorIds,
    DateTimeOffset CreatedAt, List<Guid>? MemberIds);

public sealed record CommunityMemberDto(UserDto User, CommunityRole Role);

public sealed record MembershipDto(MembershipStatus Status);

public sealed record ConversationDto(Guid Id, List<Guid> ParticipantIds, DateTimeOffset CreatedAt);

public sealed record MessageDto(Guid Id, Guid ConversationId, Guid SenderId, string Content, DateTimeOffset SentAt, bool IsRead, bool IsSimulated);

public sealed record ConversationSummaryDto(ConversationDto Conversation, UserDto OtherParticipant, MessageDto? LastMessage, int UnreadCount);

public sealed record EventDto(
    Guid Id, string Title, string Description, string Category, string City, string? Country, string Address,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid OrganizerId, Guid? CommunityId, string? ImagePath,
    int? Capacity, List<Guid>? ParticipantIds);

public sealed record ParticipationDto(bool IsParticipating, EventDto Event);

public sealed record NotificationDto(
    Guid Id, Guid UserId, NotificationCategory Category, string Title, string Body, DateTimeOffset CreatedAt,
    bool IsRead, NotificationTarget Target, Guid? TargetId);

public sealed record CountDto(int Count);

internal static class ApiMapping
{
    public static User ToModel(this UserDto d) => new()
    {
        Id = d.Id,
        FirstName = d.FirstName,
        LastName = d.LastName,
        Email = d.Email,
        Profession = d.Profession ?? string.Empty,
        Country = d.Country ?? string.Empty,
        City = d.City ?? string.Empty,
        OriginCity = d.OriginCity ?? string.Empty,
        Bio = d.Bio ?? string.Empty,
        Interests = d.Interests ?? [],
        AvatarColor = d.AvatarColor ?? "#087F5B",
        CoverImage = d.CoverImage,
        JoinedAt = d.JoinedAt.ToLocalTime(),
        IsEmailVerified = d.IsEmailVerified
    };

    public static Post ToModel(this PostDto d) => new()
    {
        Id = d.Id,
        AuthorId = d.AuthorId,
        CommunityId = d.CommunityId,
        Content = d.Content,
        ImagePath = d.ImagePath,
        CreatedAt = d.CreatedAt.ToLocalTime(),
        Visibility = d.Visibility,
        LikedBy = (d.LikedBy ?? []).ToHashSet(),
        CommentCount = d.CommentCount,
        ShareCount = d.ShareCount
    };

    public static Comment ToModel(this CommentDto d) => new()
    {
        Id = d.Id,
        PostId = d.PostId,
        AuthorId = d.AuthorId,
        Content = d.Content,
        CreatedAt = d.CreatedAt.ToLocalTime()
    };

    public static Community ToModel(this CommunityDto d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Description = d.Description,
        Category = d.Category,
        Country = d.Country ?? string.Empty,
        City = d.City ?? string.Empty,
        Visibility = d.Visibility,
        CoverImage = d.CoverImage,
        OwnerId = d.OwnerId,
        ModeratorIds = (d.ModeratorIds ?? []).ToHashSet(),
        CreatedAt = d.CreatedAt.ToLocalTime(),
        MemberIds = (d.MemberIds ?? []).ToHashSet()
    };

    public static Conversation ToModel(this ConversationDto d) => new(d.Id, d.ParticipantIds, d.CreatedAt.ToLocalTime());

    public static Message ToModel(this MessageDto d) => new()
    {
        Id = d.Id,
        ConversationId = d.ConversationId,
        SenderId = d.SenderId,
        Content = d.Content,
        SentAt = d.SentAt.ToLocalTime(),
        IsRead = d.IsRead,
        IsSimulated = d.IsSimulated
    };

    public static ConversationSummary ToModel(this ConversationSummaryDto d) =>
        new(d.Conversation.ToModel(), d.OtherParticipant.ToModel(), d.LastMessage?.ToModel(), d.UnreadCount);

    public static CommunityEvent ToModel(this EventDto d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        Description = d.Description,
        Category = d.Category,
        City = d.City,
        Country = d.Country ?? string.Empty,
        Address = d.Address,
        StartsAt = d.StartsAt.ToLocalTime(),
        EndsAt = d.EndsAt.ToLocalTime(),
        OrganizerId = d.OrganizerId,
        CommunityId = d.CommunityId,
        ImagePath = d.ImagePath,
        Capacity = d.Capacity,
        ParticipantIds = (d.ParticipantIds ?? []).ToHashSet()
    };

    public static Notification ToModel(this NotificationDto d) => new()
    {
        Id = d.Id,
        UserId = d.UserId,
        Category = d.Category,
        Title = d.Title,
        Body = d.Body,
        CreatedAt = d.CreatedAt.ToLocalTime(),
        IsRead = d.IsRead,
        Target = d.Target,
        TargetId = d.TargetId
    };
}
