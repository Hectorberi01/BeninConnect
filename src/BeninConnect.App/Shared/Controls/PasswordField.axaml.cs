using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace BeninConnect.App.Shared.Controls;

/// <summary>Champ mot de passe avec bouton afficher / masquer.</summary>
public partial class PasswordField : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PasswordField, string?>(nameof(Label));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PasswordField, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<PasswordField, string?>(nameof(Watermark));

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<PasswordField, string?>(nameof(ErrorText));

    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<PasswordField, bool>(nameof(IsRevealed));

    public PasswordField() => InitializeComponent();

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Watermark { get => GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    // Comportement purement visuel : reste dans la vue.
    private void OnToggleReveal(object? sender, RoutedEventArgs e) => IsRevealed = !IsRevealed;
}
