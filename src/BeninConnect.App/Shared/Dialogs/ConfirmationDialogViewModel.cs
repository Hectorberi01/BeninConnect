using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Shared.Dialogs;

public sealed partial class ConfirmationDialogViewModel : ObservableObject
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public ConfirmationDialogViewModel(string title, string message, string confirmText, string cancelText, bool isDestructive)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsDestructive = isDestructive;
    }

    public event EventHandler? Completed;

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }
    public bool IsDestructive { get; }
    public bool HasCancel => !string.IsNullOrEmpty(CancelText);

    public Task<bool> Result => _tcs.Task;

    [RelayCommand]
    private void Confirm() => Complete(true);

    [RelayCommand]
    public void Cancel() => Complete(false);

    private void Complete(bool result)
    {
        if (_tcs.TrySetResult(result))
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
