using BeninConnect.App.Shared.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BeninConnect.App.Core.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirmer",
        string cancelText = "Annuler",
        bool isDestructive = false);

    Task AlertAsync(string title, string message, string closeText = "Fermer");
}

/// <summary>Dialogue modal affiché en surimpression par MainView.</summary>
public sealed partial class DialogService : ObservableObject, IDialogService
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private ConfirmationDialogViewModel? _current;

    public bool IsOpen => Current is not null;

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirmer",
        string cancelText = "Annuler",
        bool isDestructive = false)
    {
        // Un seul dialogue à la fois : le précédent est annulé.
        Current?.Cancel();

        var dialog = new ConfirmationDialogViewModel(title, message, confirmText, cancelText, isDestructive);
        dialog.Completed += (_, _) =>
        {
            if (ReferenceEquals(Current, dialog))
            {
                Current = null;
            }
        };
        Current = dialog;
        return dialog.Result;
    }

    public Task AlertAsync(string title, string message, string closeText = "Fermer") =>
        ConfirmAsync(title, message, closeText, cancelText: string.Empty);
}
