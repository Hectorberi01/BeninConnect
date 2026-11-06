using CommunityToolkit.Mvvm.ComponentModel;

namespace BeninConnect.App.Core.Services;

public enum ToastKind
{
    Info,
    Success,
    Error
}

public interface IToastService
{
    void Show(string message, ToastKind kind = ToastKind.Info);
}

/// <summary>Message éphémère affiché en haut de l'écran (MainView).</summary>
public sealed partial class ToastService : ObservableObject, IToastService
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(2600);
    private int _version;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsError))]
    private ToastKind _kind;

    [ObservableProperty]
    private bool _isVisible;

    public bool IsError => Kind == ToastKind.Error;

    public string? LastMessage { get; private set; }

    public void Show(string message, ToastKind kind = ToastKind.Info)
    {
        Message = message;
        LastMessage = message;
        Kind = kind;
        IsVisible = true;
        _ = HideLaterAsync(++_version);
    }

    private async Task HideLaterAsync(int version)
    {
        await Task.Delay(Duration);
        if (version == _version)
        {
            IsVisible = false;
        }
    }
}
