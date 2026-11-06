using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Features.Authentication.Models;
using BeninConnect.App.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Authentication.ViewModels;

public sealed partial class LoginViewModel(
    INavigationService navigation,
    IAuthService auth,
    ISessionService session,
    IPreferencesService preferences) : ViewModelBase
{
    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private string? _emailError;

    [ObservableProperty]
    private string? _passwordError;

    [ObservableProperty]
    private string? _formError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isSubmitting;

    public string DemoEmail => DemoAccount.Email;
    public string DemoPassword => DemoAccount.Password;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is string email)
        {
            Email = email;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void UseDemoAccount()
    {
        Email = DemoEmail;
        Password = DemoPassword;
        EmailError = PasswordError = FormError = null;
    }

    public bool Validate()
    {
        EmailError = Validation.Email(Email);
        PasswordError = string.IsNullOrEmpty(Password) ? "Le mot de passe est obligatoire." : null;
        return EmailError is null && PasswordError is null;
    }

    private bool CanLogin() => !IsSubmitting;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        FormError = null;
        if (!Validate())
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            var result = await auth.LoginAsync(Email, Password);
            if (!result.Succeeded || result.User is null)
            {
                FormError = result.Error;
                return;
            }

            preferences.Set(PreferenceKeys.RememberedAccount, RememberMe ? result.User.Email : null);
            session.SignIn(result.User);
            navigation.ResetTo<ShellViewModel>();
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

    [RelayCommand]
    private void GoToRegister() => navigation.NavigateTo<RegisterViewModel>();

    [RelayCommand]
    private void GoToForgotPassword() => navigation.NavigateTo<ForgotPasswordViewModel>(Email);
}
