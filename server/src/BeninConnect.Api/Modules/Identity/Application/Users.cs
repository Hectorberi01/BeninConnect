using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Application;

public sealed record GetUserQuery(Guid Id) : IQuery<UserDto>;

public sealed record GetUsersByIdsQuery(IReadOnlyList<Guid> Ids) : IQuery<IReadOnlyList<UserDto>>;

public sealed record SearchUsersQuery(string? Search) : IQuery<IReadOnlyList<UserDto>>;

public sealed record GetInterestsQuery : IQuery<IReadOnlyList<string>>;

public sealed record UpdateProfileCommand(
    string FirstName, string LastName, string Profession, string Country, string City, string Bio, IReadOnlyList<string> Interests,
    string? AvatarImage = null, string? CoverImage = null)
    : ICommand<UserDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Length(nameof(FirstName), FirstName, "Le prénom", 2, 40)
            .Concat(Rules.Length(nameof(LastName), LastName, "Le nom", 2, 40))
            .Concat(Rules.Required(nameof(Country), Country, "Le pays"))
            .Concat(Rules.Required(nameof(City), City, "La ville"))
            .Concat(Rules.ImagePath(nameof(AvatarImage), AvatarImage))
            .Concat(Rules.ImagePath(nameof(CoverImage), CoverImage))
            .Concat(Extra());

    private IEnumerable<(string, string)> Extra()
    {
        if ((Bio?.Length ?? 0) > 300)
        {
            yield return (nameof(Bio), "La biographie ne doit pas dépasser 300 caractères.");
        }

        if ((Interests ?? []).Except(IdentityCatalog.Interests).Any())
        {
            yield return (nameof(Interests), "Centre d'intérêt inconnu.");
        }
    }
}

public sealed class UserHandlers(IIdentityDb db, ICurrentUser me) :
    IRequestHandler<GetUserQuery, UserDto>,
    IRequestHandler<GetUsersByIdsQuery, IReadOnlyList<UserDto>>,
    IRequestHandler<SearchUsersQuery, IReadOnlyList<UserDto>>,
    IRequestHandler<GetInterestsQuery, IReadOnlyList<string>>,
    IRequestHandler<UpdateProfileCommand, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct) =>
        (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.Id, ct) ?? throw new NotFoundException("Membre")).ToDto();

    public async Task<IReadOnlyList<UserDto>> Handle(GetUsersByIdsQuery request, CancellationToken ct)
    {
        var ids = request.Ids.Distinct().Take(500).ToList();
        return (await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(ct)).Select(u => u.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<UserDto>> Handle(SearchUsersQuery request, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Where(u => u.IsEmailVerified);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim().ToLower();
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).ToLower().Contains(q)
                || u.City.ToLower().Contains(q)
                || u.Country.ToLower().Contains(q)
                || u.Profession.ToLower().Contains(q));
        }

        return (await query.OrderBy(u => u.FirstName).Take(100).ToListAsync(ct)).Select(u => u.ToDto()).ToList();
    }

    public Task<IReadOnlyList<string>> Handle(GetInterestsQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(IdentityCatalog.Interests);

    public async Task<UserDto> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([me.Id], ct) ?? throw new NotFoundException("Compte");
        user.UpdateProfile(request.FirstName, request.LastName, request.Profession ?? "", request.Country, request.City, request.Bio ?? "", request.Interests ?? []);
        user.UpdateImages(request.AvatarImage, request.CoverImage);
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }
}
