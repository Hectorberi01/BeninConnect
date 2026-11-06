using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;

namespace BeninConnect.App.Shell;

/// <summary>
/// Vue racine. Le code-behind ne gère que l'intégration plateforme : bouton retour Android,
/// zones système (encoche, barre de gestes) et clavier virtuel.
/// </summary>
public partial class MainView : UserControl
{
    private TopLevel? _topLevel;
    private Thickness _safeArea;
    private double _keyboardHeight;

    public MainView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.BackRequested += OnBackRequested;

        if (_topLevel.InsetsManager is { } insets)
        {
            insets.DisplayEdgeToEdgePreference = true;
            _safeArea = insets.SafeAreaPadding;
            insets.SafeAreaChanged += OnSafeAreaChanged;
        }

        if (_topLevel.InputPane is { } pane)
        {
            pane.StateChanged += OnInputPaneChanged;
        }

        ApplyPadding();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.BackRequested -= OnBackRequested;
            if (_topLevel.InsetsManager is { } insets)
            {
                insets.SafeAreaChanged -= OnSafeAreaChanged;
            }

            if (_topLevel.InputPane is { } pane)
            {
                pane.StateChanged -= OnInputPaneChanged;
            }
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.HandleBack())
        {
            e.Handled = true;
        }
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
    {
        _safeArea = e.SafeAreaPadding;
        ApplyPadding();
    }

    private void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e)
    {
        _keyboardHeight = e.NewState == InputPaneState.Open ? e.EndRect.Height : 0;
        ApplyPadding();
    }

    private void ApplyPadding() =>
        Padding = new Thickness(
            _safeArea.Left,
            _safeArea.Top,
            _safeArea.Right,
            Math.Max(_safeArea.Bottom, _keyboardHeight));
}
