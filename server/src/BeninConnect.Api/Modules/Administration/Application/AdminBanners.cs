using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Administration.Domain;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Administration.Application;

/// <summary>Statut affiché dans la console : en ligne, programmée, terminée ou masquée.</summary>
public enum BannerStatus
{
    Live,
    Scheduled,
    Ended,
    Hidden
}

public sealed record BannerDto(
    Guid Id, string Title, string? Highlight, string? Subtitle, string? ImagePath, BannerLayout Layout, BannerTheme Theme,
    IReadOnlyList<BannerAction> Actions, bool IsActive, int Position, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt,
    BannerStatus Status, DateTimeOffset UpdatedAt, string UpdatedBy);

/// <summary>Bannière vue par l'application (seulement celles en ligne).</summary>
public sealed record AppBannerDto(
    Guid Id, string Title, string? Highlight, string? Subtitle, string? ImagePath, BannerLayout Layout, BannerTheme Theme, IReadOnlyList<BannerAction> Actions);

public sealed record BannerInput(
    string Title, string? Highlight, string? Subtitle, string? ImagePath, BannerLayout Layout, BannerTheme Theme,
    IReadOnlyList<BannerAction>? Actions, bool IsActive, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt)
{
    public BannerContent ToContent() => new(Title ?? "", Highlight, Subtitle, string.IsNullOrWhiteSpace(ImagePath) ? null : ImagePath, Layout, Theme,
        Actions ?? [], IsActive, StartsAt, EndsAt);
}

public sealed record GetAppBannersQuery : IQuery<IReadOnlyList<AppBannerDto>>;

public sealed record GetBannersQuery : IQuery<IReadOnlyList<BannerDto>>;

public sealed record CreateBannerCommand(BannerInput Input) : ICommand<BannerDto>;

public sealed record UpdateBannerCommand(Guid Id, BannerInput Input) : ICommand<BannerDto>;

public sealed record DeleteBannerCommand(Guid Id) : ICommand<bool>;

/// <summary>Ordre d'affichage : tous les identifiants, du premier au dernier.</summary>
public sealed record ReorderBannersCommand(IReadOnlyList<Guid> Ids) : ICommand<IReadOnlyList<BannerDto>>;

public sealed record UploadBannerImageCommand(Stream Content, long Length) : ICommand<BannerImageDto>;

public sealed record BannerImageDto(string Path);

public sealed class BannerHandlers(IAdministrationDb db, IAdminSession admin, IMediaModule media, IEventBus bus, TimeProvider clock) :
    IRequestHandler<GetAppBannersQuery, IReadOnlyList<AppBannerDto>>,
    IRequestHandler<GetBannersQuery, IReadOnlyList<BannerDto>>,
    IRequestHandler<CreateBannerCommand, BannerDto>,
    IRequestHandler<UpdateBannerCommand, BannerDto>,
    IRequestHandler<DeleteBannerCommand, bool>,
    IRequestHandler<ReorderBannersCommand, IReadOnlyList<BannerDto>>,
    IRequestHandler<UploadBannerImageCommand, BannerImageDto>
{
    public async Task<IReadOnlyList<AppBannerDto>> Handle(GetAppBannersQuery q, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var banners = await db.Banners.AsNoTracking()
            .Where(b => b.IsActive && (b.StartsAt == null || b.StartsAt <= now) && (b.EndsAt == null || b.EndsAt > now))
            .OrderBy(b => b.Position).Take(10).ToListAsync(ct);
        return banners.Select(b => new AppBannerDto(b.Id, b.Title, b.Highlight, b.Subtitle, b.ImagePath, b.Layout, b.Theme, b.Actions)).ToList();
    }

    public async Task<IReadOnlyList<BannerDto>> Handle(GetBannersQuery q, CancellationToken ct) => await ListAsync(ct);

    public async Task<BannerDto> Handle(CreateBannerCommand c, CancellationToken ct)
    {
        var me = await admin.GetAsync(ct);
        if (await db.Banners.CountAsync(ct) >= BannerRules.MaxBanners)
        {
            throw new DomainException($"{BannerRules.MaxBanners} bannières au plus : supprimez-en une avant d'en créer une autre.");
        }

        var position = (await db.Banners.MaxAsync(b => (int?)b.Position, ct) ?? -1) + 1;
        var banner = HomeBanner.Create(c.Input.ToContent(), position, me.DisplayName, clock.GetUtcNow());
        db.Banners.Add(banner);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Bannière créée", "Banner", banner.Id, banner.Title, ct);
        return ToDto(banner);
    }

    public async Task<BannerDto> Handle(UpdateBannerCommand c, CancellationToken ct)
    {
        var me = await admin.GetAsync(ct);
        var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == c.Id, ct) ?? throw new NotFoundException("Bannière");
        var released = banner.Update(c.Input.ToContent(), me.DisplayName, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Bannière modifiée", "Banner", banner.Id, $"{banner.Title}{(banner.IsActive ? "" : " (masquée)")}", ct);
        if (released is not null)
        {
            await bus.PublishAsync(new ImagesReleased([released]), ct);
        }

        return ToDto(banner);
    }

    public async Task<bool> Handle(DeleteBannerCommand c, CancellationToken ct)
    {
        await admin.GetAsync(ct);
        var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == c.Id, ct) ?? throw new NotFoundException("Bannière");
        db.Banners.Remove(banner);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Bannière supprimée", "Banner", banner.Id, banner.Title, ct);
        if (banner.ImagePath is { } image)
        {
            await bus.PublishAsync(new ImagesReleased([image]), ct);
        }

        return true;
    }

    public async Task<IReadOnlyList<BannerDto>> Handle(ReorderBannersCommand c, CancellationToken ct)
    {
        await admin.GetAsync(ct);
        var banners = await db.Banners.ToListAsync(ct);
        if (c.Ids.Count != banners.Count || c.Ids.Distinct().Count() != banners.Count || banners.Any(b => !c.Ids.Contains(b.Id)))
        {
            throw new DomainException("L'ordre doit contenir chaque bannière une seule fois.");
        }

        for (var i = 0; i < c.Ids.Count; i++)
        {
            banners.Single(b => b.Id == c.Ids[i]).MoveTo(i);
        }

        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Bannières réordonnées", "Banner", null, $"{banners.Count} bannière(s)", ct);
        return await ListAsync(ct);
    }

    public async Task<BannerImageDto> Handle(UploadBannerImageCommand c, CancellationToken ct)
    {
        await admin.GetAsync(ct);
        return new BannerImageDto(await media.SaveImageAsync(c.Content, c.Length, ct));
    }

    private async Task<IReadOnlyList<BannerDto>> ListAsync(CancellationToken ct) =>
        (await db.Banners.AsNoTracking().OrderBy(b => b.Position).ToListAsync(ct)).Select(ToDto).ToList();

    private BannerDto ToDto(HomeBanner b)
    {
        var now = clock.GetUtcNow();
        var status = !b.IsActive ? BannerStatus.Hidden
            : b.EndsAt is { } end && end <= now ? BannerStatus.Ended
            : b.StartsAt is { } start && start > now ? BannerStatus.Scheduled
            : BannerStatus.Live;
        return new BannerDto(b.Id, b.Title, b.Highlight, b.Subtitle, b.ImagePath, b.Layout, b.Theme, b.Actions, b.IsActive, b.Position,
            b.StartsAt, b.EndsAt, status, b.UpdatedAt, b.UpdatedBy);
    }
}
