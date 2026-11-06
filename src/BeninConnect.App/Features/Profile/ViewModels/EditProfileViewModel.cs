using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Profile.ViewModels;

public sealed partial class EditProfileViewModel(
    INavigationService navigation,
    IUserRepository users,
    ISessionService session,
    IToastService toast) : PageViewModel(navigation)
{
    public const int MaxInterests = 6;

    public ObservableCollection<Chip> Interests { get; } = [];

    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _profession = string.Empty;
    [ObservableProperty] private string _country = string.Empty;
    [ObservableProperty] private string _city = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BioCounter))]
    private string _bio = string.Empty;

    [ObservableProperty] private string _initials = string.Empty;
    [ObservableProperty] private string _avatarColor = "#087F5B";

    [ObservableProperty] private string? _firstNameError;
    [ObservableProperty] private string? _lastNameError;
    [ObservableProperty] private string? _countryError;
    [ObservableProperty] private string? _cityError;
    [ObservableProperty] private string? _bioError;
    [ObservableProperty] private string? _interestsError;
    [ObservableProperty] private string? _formError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSubmitting;

    public string BioCounter => $"{Bio.Length} / 300";

    public override Task OnNavigatedToAsync(object? parameter)
    {
        var me = session.CurrentUser ?? throw new InvalidOperationException("Aucun utilisateur connecté.");
        FirstName = me.FirstName;
        LastName = me.LastName;
        Profession = me.Profession;
        Country = me.Country;
        City = me.City;
        Bio = me.Bio;
        Initials = me.Initials;
        AvatarColor = me.AvatarColor;

        foreach (var interest in users.AvailableInterests)
        {
            Interests.Add(new Chip(interest, interest, ToggleInterest, me.Interests.Contains(interest)));
        }

        return Task.CompletedTask;
    }

    private void ToggleInterest(Chip chip)
    {
        if (!chip.IsSelected && Interests.Count(i => i.IsSelected) >= MaxInterests)
        {
            InterestsError = $"Maximum {MaxInterests} centres d'intérêt.";
            return;
        }

        chip.IsSelected = !chip.IsSelected;
        InterestsError = null;
    }

    public bool Validate()
    {
        FirstNameError = Validation.Length(FirstName, "Le prénom", 2, 40);
        LastNameError = Validation.Length(LastName, "Le nom", 2, 40);
        CountryError = Validation.Required(Country, "Le pays");
        CityError = Validation.Required(City, "La ville");
        BioError = Bio.Length > 300 ? "La biographie ne doit pas dépasser 300 caractères." : null;
        return FirstNameError is null && LastNameError is null && CountryError is null && CityError is null && BioError is null;
    }

    private bool CanSave() => !IsSubmitting;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        FormError = null;
        if (!Validate())
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            var updated = await users.UpdateProfileAsync(new UpdateProfileRequest(
                session.CurrentUserId,
                FirstName,
                LastName,
                Profession,
                Country,
                City,
                Bio,
                Interests.Where(i => i.IsSelected).Select(i => i.Key).ToList()));

            session.Update(updated);
            toast.Show("Profil mis à jour.", ToastKind.Success);
            Navigation.GoBack();
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
}
