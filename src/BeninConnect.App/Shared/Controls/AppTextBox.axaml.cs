using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input.TextInput;

namespace BeninConnect.App.Shared.Controls;

/// <summary>Champ de formulaire : libellé, saisie, message d'erreur de validation.</summary>
public partial class AppTextBox : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AppTextBox, string?>(nameof(Label));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AppTextBox, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<AppTextBox, string?>(nameof(Watermark));

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<AppTextBox, string?>(nameof(ErrorText));

    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<AppTextBox, int>(nameof(MaxLength));

    public static readonly StyledProperty<bool> IsMultilineProperty =
        AvaloniaProperty.Register<AppTextBox, bool>(nameof(IsMultiline));

    public static readonly StyledProperty<TextInputContentType> ContentTypeProperty =
        AvaloniaProperty.Register<AppTextBox, TextInputContentType>(nameof(ContentType));

    public AppTextBox() => InitializeComponent();

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Watermark { get => GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public int MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }
    public bool IsMultiline { get => GetValue(IsMultilineProperty); set => SetValue(IsMultilineProperty, value); }
    public TextInputContentType ContentType { get => GetValue(ContentTypeProperty); set => SetValue(ContentTypeProperty, value); }

    /// <summary>Donne le focus au champ (ex : ouverture des commentaires).</summary>
    public void FocusInput() => Input.Focus();
}
