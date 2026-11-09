using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Identity.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Application;

public sealed record BlockUserCommand(Guid UserId) : ICommand<bool>;

public sealed record UnblockUserCommand(Guid UserId) : ICommand<bool>;

/// <summary>Membres bloqués par le membre connecté, du plus récent au plus ancien.</summary>
public sealed record GetBlockedUsersQuery : IQuery<IReadOnlyList<UserDto>>;

public sealed class BlockHandlers(IIdentityDb db, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<BlockUserCommand, bool>,
    IRequestHandler<UnblockUserCommand, bool>,
    IRequestHandler<GetBlockedUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<bool> Handle(BlockUserCommand request, CancellationToken ct)
    {
        if (request.UserId == me.Id)
        {
            throw new DomainException("Vous ne pouvez pas vous bloquer vous-même.");
        }

        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, ct))
        {
            throw new NotFoundException("Membre");
        }

        if (!await db.Blocks.AnyAsync(b => b.BlockerId == me.Id && b.BlockedId == request.UserId, ct))
        {
            db.Blocks.Add(UserBlock.Create(me.Id, request.UserId, clock.GetUtcNow()));
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<bool> Handle(UnblockUserCommand request, CancellationToken ct)
    {
        await db.Blocks.Where(b => b.BlockerId == me.Id && b.BlockedId == request.UserId).ExecuteDeleteAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<UserDto>> Handle(GetBlockedUsersQuery request, CancellationToken ct)
    {
        var blocked = await db.Blocks.AsNoTracking().Where(b => b.BlockerId == me.Id)
            .OrderByDescending(b => b.CreatedAt).Select(b => b.BlockedId).ToListAsync(ct);
        var users = await db.Users.AsNoTracking().Where(u => blocked.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        return blocked.Where(users.ContainsKey).Select(id => users[id].ToDto()).ToList();
    }
}
