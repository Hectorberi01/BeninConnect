using System.Globalization;
using System.Text;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Messaging.Models;
using BeninConnect.App.Features.Notifications.Models;
using BeninConnect.App.MockData.Repositories;
using BeninConnect.App.MockData.Seed;
using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.MockData.Factories;

/// <summary>
/// Construit le jeu de données initial. Déterministe (graine fixe) : mêmes identifiants et mêmes contenus à chaque lancement.
/// Les dates sont calculées par rapport à « maintenant » pour que le fil et les événements restent d'actualité.
/// </summary>
public static class MockDataFactory
{
    public const int Seed = 2026;
    public const int CommentCount = 200;
    public const int NotificationCount = 30;

    public static MockDataStore Create(DateTimeOffset now)
    {
        var random = new Random(Seed);
        var store = new MockDataStore();

        var users = CreateUsers(store, random, now);
        var communities = CreateCommunities(store, random, now, users);
        CreatePosts(store, random, now, users, communities);
        CreateComments(store, random, now, users);
        CreateConversations(store, random, now, users);
        CreateEvents(store, random, now, users, communities);
        CreateNotifications(store, random, now, users);

        return store;
    }

    private static Guid NextGuid(Random random)
    {
        var bytes = new byte[16];
        random.NextBytes(bytes);
        return new Guid(bytes);
    }

    private static List<User> CreateUsers(MockDataStore store, Random random, DateTimeOffset now)
    {
        var users = new List<User>();
        for (var i = 0; i < SeedUsers.All.Length; i++)
        {
            var seed = SeedUsers.All[i];
            var email = i == 0
                ? MockAuthService.DemoEmail
                : $"{Slug(seed.FirstName)}.{Slug(seed.LastName)}@exemple.test";

            users.Add(new User
            {
                Id = NextGuid(random),
                FirstName = seed.FirstName,
                LastName = seed.LastName,
                Email = email,
                Profession = seed.Profession,
                City = seed.City,
                Country = seed.Country,
                OriginCity = seed.OriginCity,
                Bio = seed.Bio,
                Interests = seed.Interests,
                AvatarColor = SeedUsers.AvatarColors[i % SeedUsers.AvatarColors.Length],
                CoverImage = $"Profile/cover_{i % 6 + 1:00}.jpg",
                JoinedAt = now.AddDays(-random.Next(30, 900))
            });
        }

        store.Users.AddRange(users);
        store.DemoUserId = users[0].Id;
        store.DemoCredentials[MockAuthService.DemoEmail] = MockAuthService.DemoPassword;
        return users;
    }

    private static Dictionary<string, Guid> CreateCommunities(MockDataStore store, Random random, DateTimeOffset now, List<User> users)
    {
        var keys = new Dictionary<string, Guid>();

        // Les auteurs de publications d'une communauté en sont membres.
        var authorsByCommunity = SeedPosts.All
            .Where(p => p.Community is not null)
            .GroupBy(p => p.Community!)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Author).ToHashSet());

        for (var i = 0; i < SeedCommunities.All.Length; i++)
        {
            var seed = SeedCommunities.All[i];
            var memberIndexes = new HashSet<int>(seed.Members) { seed.OwnerIndex };
            if (authorsByCommunity.TryGetValue(seed.Key, out var authors))
            {
                memberIndexes.UnionWith(authors);
            }

            if (seed.DemoIsMember)
            {
                memberIndexes.Add(0);
            }
            else
            {
                memberIndexes.Remove(0);
            }

            var moderator = seed.Members.FirstOrDefault(m => m != seed.OwnerIndex && m != 0);

            var community = new Community
            {
                Id = NextGuid(random),
                Name = seed.Name,
                Description = seed.Description,
                Category = seed.Category,
                City = seed.City,
                Country = seed.Country,
                Visibility = seed.Visibility,
                CoverImage = $"Covers/community_{i + 1:00}.jpg",
                OwnerId = users[seed.OwnerIndex].Id,
                ModeratorIds = new HashSet<Guid> { users[moderator].Id },
                CreatedAt = now.AddDays(-random.Next(60, 1200)),
                MemberIds = memberIndexes.Select(m => users[m].Id).ToHashSet()
            };

            store.Communities.Add(community);
            keys[seed.Key] = community.Id;
        }

        // Demande d'adhésion en attente pour illustrer l'état « Demande envoyée ».
        var parents = store.Communities.First(c => c.Id == keys["parents"]);
        store.JoinRequests.Add(new JoinRequest(NextGuid(random), parents.Id, users[0].Id, now.AddDays(-2)));

        return keys;
    }

    private static void CreatePosts(MockDataStore store, Random random, DateTimeOffset now, List<User> users, Dictionary<string, Guid> communities)
    {
        foreach (var seed in SeedPosts.All)
        {
            Guid? communityId = seed.Community is null ? null : communities[seed.Community];
            var isPrivate = communityId is { } cid && store.Communities.First(c => c.Id == cid).IsPrivate;

            var likeCount = random.Next(0, 24);
            var likes = users
                .Where((u, index) => index != seed.Author && index != 0)
                .OrderBy(_ => random.Next())
                .Take(likeCount)
                .Select(u => u.Id)
                .ToHashSet();

            if (seed.Author != 0 && random.NextDouble() < 0.25)
            {
                likes.Add(users[0].Id);
            }

            store.Posts.Add(new Post
            {
                Id = NextGuid(random),
                AuthorId = users[seed.Author].Id,
                CommunityId = communityId,
                Content = seed.Text,
                ImagePath = seed.Image > 0 ? $"Posts/post_{seed.Image:00}.jpg" : null,
                CreatedAt = now.AddHours(-seed.HoursAgo).AddMinutes(-random.Next(0, 50)),
                Visibility = isPrivate ? PostVisibility.CommunityMembers : PostVisibility.Public,
                LikedBy = likes,
                ShareCount = random.Next(0, 9)
            });
        }
    }

    private static void CreateComments(MockDataStore store, Random random, DateTimeOffset now, List<User> users)
    {
        // Répartition pondérée : les publications récentes reçoivent davantage de commentaires.
        var posts = store.Posts.OrderByDescending(p => p.CreatedAt).ToList();
        var counts = new int[posts.Count];

        for (var i = 0; i < CommentCount; i++)
        {
            var index = (int)Math.Floor(Math.Pow(random.NextDouble(), 1.6) * posts.Count);
            counts[Math.Min(index, posts.Count - 1)]++;
        }

        for (var p = 0; p < posts.Count; p++)
        {
            var post = posts[p];
            var available = (now - post.CreatedAt).TotalMinutes;
            for (var c = 0; c < counts[p]; c++)
            {
                User author;
                do
                {
                    author = users[random.Next(users.Count)];
                }
                while (author.Id == post.AuthorId);

                store.Comments.Add(new Comment
                {
                    Id = NextGuid(random),
                    PostId = post.Id,
                    AuthorId = author.Id,
                    Content = SeedComments.Pool[random.Next(SeedComments.Pool.Length)],
                    CreatedAt = post.CreatedAt.AddMinutes(random.NextDouble() * Math.Max(1, available - 1))
                });
            }

            var index = store.Posts.FindIndex(x => x.Id == post.Id);
            store.Posts[index] = post with { CommentCount = counts[p] };
        }
    }

    private static void CreateConversations(MockDataStore store, Random random, DateTimeOffset now, List<User> users)
    {
        var demo = users[0];
        foreach (var seed in SeedConversations.All)
        {
            var other = users[seed.OtherUser];
            var lastAt = now.AddHours(-seed.HoursAgo).AddMinutes(-random.Next(0, 40));

            // On remonte le temps à partir du dernier message.
            var times = new DateTimeOffset[seed.Lines.Length];
            times[^1] = lastAt;
            for (var i = seed.Lines.Length - 2; i >= 0; i--)
            {
                times[i] = times[i + 1].AddMinutes(-random.Next(2, 95));
            }

            var conversation = new Conversation(NextGuid(random), [demo.Id, other.Id], times[0].AddMinutes(-1));
            store.Conversations.Add(conversation);

            var unreadRemaining = seed.UnreadCount;
            var messages = new List<Message>();
            for (var i = seed.Lines.Length - 1; i >= 0; i--)
            {
                var line = seed.Lines[i];
                var fromDemo = line.StartsWith('>');
                var isRead = fromDemo || unreadRemaining <= 0;
                if (!fromDemo && unreadRemaining > 0)
                {
                    unreadRemaining--;
                }

                messages.Add(new Message
                {
                    Id = NextGuid(random),
                    ConversationId = conversation.Id,
                    SenderId = fromDemo ? demo.Id : other.Id,
                    Content = fromDemo ? line[1..].Trim() : line,
                    SentAt = times[i],
                    IsRead = isRead
                });
            }

            store.Messages.AddRange(messages.OrderBy(m => m.SentAt));
        }
    }

    private static void CreateEvents(MockDataStore store, Random random, DateTimeOffset now, List<User> users, Dictionary<string, Guid> communities)
    {
        var demo = users[0];
        foreach (var seed in SeedEvents.All)
        {
            var day = now.Date.AddDays(seed.DaysFromNow);
            var starts = new DateTimeOffset(day.AddHours(seed.StartHour), now.Offset);
            var organizer = users[seed.Organizer];

            var max = seed.Capacity ?? int.MaxValue;
            var target = Math.Min(Math.Min(seed.Participants, users.Count - 2), max);
            var participants = users
                .Where(u => u.Id != demo.Id && u.Id != organizer.Id)
                .OrderBy(_ => random.Next())
                .Take(Math.Max(0, target - 1))
                .Select(u => u.Id)
                .ToHashSet();
            participants.Add(organizer.Id);

            if (seed.DemoParticipates)
            {
                participants.Add(demo.Id);
            }

            store.Events.Add(new CommunityEvent
            {
                Id = NextGuid(random),
                Title = seed.Title,
                Description = seed.Description,
                Category = seed.Category,
                City = seed.City,
                Country = seed.Country,
                Address = seed.Address,
                StartsAt = starts,
                EndsAt = starts.AddHours(seed.DurationHours),
                OrganizerId = organizer.Id,
                CommunityId = seed.Community is null ? null : communities[seed.Community],
                ImagePath = $"Events/event_{seed.Image % 10 + 1:00}.jpg",
                Capacity = seed.Capacity,
                ParticipantIds = participants
            });
        }
    }

    private static void CreateNotifications(MockDataStore store, Random random, DateTimeOffset now, List<User> users)
    {
        var demo = users[0];
        var myPosts = store.Posts.Where(p => p.AuthorId == demo.Id).OrderByDescending(p => p.CreatedAt).ToList();
        var others = users.Skip(1).ToList();
        var conversations = store.Conversations.ToList();
        var events = store.Events.OrderBy(e => e.StartsAt).ToList();
        var myCommunities = store.Communities.Where(c => c.MemberIds.Contains(demo.Id)).ToList();

        var list = new List<Notification>();
        var minutes = 6;

        void Add(NotificationCategory category, string title, string body, NotificationTarget target, Guid? targetId, bool unread)
        {
            list.Add(new Notification
            {
                Id = NextGuid(random),
                UserId = demo.Id,
                Category = category,
                Title = title,
                Body = body,
                CreatedAt = now.AddMinutes(-minutes),
                IsRead = !unread,
                Target = target,
                TargetId = targetId
            });
            minutes += random.Next(20, 460);
        }

        for (var i = 0; i < 8; i++)
        {
            var post = myPosts[i % myPosts.Count];
            var actor = others[random.Next(others.Count)];
            if (i % 2 == 0)
            {
                Add(NotificationCategory.Social, $"{actor.DisplayName} a aimé votre publication", Excerpt(post.Content), NotificationTarget.Post, post.Id, i < 3);
            }
            else
            {
                Add(NotificationCategory.Social, $"{actor.DisplayName} a commenté votre publication",
                    $"« {SeedComments.Pool[random.Next(SeedComments.Pool.Length)]} »", NotificationTarget.Post, post.Id, i < 3);
            }
        }

        for (var i = 0; i < 6; i++)
        {
            var conversation = conversations[i];
            var other = users.First(u => u.Id == conversation.ParticipantIds[1]);
            var last = store.Messages.Where(m => m.ConversationId == conversation.Id && m.SenderId == other.Id).MaxBy(m => m.SentAt);
            Add(NotificationCategory.Messages, $"Nouveau message de {other.DisplayName}", Excerpt(last?.Content ?? ""), NotificationTarget.Conversation, conversation.Id, i < 2);
        }

        for (var i = 0; i < 6; i++)
        {
            var community = myCommunities[i % myCommunities.Count];
            var actor = others[random.Next(others.Count)];
            if (i % 2 == 0)
            {
                Add(NotificationCategory.Communities, $"Nouvelle publication dans {community.Name}", $"{actor.DisplayName} a partagé une publication.", NotificationTarget.Community, community.Id, i < 2);
            }
            else
            {
                Add(NotificationCategory.Communities, $"{actor.DisplayName} a rejoint {community.Name}", "Souhaitez-lui la bienvenue !", NotificationTarget.Community, community.Id, false);
            }
        }

        for (var i = 0; i < 6; i++)
        {
            var ev = events[i];
            var title = i % 2 == 0 ? $"Rappel : {ev.Title}" : $"Nouvel événement : {ev.Title}";
            Add(NotificationCategory.Events, title, $"{ev.City} · {ev.StartsAt.Day}/{ev.StartsAt.Month:00} à {ev.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture)}", NotificationTarget.Event, ev.Id, i < 2);
        }

        Add(NotificationCategory.System, "Complétez votre profil", "Ajoutez vos centres d'intérêt pour recevoir de meilleures suggestions.", NotificationTarget.Profile, demo.Id, true);
        Add(NotificationCategory.System, "Mode démonstration", "Toutes les données de cette version sont fictives et restent sur votre appareil.", NotificationTarget.None, null, false);
        Add(NotificationCategory.System, "Nouvelles communautés à découvrir", "Trois communautés correspondent à vos centres d'intérêt.", NotificationTarget.None, null, false);
        Add(NotificationCategory.System, "Bienvenue sur BéninConnect", "Connectez-vous à votre communauté, partout dans le monde.", NotificationTarget.None, null, false);

        store.Notifications.AddRange(list.Take(NotificationCount));
    }

    private static string Excerpt(string text) => text.Length <= 80 ? text : text[..77].TrimEnd() + "…";

    private static string Slug(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (char.IsAsciiLetterLower(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
