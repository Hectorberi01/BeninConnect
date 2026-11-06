using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Authentication.ViewModels;

/// <summary>Vérification email simulée : aucun email n'est envoyé, le code de démonstration est affiché.</summary>
public sealed partial class VerifyEmailViewModel(
    INavigationService navigation,
    IAuthService auth,
    ISessionService session,
    IToastService toast) : PageViewModel(navigation)
{
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string? _codeError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private bool _isSubmitting;

    public string DemoCode => auth.DemoVerificationCode;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        Email = parameter as string ?? string.Empty;
        return Task.CompletedTask;
    }

    private bool CanVerify() => !IsSubmitting;

    [RelayCommand(CanExecute = nameof(CanVerify))]
    private async Task VerifyAsync()
    {
        CodeError = Code.Trim().Length != 6 || !Code.Trim().All(char.IsDigit)
            ? "Le code contient 6 chiffres."
            : null;

        if (CodeError is not null)
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            var result = await auth.VerifyEmailAsync(Email, Code);
            if (!result.Succeeded || result.User is null)
            {
                CodeError = result.Error;
                return;
            }

            session.SignIn(result.User);
            toast.Show("Adresse email vérifiée. Bienvenue !", ToastKind.Success);
            Navigation.ResetTo<ShellViewModel>();
        }
        catch (Exception ex)
        {
            CodeError = ErrorMessages.From(ex);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    [RelayCommand]
    private void FillDemoCode() => Code = DemoCode;

    [RelayCommand]
    private async Task ResendAsync()
    {
        try
        {
            await auth.ResendVerificationCodeAsync(Email);
            toast.Show($"Nouveau code simulé : {DemoCode} (aucun email n'est envoyé).");
        }
        catch (Exception ex)
        {
            toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
    }
}
