using Avalonia;
using Avalonia.Controls;

namespace BeninConnect.App.Shared.Controls;

/// <summary>Pastille de compteur (non-lus). Masquée quand le compteur vaut 0.</summary>
public partial class BadgeView : UserControl
{
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<BadgeView, int>(nameof(Count));

    public static readonly DirectProperty<BadgeView, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<BadgeView, string>(nameof(DisplayText), b => b.DisplayText);

    private string _displayText = string.Empty;

    public BadgeView()
    {
        InitializeComponent();
        IsVisible = false;
    }

    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    public string DisplayText
    {
        get => _displayText;
        private set => SetAndRaise(DisplayTextProperty, ref _displayText, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty)
        {
            DisplayText = Count > 99 ? "99+" : Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            IsVisible = Count > 0;
        }
    }
}
