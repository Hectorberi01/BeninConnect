using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using BeninConnect.App.Features.Feed.ViewModels;

namespace BeninConnect.App.Features.Feed.Views;

public partial class PostDetailsView : UserControl
{
    private PostDetailsViewModel? _vm;

    public PostDetailsView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as PostDetailsViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    // Comportement visuel : bouton « Commenter » → focus sur le champ de saisie (ouvre le clavier).
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PostDetailsViewModel.IsContent) && _vm is { IsContent: true, FocusComment: true })
        {
            _vm.FocusComment = false;
            Dispatcher.UIThread.Post(() => CommentBox.Focus(), DispatcherPriority.Background);
        }
    }
}
