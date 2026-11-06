using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace BeninConnect.App.Shared.Controls;

/// <summary>Avatar rond avec initiales sur fond coloré (aucune photo réelle n'est utilisée).</summary>
public sealed class AvatarView : Control
{
    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<AvatarView, string?>(nameof(Initials));

    public static readonly StyledProperty<string?> ColorHexProperty =
        AvaloniaProperty.Register<AvatarView, string?>(nameof(ColorHex), "#087F5B");

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AvatarView, double>(nameof(Size), 40);

    public static readonly StyledProperty<bool> ShowRingProperty =
        AvaloniaProperty.Register<AvatarView, bool>(nameof(ShowRing));

    static AvatarView()
    {
        AffectsRender<AvatarView>(InitialsProperty, ColorHexProperty, ShowRingProperty);
        AffectsMeasure<AvatarView>(SizeProperty);
    }

    public string? Initials
    {
        get => GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
    }

    public string? ColorHex
    {
        get => GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Anneau blanc (avatar posé sur une couverture).</summary>
    public bool ShowRing
    {
        get => GetValue(ShowRingProperty);
        set => SetValue(ShowRingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InitialsProperty)
        {
            AutomationProperties.SetName(this, $"Avatar {Initials}");
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var size = Size;
        var brush = ColorHex is not null && Color.TryParse(ColorHex, out var color)
            ? new SolidColorBrush(color)
            : new SolidColorBrush(Color.Parse("#087F5B"));

        var rect = new Rect(0, 0, size, size);
        var pen = ShowRing ? new Pen(Brushes.White, Math.Max(2, size / 22)) : null;
        context.DrawEllipse(brush, pen, rect.Center, size / 2 - (pen?.Thickness ?? 0) / 2, size / 2 - (pen?.Thickness ?? 0) / 2);

        if (string.IsNullOrEmpty(Initials))
        {
            return;
        }

        var text = new FormattedText(
            Initials,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            size * 0.38,
            Brushes.White);

        context.DrawText(text, new Point((size - text.Width) / 2, (size - text.Height) / 2));
    }
}
