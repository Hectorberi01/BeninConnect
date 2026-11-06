using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Notifications.Contracts;

/// <summary>
/// Notification libre demandée par un module (le module Notifications réagit aussi aux événements des autres modules).
/// Category : Social, Messages, Communities, Events, System. Target : None, Post, Conversation, Community, Event, Profile.
/// </summary>
public sealed record NotificationRequested(Guid UserId, string Category, string Title, string Body, string Target, Guid? TargetId) : IIntegrationEvent;
