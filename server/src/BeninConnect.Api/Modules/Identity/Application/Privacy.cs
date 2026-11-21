using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Identity.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Application;

public sealed record GetPrivacyQuery : IQuery<PrivacySettingsDto>;

public sealed record UpdatePrivacyCommand(bool PrivateProfile, bool ShowCity, bool AllowMessages) : ICommand<PrivacySettingsDto>;

public sealed class PrivacyHandlers(IIdentityDb db, ICurrentUser me) :
    IRequestHandler<GetPrivacyQuery, PrivacySettingsDto>,
    IRequestHandler<UpdatePrivacyCommand, PrivacySettingsDto>
{
    public async Task<PrivacySettingsDto> Handle(GetPrivacyQuery request, CancellationToken ct) =>
        (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == me.Id, ct) ?? throw new NotFoundException("Compte")).Privacy();

    public async Task<PrivacySettingsDto> Handle(UpdatePrivacyCommand request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([me.Id], ct) ?? throw new NotFoundException("Compte");
        user.UpdatePrivacy(request.PrivateProfile, request.ShowCity, request.AllowMessages);
        await db.SaveChangesAsync(ct);
        return user.Privacy();
    }
}

/// <summary>Profils vus par un autre membre : email jamais visible, ville et profil privé respectés.</summary>
public sealed class UserViews(IEnumerable<IContactDirectory> directories)
{
    public async Task<IReadOnlyList<UserDto>> ForAsync(Guid? viewerId, IReadOnlyCollection<User> users, CancellationToken ct)
    {
        var restricted = users.Where(u => u.PrivateProfile && u.Id != viewerId).Select(u => u.Id).ToList();
        var contacts = new HashSet<Guid>();
        if (viewerId is { } viewer && restricted.Count > 0)
        {
            foreach (var directory in directories)
            {
                contacts.UnionWith(await directory.ContactsAmongAsync(viewer, restricted, ct));
            }
        }

        return users.Select(u => View(u, viewerId, contacts)).ToList();
    }

    public async Task<UserDto> ForAsync(Guid? viewerId, User user, CancellationToken ct) => (await ForAsync(viewerId, [user], ct))[0];

    private static UserDto View(User u, Guid? viewerId, HashSet<Guid> contacts)
    {
        var dto = u.ToDto();
        if (u.Id == viewerId)
        {
            return dto;
        }

        dto = dto with { Email = string.Empty, City = u.ShowCity ? u.City : string.Empty };
        return u.PrivateProfile && !contacts.Contains(u.Id)
            ? dto with { Profession = string.Empty, City = string.Empty, OriginCity = string.Empty, Bio = string.Empty, Interests = [] }
            : dto;
    }
}

public static class PrivacyMapping
{
    public static PrivacySettingsDto Privacy(this User u) => new(u.PrivateProfile, u.ShowCity, u.AllowMessages);
}
