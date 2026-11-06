using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Feed.ViewModels;

public sealed record CreatePostParameter(Guid? CommunityId = null, bool OpenImagePicker = false);

public sealed record CommunityOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record VisibilityOption(PostVisibility Value, string Label, string Description)
{
    public override string ToString() => Label;
}

/// <summary>Image de démonstration sélectionnable (galerie locale, hors ligne).</summary>
public sealed partial class ImageOption(string path, Action<ImageOption> onSelected) : ObservableObject
{
    public string Path { get; } = path;

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private void Select() => onSelected(this);
}

public sealed partial class CreatePostViewModel(
    INavigationService navigation,
    IPostRepository posts,
    ICommunityRepository communities,
    ISessionService session,
    IToastService toast,
    IDialogService dialogs) : PageViewModel(navigation)
{
    public const int MaxLength = 2000;

    public ObservableCollection<CommunityOption> CommunityOptions { get; } = [new(null, "Aucune (fil public)")];

    public IReadOnlyList<VisibilityOption> VisibilityOptions { get; } =
    [
        new(PostVisibility.Public, "Public", "Visible par tous les membres de BéninConnect"),
        new(PostVisibility.CommunityMembers, "Membres de la communauté", "Visible uniquement dans la communauté choisie"),
        new(PostVisibility.OnlyMe, "Moi uniquement", "Brouillon visible seulement par vous"),
    ];

    public ObservableCollection<ImageOption> Images { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemainingText), nameof(IsDirty))]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private string _content = string.Empty;

    [ObservableProperty]
    private CommunityOption? _selectedCommunity;

    [ObservableProperty]
    private VisibilityOption? _selectedVisibility;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage), nameof(IsDirty))]
    private string? _selectedImage;

    [ObservableProperty]
    private bool _isImagePickerOpen;

    [ObservableProperty]
    private string? _contentError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private bool _isSubmitting;

    public bool HasImage => SelectedImage is not null;
    public bool IsDirty => !string.IsNullOrWhiteSpace(Content) || SelectedImage is not null;
    public string RemainingText => $"{Content.Length} / {MaxLength}";
    public string AuthorName => session.CurrentUser?.DisplayName ?? string.Empty;
    public string AuthorInitials => session.CurrentUser?.Initials ?? string.Empty;
    public string AuthorColor => session.CurrentUser?.AvatarColor ?? "#087F5B";

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        var p = parameter as CreatePostParameter ?? new CreatePostParameter();
        SelectedVisibility = VisibilityOptions[0];
        SelectedCommunity = CommunityOptions[0];
        IsImagePickerOpen = p.OpenImagePicker;

        for (var i = 1; i <= 16; i++)
        {
            Images.Add(new ImageOption($"Posts/post_{i:00}.jpg", OnImageSelected));
        }

        try
        {
            var mine = await communities.GetForMemberAsync(session.CurrentUserId);
            foreach (var c in mine)
            {
                CommunityOptions.Add(new CommunityOption(c.Id, c.Name));
            }

            if (p.CommunityId is { } id && CommunityOptions.FirstOrDefault(o => o.Id == id) is { } preselected)
            {
                SelectedCommunity = preselected;
            }
        }
        catch (Exception ex)
        {
            toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
    }

    partial void OnSelectedCommunityChanged(CommunityOption? value)
    {
        // « Membres de la communauté » n'a de sens qu'avec une communauté choisie.
        if (value?.Id is null && SelectedVisibility?.Value == PostVisibility.CommunityMembers)
        {
            SelectedVisibility = VisibilityOptions[0];
        }
    }

    public bool Validate()
    {
        ContentError = Content.Trim().Length switch
        {
            0 => "Écrivez quelque chose avant de publier.",
            < 3 => "La publication doit contenir au moins 3 caractères.",
            > MaxLength => $"La publication ne doit pas dépasser {MaxLength} caractères.",
            _ => null
        };

        if (ContentError is null && SelectedVisibility?.Value == PostVisibility.CommunityMembers && SelectedCommunity?.Id is null)
        {
            ContentError = "Choisissez une communauté pour une publication réservée aux membres.";
        }

        return ContentError is null;
    }

    private bool CanPublish() => !IsSubmitting && !string.IsNullOrWhiteSpace(Content);

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        if (!Validate())
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            await posts.CreateAsync(new CreatePostRequest(
                session.CurrentUserId,
                Content,
                SelectedCommunity?.Id,
                SelectedImage,
                SelectedVisibility?.Value ?? PostVisibility.Public));

            toast.Show("Votre publication est en ligne.", ToastKind.Success);
            Navigation.GoBack();
        }
        catch (Exception ex)
        {
            ContentError = ErrorMessages.From(ex);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsDirty && !await dialogs.ConfirmAsync("Abandonner la publication ?", "Votre texte ne sera pas conservé.", "Abandonner", "Continuer", isDestructive: true))
        {
            return;
        }

        Navigation.GoBack();
    }

    /// <summary>Retour Android : demande confirmation si un brouillon est en cours.</summary>
    public override bool OnBackRequested()
    {
        if (!IsDirty)
        {
            return false;
        }

        _ = CancelAsync();
        return true;
    }

    [RelayCommand]
    private void ToggleImagePicker() => IsImagePickerOpen = !IsImagePickerOpen;

    [RelayCommand]
    private void RemoveImage()
    {
        SelectedImage = null;
        foreach (var image in Images)
        {
            image.IsSelected = false;
        }
    }

    private void OnImageSelected(ImageOption option)
    {
        foreach (var image in Images)
        {
            image.IsSelected = ReferenceEquals(image, option);
        }

        SelectedImage = option.Path;
        IsImagePickerOpen = false;
    }
}
