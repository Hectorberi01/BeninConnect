using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace BeninConnect.App.Shared.Controls;

public partial class SearchBar : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SearchBar, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<SearchBar, string?>(nameof(Watermark), "Rechercher");

    public SearchBar() => InitializeComponent();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Watermark { get => GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }

    private void OnClear(object? sender, RoutedEventArgs e) => Text = string.Empty;
}
