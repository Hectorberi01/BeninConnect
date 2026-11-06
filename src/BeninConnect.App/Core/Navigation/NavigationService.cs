using BeninConnect.App.Core.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.App.Core.Navigation;

/// <summary>
/// Navigation par pile de ViewModels. Les onglets vivent dans ShellViewModel, qui reste dans la pile
/// pendant la navigation secondaire : leur état n'est donc jamais recréé.
/// </summary>
public sealed class NavigationService(IServiceProvider services) : ObservableObject, INavigationService
{
    // Anti double-tap : même destination + même paramètre en moins de 400 ms = ignoré.
    private const long DoubleTapWindowMs = 400;

    private readonly List<ViewModelBase> _stack = [];
    private Type? _lastPushedType;
    private object? _lastParameter;
    private long _lastPushTicks;

    public ViewModelBase? CurrentViewModel => _stack.Count > 0 ? _stack[^1] : null;

    public bool CanGoBack => _stack.Count > 1;

    public IReadOnlyList<ViewModelBase> Stack => _stack;

    public Task LastNavigationTask { get; private set; } = Task.CompletedTask;

    public event EventHandler? Navigated;

    public void NavigateTo<TViewModel>(object? parameter = null)
        where TViewModel : ViewModelBase
    {
        var now = Environment.TickCount64;
        if (_lastPushedType == typeof(TViewModel)
            && Equals(_lastParameter, parameter)
            && now - _lastPushTicks < DoubleTapWindowMs)
        {
            return;
        }

        _lastPushedType = typeof(TViewModel);
        _lastParameter = parameter;
        _lastPushTicks = now;
        Push(Resolve<TViewModel>(), parameter);
    }

    public void ReplaceWith<TViewModel>(object? parameter = null)
        where TViewModel : ViewModelBase
    {
        if (_stack.Count > 0)
        {
            var leaving = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            leaving.OnNavigatedFrom();
        }

        Push(Resolve<TViewModel>(), parameter);
    }

    public void ResetTo<TViewModel>(object? parameter = null)
        where TViewModel : ViewModelBase
    {
        foreach (var vm in _stack)
        {
            vm.OnNavigatedFrom();
        }

        _stack.Clear();
        _lastPushedType = null;
        Push(Resolve<TViewModel>(), parameter);
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        var leaving = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        leaving.OnNavigatedFrom();
        _lastPushedType = null;
        RaiseChanged();
        LastNavigationTask = CurrentViewModel!.OnResumedAsync();
    }

    public bool TryHandleBack()
    {
        if (CurrentViewModel?.OnBackRequested() == true)
        {
            return true;
        }

        if (CanGoBack)
        {
            GoBack();
            return true;
        }

        return false;
    }

    private TViewModel Resolve<TViewModel>()
        where TViewModel : ViewModelBase =>
        services.GetRequiredService<TViewModel>();

    private void Push(ViewModelBase vm, object? parameter)
    {
        _stack.Add(vm);
        RaiseChanged();
        LastNavigationTask = vm.OnNavigatedToAsync(parameter);
    }

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(CurrentViewModel));
        OnPropertyChanged(nameof(CanGoBack));
        Navigated?.Invoke(this, EventArgs.Empty);
    }
}
