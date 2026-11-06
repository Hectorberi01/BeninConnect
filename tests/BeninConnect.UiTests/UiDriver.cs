using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BeninConnect.App.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.UiTests;

/// <summary>Pilote une fenêtre headless contenant la vraie MainView, avec son propre conteneur.</summary>
public sealed class UiDriver : IDisposable
{
    private readonly ServiceProvider _services;

    public UiDriver()
    {
        BindingErrors = new BindingErrorSink();
        Logger.Sink = BindingErrors;

        _services = App.App.BuildServices(TestAppBuilder.Options);
        Main = _services.GetRequiredService<MainViewModel>();
        Window = new Window { Width = 400, Height = 860, Content = new MainView { DataContext = Main } };
        Window.Show();
    }

    public Window Window { get; }
    public MainViewModel Main { get; }
    public BindingErrorSink BindingErrors { get; }

    public T Get<T>() where T : notnull => _services.GetRequiredService<T>();

    public void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }
    }

    public async Task<T> WaitForAsync<T>(Func<T, bool>? predicate = null, int timeoutMs = 5000) where T : Control
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            Pump();
            var match = Window.GetVisualDescendants().OfType<T>()
                .FirstOrDefault(c => c.IsEffectivelyVisible && (predicate?.Invoke(c) ?? true));
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(15);
        }

        throw new TimeoutException($"Élément {typeof(T).Name} introuvable à l'écran.");
    }

    public async Task WaitUntilAsync(Func<bool> condition, string what, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            Pump();
            if (condition())
            {
                return;
            }

            await Task.Delay(15);
        }

        throw new TimeoutException($"Condition non atteinte : {what}");
    }

    /// <summary>Déclenche la commande du premier bouton visible portant ce nom d'accessibilité ou ce texte.</summary>
    public async Task ClickAsync(string nameOrText)
    {
        var button = await WaitForAsync<Button>(b =>
            AutomationProperties.GetName(b) == nameOrText
            || (b.Content as string) == nameOrText
            || (b.Content is TextBlock t && t.Text == nameOrText)
            || b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == nameOrText));

        Assert.True(button.IsEffectivelyEnabled, $"Bouton « {nameOrText} » désactivé.");
        Assert.NotNull(button.Command);
        button.Command!.Execute(button.CommandParameter);
        Pump();
    }

    public async Task TypeAsync(string watermark, string text)
    {
        var box = await WaitForAsync<TextBox>(t => t.Watermark == watermark);
        box.Text = text;
        Pump();
    }

    public void Dispose()
    {
        Window.Close();
        _services.Dispose();
        Logger.Sink = null;
    }
}

/// <summary>Capture les erreurs de binding (propriété introuvable, conversion impossible...).</summary>
public sealed class BindingErrorSink : ILogSink
{
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (_errors)
            {
                return _errors.ToList();
            }
        }
    }

    public bool IsEnabled(LogEventLevel level, string area) =>
        level >= LogEventLevel.Warning && area is LogArea.Binding or LogArea.Property;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Add($"[{area}] {messageTemplate} ({source?.GetType().Name})");

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
        Add($"[{area}] {messageTemplate} {string.Join(" | ", propertyValues)} ({source?.GetType().Name})");

    private void Add(string message)
    {
        // Chemin intermédiaire nul pendant le chargement (ex. Community avant réponse) : attendu, pas une erreur.
        if (message.Contains("Value is null"))
        {
            return;
        }

        lock (_errors)
        {
            _errors.Add(message);
        }
    }
}
