using BeninConnect.Modules.Messaging.Application;
using BeninConnect.Modules.Messaging.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Infrastructure.Persistence;

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : ModuleDbContext(options, MessagingDbContext.SchemaName), IMessagingDb
{
    public const string SchemaName = "messaging";

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Conversation>(e =>
        {
            e.ToTable("conversations");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Participants).WithOne().HasForeignKey(p => p.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<ConversationParticipant>(e =>
        {
            e.ToTable("participants");
            e.HasKey(x => new { x.ConversationId, x.UserId });
            e.HasIndex(x => x.UserId);
        });

        b.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasMaxLength(Message.MaxLength);
            e.HasIndex(x => new { x.ConversationId, x.SentAt });
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Reactions).WithOne().HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Reactions).UsePropertyAccessMode(PropertyAccessMode.Field);
            // Pièce jointe facultative : colonnes Attachment_* de la table messages
            e.OwnsOne(x => x.Attachment, a =>
            {
                a.Property(x => x.Name).HasMaxLength(64);
                a.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
                a.Property(x => x.FileName).HasMaxLength(MessageAttachment.MaxFileNameLength);
                a.Property(x => x.ContentType).HasMaxLength(64);
            });
        });

        b.Entity<MessageReaction>(e =>
        {
            e.ToTable("message_reactions");
            e.HasKey(x => new { x.MessageId, x.UserId });
            e.Property(x => x.Emoji).HasMaxLength(16);
        });
    }
}
