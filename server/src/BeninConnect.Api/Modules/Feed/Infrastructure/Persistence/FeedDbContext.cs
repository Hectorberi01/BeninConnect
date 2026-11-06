using BeninConnect.Modules.Feed.Application;
using BeninConnect.Modules.Feed.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Feed.Infrastructure.Persistence;

/// <summary>AuthorId, UserId, CommunityId : identifiants d'autres modules, sans clé étrangère.</summary>
public sealed class FeedDbContext(DbContextOptions<FeedDbContext> options) : ModuleDbContext(options, FeedDbContext.SchemaName), IFeedDb
{
    public const string SchemaName = "feed";

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Post>(e =>
        {
            e.ToTable("posts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasMaxLength(Post.MaxLength);
            e.Property(x => x.HiddenReason).HasMaxLength(200);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.AuthorId);
            e.HasIndex(x => x.CommunityId);
            e.HasMany(x => x.Likes).WithOne().HasForeignKey(l => l.PostId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Likes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<PostLike>(e =>
        {
            e.ToTable("post_likes");
            e.HasKey(x => new { x.PostId, x.UserId });
        });

        b.Entity<Comment>(e =>
        {
            e.ToTable("comments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasMaxLength(Comment.MaxLength);
            e.HasIndex(x => x.PostId);
            e.HasIndex(x => x.AuthorId);
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Reactions).WithOne().HasForeignKey(r => r.CommentId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Reactions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<CommentReaction>(e =>
        {
            e.ToTable("comment_reactions");
            e.HasKey(x => new { x.CommentId, x.UserId });
            e.Property(x => x.Emoji).HasMaxLength(16);
        });
    }
}
