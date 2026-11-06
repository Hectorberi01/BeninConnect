using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Authentication.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Authentication.ViewModels;

public sealed partial class RegisterViewModel(INavigationService navigation, IAuthService auth) : PageViewModel(navigation)
{
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _country = string.Empty;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _acceptTerms;

    [ObservableProperty] private string? _firstNameError;
    [ObservableProperty] private string? _lastNameError;
    [ObservableProperty] private string? _emailError;
    [ObservableProperty] private string? _countryError;
    [ObservableProperty] private string? _cityError;
    [ObservableProperty] private string? _passwordError;
    [ObservableProperty] private string? _confirmPasswordError;
    [ObservableProperty] private string? _termsError;
    [ObservableProperty] private string? _formError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool _isSubmitting;

    public bool Validate()
    {
        FirstNameError = Validation.Length(FirstName, "Le prénom", 2, 40);
        LastNameError = Validation.Length(LastName, "Le nom", 2, 40);
        EmailError = Validation.Email(Email);
        CountryError = Validation.Required(Country, "Le pays");
        CityError = Validation.Required(City, "La ville");
        PasswordError = Validation.Password(Password);
        ConfirmPasswordError = Password == ConfirmPassword ? null : "Les mots de passe ne correspondent pas.";
        TermsError = AcceptTerms ? null : "Vous devez accepter les conditions de la démonstration.";

        return new[] { FirstNameError, LastNameError, EmailError, CountryError, CityError, PasswordError, ConfirmPasswordError, TermsError }
            .All(e => e is null);
    }

    private bool CanRegister() => !IsSubmitting;

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        FormError = null;
        if (!Validate())
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            var result = await auth.RegisterAsync(new RegisterRequest(FirstName, LastName, Email, Country, City, Password));
            if (!result.Succeeded)
            {
                FormError = result.Error;
                return;
            }

            Navigation.ReplaceWith<VerifyEmailViewModel>(Email.Trim());
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
