using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace BeninConnect.App.Shared.Components;

/// <summary>En-tête d'écran secondaire : retour, titre, sous-titre, actions à droite.</summary>
public partial class AppHeader : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AppHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<AppHeader, string?>(nameof(Subtitle));

    public static readonly StyledProperty<ICommand?> BackCommandProperty =
        AvaloniaProperty.Register<AppHeader, ICommand?>(nameof(BackCommand));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<AppHeader, object?>(nameof(Actions));

    public AppHeader() => InitializeComponent();

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public ICommand? BackCommand { get => GetValue(BackCommandProperty); set => SetValue(BackCommandProperty, value); }
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
}
