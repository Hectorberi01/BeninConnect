using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Application;

/// <summary>Suppression définitive du compte connecté, confirmée par le mot de passe.</summary>
public sealed record DeleteAccountCommand(string Password) : ICommand<bool>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Required(nameof(Password), Password, "Le mot de passe");
}

public sealed class DeleteAccountHandler(IIdentityDb db, IPasswordHasher hasher, IEventBus bus, ICurrentUser me)
    : IRequestHandler<DeleteAccountCommand, bool>
{
    public async Task<bool> Handle(DeleteAccountCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == me.Id, ct) ?? throw new NotFoundException("Membre");
        if (!hasher.Verify(user.PasswordHash, request.Password))
        {
            throw new DomainException("Mot de passe incorrect.");
        }

        var images = new[] { user.AvatarImage, user.CoverImage }.OfType<string>().ToList();
        db.Users.Remove(user);
        db.Blocks.RemoveRange(await db.Blocks.Where(b => b.BlockerId == user.Id || b.BlockedId == user.Id).ToListAsync(ct));
        await db.SaveChangesAsync(ct);

        // Les autres modules effacent publications, messages, adhésions, notifications...
        await bus.PublishAsync(new MemberDeleted(user.Id), ct);
        if (images.Count > 0)
        {
            await bus.PublishAsync(new ImagesReleased(images), ct);
        }

        return true;
    }
}
