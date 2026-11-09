namespace BeninConnect.Modules.Identity.Domain;

/// <summary>Un membre en bloque un autre : plus de contenu ni de message entre eux, dans les deux sens.</summary>
public sealed class UserBlock
{
    private UserBlock()
    {
    }

    public Guid BlockerId { get; private set; }
    public Guid BlockedId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static UserBlock Create(Guid blockerId, Guid blockedId, DateTimeOffset now) =>
        new() { BlockerId = blockerId, BlockedId = blockedId, CreatedAt = now };
}
