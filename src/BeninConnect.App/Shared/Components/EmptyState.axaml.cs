using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace BeninConnect.App.Shared.Components;

/// <summary>État « liste vide » avec action optionnelle.</summary>
public partial class EmptyState : UserControl
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title), "Rien à afficher");

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Message));

    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(ActionText));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<EmptyState, ICommand?>(nameof(ActionCommand));

    public EmptyState() => InitializeComponent();

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public string? ActionText { get => GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
    public ICommand? ActionCommand { get => GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
}
