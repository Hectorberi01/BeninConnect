using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Authentication.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Authentication.ViewModels;

public sealed partial class ForgotPasswordViewModel(INavigationService navigation, IAuthService auth) : PageViewModel(navigation)
{
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string? _emailError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    private bool _isSent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSubmitting;

    public bool IsFormVisible => !IsSent;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is string email)
        {
            Email = email;
        }

        return Task.CompletedTask;
    }

    private bool CanSend() => !IsSubmitting;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        EmailError = Validation.Email(Email);
        if (EmailError is not null)
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            await auth.RequestPasswordResetAsync(Email);
            IsSent = true;
        }
        catch (Exception ex)
        {
            EmailError = ErrorMessages.From(ex);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    [RelayCommand]
    private void BackToLogin() => Navigation.GoBack();
}
