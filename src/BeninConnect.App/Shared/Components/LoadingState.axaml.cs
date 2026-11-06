using Avalonia;
using Avalonia.Controls;

namespace BeninConnect.App.Shared.Components;

/// <summary>Indicateur de chargement discret.</summary>
public partial class LoadingState : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<LoadingState, string?>(nameof(Text), "Chargement…");

    public LoadingState() => InitializeComponent();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
