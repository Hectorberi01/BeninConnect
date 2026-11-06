using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Communities.ViewModels;

public sealed class CommunityItemFactory(
    ICommunityRepository communities,
    INavigationService navigation,
    IToastService toast,
    IDialogService dialogs,
    ISessionService session,
    IMessenger messenger)
{
    internal ICommunityRepository Communities => communities;
    internal INavigationService Navigation => navigation;
    internal IToastService Toast => toast;
    internal IDialogService Dialogs => dialogs;
    internal ISessionService Session => session;
    internal IMessenger Messenger => messenger;

    public CommunityItemViewModel Create(Community community, IReadOnlySet<Guid> pending) =>
        new(community, community.StatusFor(session.CurrentUserId, pending), this);

    public async Task<IReadOnlyList<CommunityItemViewModel>> CreateAsync(IEnumerable<Community> items, CancellationToken cancellationToken = default)
    {
        var pending = await communities.GetPendingRequestsAsync(session.CurrentUserId, cancellationToken);
        return items.Select(c => Create(c, pending)).ToList();
    }
}

/// <summary>Carte de communauté avec gestion de l'adhésion (rejoindre, quitter, demande en attente).</summary>
public sealed partial class CommunityItemViewModel : ObservableObject
{
    private readonly CommunityItemFactory _ctx;

    internal CommunityItemViewModel(Community community, MembershipStatus status, CommunityItemFactory context)
    {
        _ctx = context;
        _community = community;
        _status = status;

        context.Messenger.Register<CommunityItemViewModel, CommunityChangedMessage>(this, static (r, m) =>
        {
            if (m.Community.Id == r.Id)
            {
                r.Community = m.Community;
            }
        });

        context.Messenger.Register<CommunityItemViewModel, MembershipChangedMessage>(this, static (r, m) =>
        {
            if (m.CommunityId == r.Id && r._ctx.Session.CurrentUser?.Id == m.UserId)
            {
                r.Status = m.Status;
            }
        });
    }

    public Guid Id => Community.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(Description), nameof(Location), nameof(MemberCountText), nameof(CoverImage), nameof(IsPrivate), nameof(Category))]
    private Community _community;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMember), nameof(IsPending), nameof(CanJoin), nameof(StatusText), nameof(IsOwner))]
    private MembershipStatus _status;

    [ObservableProperty]
    private bool _isBusy;

    public string Name => Community.Name;
    public string Description => Community.Description;
    public string Category => Community.Category;
    public string Location => string.IsNullOrEmpty(Community.Location) ? "International" : Community.Location;
    public string? CoverImage => Community.CoverImage;
    public bool IsPrivate => Community.IsPrivate;
    public string Initial => Community.Name[..1].ToUpperInvariant();

    public string MemberCountText => Community.MemberCount switch
    {
        0 => "Aucun membre",
        1 => "1 membre",
        var n => $"{n} membres"
    };

    public bool IsMember => Status is MembershipStatus.Member or MembershipStatus.Owner;
    public bool IsOwner => Status == MembershipStatus.Owner;
    public bool IsPending => Status == MembershipStatus.Pending;
    public bool CanJoin => Status == MembershipStatus.None;

    public string StatusText => Status switch
    {
        MembershipStatus.Owner => "Créateur",
        MembershipStatus.Member => "Membre",
        MembershipStatus.Pending => "Demande envoyée",
        _ => IsPrivate ? "Privée" : "Publique"
    };

    [RelayCommand]
    private void Open() => _ctx.Navigation.NavigateTo<CommunityDetailsViewModel>(Id);

    [RelayCommand]
    private async Task JoinAsync()
    {
        await RunAsync(async () =>
        {
            var status = await _ctx.Communities.JoinAsync(Id, _ctx.Session.CurrentUserId);
            Status = status;
            _ctx.Toast.Show(
                status == MembershipStatus.Pending
                    ? "Demande envoyée aux administrateurs de la communauté."
                    : $"Vous avez rejoint « {Name} ».",
                ToastKind.Success);
        });
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        var confirmed = await _ctx.Dialogs.ConfirmAsync(
            "Quitter la communauté ?",
            $"Vous ne verrez plus les publications réservées aux membres de « {Name} ».",
            "Quitter",
            isDestructive: true);

        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _ctx.Communities.LeaveAsync(Id, _ctx.Session.CurrentUserId);
            Status = MembershipStatus.None;
            _ctx.Toast.Show($"Vous avez quitté « {Name} ».");
        });
    }

    [RelayCommand]
    private async Task CancelRequestAsync()
    {
        await RunAsync(async () =>
        {
            await _ctx.Communities.CancelJoinRequestAsync(Id, _ctx.Session.CurrentUserId);
            Status = MembershipStatus.None;
            _ctx.Toast.Show("Demande d'adhésion annulée.");
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _ctx.Toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
