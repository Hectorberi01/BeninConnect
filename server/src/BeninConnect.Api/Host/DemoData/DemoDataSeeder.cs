using System.Globalization;
using System.Text;
using BeninConnect.Modules.Administration.Domain;
using BeninConnect.Modules.Administration.Infrastructure.Persistence;
using BeninConnect.Modules.Communities.Domain;
using BeninConnect.Modules.Communities.Infrastructure.Persistence;
using BeninConnect.Modules.Events.Domain;
using BeninConnect.Modules.Events.Infrastructure.Persistence;
using BeninConnect.Modules.Feed.Domain;
using BeninConnect.Modules.Feed.Infrastructure.Persistence;
using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Domain;
using BeninConnect.Modules.Identity.Infrastructure.Persistence;
using BeninConnect.Modules.Messaging.Domain;
using BeninConnect.Modules.Messaging.Infrastructure.Persistence;
using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Modules.Moderation.Domain;
using BeninConnect.Modules.Moderation.Infrastructure.Persistence;
using BeninConnect.Modules.Notifications.Domain;
using BeninConnect.Modules.Notifications.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Host.DemoData;

/// <summary>
/// Jeu de démonstration identique à celui de l'application mobile (mêmes personnes, communautés, textes).
/// Outil de développement de l'hôte : il écrit dans la base de chaque module (seul endroit autorisé à tous les connaître).
/// </summary>
public sealed class DemoDataSeeder(
    IdentityDbContext identity, CommunitiesDbContext communitiesDb, FeedDbContext feed, EventsDbContext eventsDb,
    MessagingDbContext messaging, NotificationsDbContext notifications, ModerationDbContext moderation, AdministrationDbContext administration,
    IPasswordHasher hasher, TimeProvider clock, DemoSeedAccount? account = null)
{
    public const string DemoEmail = "demo@beninconnect.test";
    public const string DemoPassword = "Demo123!";
    public const string AdminPassword = "Admin2026!";

    /// <summary>Un compte de démonstration par rôle de la console d'administration.</summary>
    public static readonly (string Email, string Name, AdminRole Role)[] DemoAdmins =
    [
        ("superadmin@beninconnect.test", "Aïcha Super", AdminRole.SuperAdmin),
        ("admin@beninconnect.test", "Koffi Admin", AdminRole.Admin),
        ("moderation@beninconnect.test", "Nadia Modération", AdminRole.Moderator),
        ("compta@beninconnect.test", "Rodrigue Comptable", AdminRole.Accountant),
        ("dev@beninconnect.test", "Yao Dev", AdminRole.Developer)
    ];
    private const int Seed = 2026;

    public async Task<bool> SeedIfEmptyAsync(CancellationToken ct = default)
    {
        if (await identity.Users.AnyAsync(ct))
        {
            return false;
        }

        await SeedAsync(ct);
        return true;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        var random = new Random(Seed);
        var now = clock.GetUtcNow();
        var demo = account ?? DemoSeedAccount.Default;
        var passwordHash = hasher.Hash(demo.Password);

        var users = SeedUsers.All.Select((s, i) => User.CreateVerified(
            NextGuid(random), s.FirstName, s.LastName,
            i == 0 ? demo.Email : $"{Slug(s.FirstName)}.{Slug(s.LastName)}@exemple.test",
            passwordHash, s.Profession, s.Country, s.City, s.OriginCity, s.Bio, s.Interests,
            IdentityCatalog.AvatarColors[i % IdentityCatalog.AvatarColors.Length], $"Profile/cover_{i % 6 + 1:00}.jpg",
            now.AddDays(-random.Next(30, 900)))).ToList();
        identity.Users.AddRange(users);

        var communities = SeedCommunitiesInto(random, now, users);
        var posts = SeedPostsInto(random, now, users, communities);
        SeedCommentsInto(random, now, users, posts);
        var conversations = SeedConversationsInto(random, now, users);
        var events = SeedEventsInto(random, now, users, communities);
        SeedNotificationsInto(random, now, users, posts, conversations, events, communities.Values.ToList());
        SeedAdminsAndReports(now, users, posts, demo.SeedAdmins);

        await identity.SaveChangesAsync(ct);
        await communitiesDb.SaveChangesAsync(ct);
        await feed.SaveChangesAsync(ct);
        await eventsDb.SaveChangesAsync(ct);
        await messaging.SaveChangesAsync(ct);
        await notifications.SaveChangesAsync(ct);
        await moderation.SaveChangesAsync(ct);
        await administration.SaveChangesAsync(ct);
    }

    private void SeedAdminsAndReports(DateTimeOffset now, List<User> users, List<Post> posts, bool seedAdmins)
    {
        // Comptes de la console aux mots de passe publics : jamais en environnement de revue
        if (seedAdmins)
        {
            var adminHash = hasher.Hash(AdminPassword);
            foreach (var (email, name, role) in DemoAdmins)
            {
                administration.AdminUsers.Add(AdminUser.Create(Guid.NewGuid(), email, name, adminHash, role, now.AddDays(-120)));
            }
        }

        // File de modération : quelques signalements ouverts (aléatoire séparé).
        var random = new Random(Seed + 3);
        var comments = feed.Comments.Local.ToList();
        var reasons = Enum.GetValues<ReportReason>();
        string[] details =
        [
            "Lien suspect répété dans plusieurs publications.", "Propos blessants envers un membre.", "", "Annonce commerciale hors sujet.",
            "Information non vérifiée sur les démarches de visa.", "", "Message insistant reçu après un refus."
        ];

        for (var i = 0; i < 14; i++)
        {
            var reporter = users[random.Next(1, users.Count)];
            var (target, targetId, owner) = (i % 7) switch
            {
                0 or 1 or 2 or 3 => Pick(posts, p => (ReportTarget.Post, p.Id, p.AuthorId)),
                4 or 5 => Pick(comments, c => (ReportTarget.Comment, c.Id, c.AuthorId)),
                _ => Pick(users, u => (ReportTarget.User, u.Id, u.Id))
            };
            if (owner == reporter.Id)
            {
                continue;
            }

            var report = Report.Create(Guid.NewGuid(), reporter.Id, target, targetId, reasons[random.Next(reasons.Length)],
                details[random.Next(details.Length)], now.AddHours(-random.Next(1, 24 * 10)));
            moderation.Reports.Add(report);
        }

        (ReportTarget, Guid, Guid) Pick<T>(List<T> items, Func<T, (ReportTarget, Guid, Guid)> map) => map(items[random.Next(items.Count)]);
    }

    private Dictionary<string, Community> SeedCommunitiesInto(Random random, DateTimeOffset now, List<User> users)
    {
        var authorsByCommunity = SeedPosts.All.Where(p => p.Community is not null)
            .GroupBy(p => p.Community!).ToDictionary(g => g.Key, g => g.Select(p => p.Author).ToHashSet());

        var result = new Dictionary<string, Community>();
        for (var i = 0; i < SeedCommunities.All.Length; i++)
        {
            var s = SeedCommunities.All[i];
            var community = Community.Create(
                NextGuid(random), s.Name, s.Description, s.Category, s.Country, s.City, s.Visibility,
                $"Covers/community_{i + 1:00}.jpg", users[s.OwnerIndex].Id, now.AddDays(-random.Next(60, 1200)));

            var members = new HashSet<int>(s.Members);
            if (authorsByCommunity.TryGetValue(s.Key, out var authors))
            {
                members.UnionWith(authors);
            }

            if (s.DemoIsMember)
            {
                members.Add(0);
            }
            else
            {
                members.Remove(0);
            }

            var moderator = s.Members.FirstOrDefault(m => m != s.OwnerIndex && m != 0);
            foreach (var m in members.Where(m => m != s.OwnerIndex))
            {
                community.AddMember(users[m].Id, m == moderator ? CommunityRole.Moderator : CommunityRole.Member, now.AddDays(-random.Next(1, 300)));
            }

            communitiesDb.Communities.Add(community);
            result[s.Key] = community;
        }

        result["parents"].AddJoinRequest(users[0].Id, now.AddDays(-2));
        return result;
    }

    private List<Post> SeedPostsInto(Random random, DateTimeOffset now, List<User> users, Dictionary<string, Community> communities)
    {
        var posts = new List<Post>();
        foreach (var s in SeedPosts.All)
        {
            var community = s.Community is null ? null : communities[s.Community];
            var post = Post.Create(
                NextGuid(random), users[s.Author].Id, community?.Id, s.Text,
                s.Image > 0 ? $"Posts/post_{s.Image:00}.jpg" : null,
                community?.IsPrivate == true ? PostVisibility.CommunityMembers : PostVisibility.Public,
                now);
            post.SeedCreatedAt(now.AddHours(-s.HoursAgo).AddMinutes(-random.Next(0, 50)));

            var likers = users.Where((_, index) => index != s.Author && index != 0)
                .OrderBy(_ => random.Next()).Take(random.Next(0, 24)).ToList();
            foreach (var liker in likers)
            {
                post.ToggleLike(liker.Id, post.CreatedAt);
            }

            if (s.Author != 0 && random.NextDouble() < 0.25)
            {
                post.ToggleLike(users[0].Id, post.CreatedAt);
            }

            post.SeedCounters(0, random.Next(0, 9));
            posts.Add(post);
        }

        feed.Posts.AddRange(posts);
        return posts;
    }

    private void SeedCommentsInto(Random random, DateTimeOffset now, List<User> users, List<Post> posts)
    {
        var reactions = new Random(Seed + 1);
        var ordered = posts.OrderByDescending(p => p.CreatedAt).ToList();
        var counts = new int[ordered.Count];
        for (var i = 0; i < 200; i++)
        {
            counts[Math.Min((int)Math.Floor(Math.Pow(random.NextDouble(), 1.6) * ordered.Count), ordered.Count - 1)]++;
        }

        for (var p = 0; p < ordered.Count; p++)
        {
            var post = ordered[p];
            var minutes = Math.Max(1, (now - post.CreatedAt).TotalMinutes - 1);
            for (var c = 0; c < counts[p]; c++)
            {
                User author;
                do
                {
                    author = users[random.Next(users.Count)];
                }
                while (author.Id == post.AuthorId);

                var comment = post.AddComment(NextGuid(random), author.Id, SeedComments.Pool[random.Next(SeedComments.Pool.Length)],
                    post.CreatedAt.AddMinutes(random.NextDouble() * minutes));
                SeedReactions(reactions, comment.React, users.Where(u => u.Id != author.Id).ToList(), comment.CreatedAt, now, 0.3, 3);
                feed.Comments.Add(comment);
            }
        }
    }

    /// <summary>Quelques réactions de démonstration (aléatoire séparé : le reste du jeu ne change pas).</summary>
    private static void SeedReactions(Random reactions, Func<Guid, string?, DateTimeOffset, bool> react, List<User> candidates,
        DateTimeOffset from, DateTimeOffset now, double probability, int max)
    {
        if (candidates.Count == 0 || reactions.NextDouble() >= probability)
        {
            return;
        }

        var count = reactions.Next(1, max + 1);
        foreach (var user in candidates.OrderBy(_ => reactions.Next()).Take(count))
        {
            var at = from.AddMinutes(reactions.NextDouble() * Math.Max(1, (now - from).TotalMinutes - 1));
            react(user.Id, Reactions.Allowed[reactions.Next(Reactions.Allowed.Count)], at);
        }
    }

    private List<Conversation> SeedConversationsInto(Random random, DateTimeOffset now, List<User> users)
    {
        var demo = users[0];
        var reactions = new Random(Seed + 2);
        var result = new List<Conversation>();
        foreach (var s in SeedConversations.All)
        {
            var other = users[s.OtherUser];
            var times = new DateTimeOffset[s.Lines.Length];
            times[^1] = now.AddHours(-s.HoursAgo).AddMinutes(-random.Next(0, 40));
            for (var i = s.Lines.Length - 2; i >= 0; i--)
            {
                times[i] = times[i + 1].AddMinutes(-random.Next(2, 95));
            }

            var conversation = Conversation.CreateDirect(NextGuid(random), demo.Id, other.Id, times[0].AddMinutes(-1));
            messaging.Conversations.Add(conversation);
            result.Add(conversation);

            var unreadRemaining = s.UnreadCount;
            for (var i = s.Lines.Length - 1; i >= 0; i--)
            {
                var line = s.Lines[i];
                var fromDemo = line.StartsWith('>');
                var isRead = fromDemo || unreadRemaining <= 0;
                if (!fromDemo && unreadRemaining > 0)
                {
                    unreadRemaining--;
                }

                var message = Message.Create(NextGuid(random), conversation.Id, fromDemo ? demo.Id : other.Id,
                    fromDemo ? line[1..].Trim() : line, times[i], isRead: isRead);
                SeedReactions(reactions, message.React, [fromDemo ? other : demo], message.SentAt, now, 0.15, 1);
                messaging.Messages.Add(message);
            }
        }

        return result;
    }

    private List<CommunityEvent> SeedEventsInto(Random random, DateTimeOffset now, List<User> users, Dictionary<string, Community> communities)
    {
        var demo = users[0];
        var paris = TimeSpan.FromHours(1);
        var result = new List<CommunityEvent>();
        foreach (var s in SeedEvents.All)
        {
            var localDay = now.ToOffset(paris).Date.AddDays(s.DaysFromNow);
            var starts = new DateTimeOffset(localDay.AddHours(s.StartHour), paris).ToUniversalTime();
            var organizer = users[s.Organizer];

            var created = CommunityEvent.Create(
                NextGuid(random), s.Title, s.Description, s.Category, s.City, s.Country, s.Address,
                starts, starts.AddHours(s.DurationHours), organizer.Id,
                s.Community is null ? null : communities[s.Community].Id,
                $"Events/event_{s.Image % 10 + 1:00}.jpg", s.Capacity, now, allowPast: true);

            var target = Math.Min(Math.Min(s.Participants, users.Count - 2), s.Capacity ?? int.MaxValue);
            foreach (var u in users.Where(u => u.Id != demo.Id && u.Id != organizer.Id).OrderBy(_ => random.Next()).Take(Math.Max(0, target - 1)))
            {
                created.ToggleParticipation(u.Id, now);
            }

            if (s.DemoParticipates && !created.IsFull)
            {
                created.ToggleParticipation(demo.Id, now);
            }

            eventsDb.Events.Add(created);
            result.Add(created);
        }

        return result;
    }

    private void SeedNotificationsInto(Random random, DateTimeOffset now, List<User> users, List<Post> posts,
        List<Conversation> conversations, List<CommunityEvent> events, List<Community> communities)
    {
        var demo = users[0];
        var myPosts = posts.Where(p => p.AuthorId == demo.Id).OrderByDescending(p => p.CreatedAt).ToList();
        var others = users.Skip(1).ToList();
        var myCommunities = communities.Where(c => c.IsMember(demo.Id)).ToList();
        var minutes = 6;

        void Add(NotificationCategory category, string title, string body, NotificationTarget target, Guid? id, bool unread)
        {
            notifications.Notifications.Add(Notification.Create(NextGuid(random), demo.Id, category, title, body, now.AddMinutes(-minutes), target, id, !unread));
            minutes += random.Next(20, 460);
        }

        for (var i = 0; i < 8; i++)
        {
            var post = myPosts[i % myPosts.Count];
            var actor = others[random.Next(others.Count)];
            Add(NotificationCategory.Social,
                i % 2 == 0 ? $"{actor.DisplayName} a aimé votre publication" : $"{actor.DisplayName} a commenté votre publication",
                i % 2 == 0 ? Excerpt(post.Content) : $"« {SeedComments.Pool[random.Next(SeedComments.Pool.Length)]} »",
                NotificationTarget.Post, post.Id, i < 3);
        }

        for (var i = 0; i < 6; i++)
        {
            var conversation = conversations[i];
            var other = users.First(u => u.Id == conversation.Participants.Last().UserId);
            Add(NotificationCategory.Messages, $"Nouveau message de {other.DisplayName}", "Ouvrez la conversation pour lire le message.",
                NotificationTarget.Conversation, conversation.Id, i < 2);
        }

        for (var i = 0; i < 6; i++)
        {
            var community = myCommunities[i % myCommunities.Count];
            var actor = others[random.Next(others.Count)];
            Add(NotificationCategory.Communities,
                i % 2 == 0 ? $"Nouvelle publication dans {community.Name}" : $"{actor.DisplayName} a rejoint {community.Name}",
                i % 2 == 0 ? $"{actor.DisplayName} a partagé une publication." : "Souhaitez-lui la bienvenue !",
                NotificationTarget.Community, community.Id, i == 0);
        }

        foreach (var (ev, i) in events.OrderBy(e => e.StartsAt).Take(6).Select((e, i) => (e, i)))
        {
            Add(NotificationCategory.Events, i % 2 == 0 ? $"Rappel : {ev.Title}" : $"Nouvel événement : {ev.Title}",
                $"{ev.City} · {ev.StartsAt.ToOffset(TimeSpan.FromHours(1)).ToString("dd/MM 'à' HH:mm", CultureInfo.InvariantCulture)}",
                NotificationTarget.Event, ev.Id, i < 2);
        }

        Add(NotificationCategory.System, "Complétez votre profil", "Ajoutez vos centres d'intérêt pour recevoir de meilleures suggestions.", NotificationTarget.Profile, demo.Id, true);
        Add(NotificationCategory.System, "Mode démonstration", "Toutes les données de cette version sont fictives.", NotificationTarget.None, null, false);
        Add(NotificationCategory.System, "Nouvelles communautés à découvrir", "Trois communautés correspondent à vos centres d'intérêt.", NotificationTarget.None, null, false);
        Add(NotificationCategory.System, "Bienvenue sur BéninConnect", "Connectez-vous à votre communauté, partout dans le monde.", NotificationTarget.None, null, false);
    }

    private static Guid NextGuid(Random random)
    {
        var bytes = new byte[16];
        random.NextBytes(bytes);
        return new Guid(bytes);
    }

    private static string Excerpt(string text) => text.Length <= 80 ? text : text[..77].TrimEnd() + "…";

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterLower(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

/// <summary>
/// Compte principal du jeu de démonstration (index 0) ; mot de passe commun à tous les membres de démo.
/// Environnement de revue : compte donné à Apple / Google, sans comptes de console.
/// </summary>
public sealed record DemoSeedAccount(string Email, string Password, bool SeedAdmins)
{
    public static DemoSeedAccount Default { get; } = new(DemoDataSeeder.DemoEmail, DemoDataSeeder.DemoPassword, true);
}
