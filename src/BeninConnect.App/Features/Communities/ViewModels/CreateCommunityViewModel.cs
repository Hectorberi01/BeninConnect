using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Feed.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Communities.ViewModels;

public sealed partial class CreateCommunityViewModel(
    INavigationService navigation,
    ICommunityRepository communities,
    ISessionService session,
    IToastService toast) : PageViewModel(navigation)
{
    public ObservableCollection<string> Categories { get; } = [];
    public ObservableCollection<ImageOption> Covers { get; } = [];

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string? _category;
    [ObservableProperty] private string _country = string.Empty;
    [ObservableProperty] private string _city = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrivate))]
    private bool _isPublic = true;

    [ObservableProperty] private string? _coverImage;

    [ObservableProperty] private string? _nameError;
    [ObservableProperty] private string? _descriptionError;
    [ObservableProperty] private string? _categoryError;
    [ObservableProperty] private string? _countryError;
    [ObservableProperty] private string? _cityError;
    [ObservableProperty] private string? _formError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isSubmitting;

    public bool IsPrivate
    {
        get => !IsPublic;
        set => IsPublic = !value;
    }

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        for (var i = 1; i <= 15; i++)
        {
            Covers.Add(new ImageOption($"Covers/community_{i:00}.jpg", OnCoverSelected));
        }

        OnCoverSelected(Covers[0]);
        Country = session.CurrentUser?.Country ?? string.Empty;
        City = session.CurrentUser?.City ?? string.Empty;

        try
        {
            foreach (var c in await communities.GetCategoriesAsync())
            {
                Categories.Add(c);
            }
        }
        catch (Exception ex)
        {
            FormError = ErrorMessages.From(ex);
        }
    }

    public bool Validate()
    {
        NameError = Validation.Length(Name, "Le nom", 3, 60);
        DescriptionError = Validation.Length(Description, "La description", 10, 500);
        CategoryError = string.IsNullOrEmpty(Category) ? "Choisissez une catégorie." : null;
        CountryError = Validation.Required(Country, "Le pays");
        CityError = Validation.Required(City, "La ville");
        return NameError is null && DescriptionError is null && CategoryError is null && CountryError is null && CityError is null;
    }

    private bool CanCreate() => !IsSubmitting;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        FormError = null;
        if (!Validate())
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            var created = await communities.CreateAsync(new CreateCommunityRequest(
                session.CurrentUserId,
                Name,
                Description,
                Category!,
                Country,
                City,
                IsPublic ? CommunityVisibility.Public : CommunityVisibility.Private,
                CoverImage));

            toast.Show($"Communauté « {created.Name} » créée.", ToastKind.Success);
            Navigation.ReplaceWith<CommunityDetailsViewModel>(created.Id);
        }
        catch (Exception ex)
        {
            FormError = ErrorMessages.From(ex);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private void OnCoverSelected(ImageOption option)
    {
        foreach (var cover in Covers)
        {
            cover.IsSelected = ReferenceEquals(cover, option);
        }

        CoverImage = option.Path;
    }
}
