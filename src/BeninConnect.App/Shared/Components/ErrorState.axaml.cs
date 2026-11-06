using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace BeninConnect.App.Shared.Components;

/// <summary>État d'erreur avec bouton « Réessayer ».</summary>
public partial class ErrorState : UserControl
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ErrorState, string?>(nameof(Message));

    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<ErrorState, ICommand?>(nameof(RetryCommand));

    public ErrorState() => InitializeComponent();

    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }
}
