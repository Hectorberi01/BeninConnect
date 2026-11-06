using Avalonia.Controls;
using Avalonia.Threading;
using BeninConnect.App.Features.Messaging.ViewModels;

namespace BeninConnect.App.Features.Messaging.Views;

public partial class ConversationView : UserControl
{
    private ConversationViewModel? _vm;

    public ConversationView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.MessageAppended -= OnMessageAppended;
        }

        _vm = DataContext as ConversationViewModel;
        if (_vm is not null)
        {
            _vm.MessageAppended += OnMessageAppended;
        }
    }

    // Comportement visuel : défilement automatique vers le dernier message.
    private void OnMessageAppended(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => MessagesScroll.ScrollToEnd(), DispatcherPriority.Background);
}
