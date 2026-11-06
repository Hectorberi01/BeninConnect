using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BeninConnect.App.Core.DependencyInjection;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.App;

public partial class App : Application
{
    /// <summary>Conteneur racine. Défini avant l'initialisation pour permettre aux tests d'injecter leurs options.</summary>
    public static IServiceProvider? Services { get; private set; }

    /// <summary>Options utilisées au prochain démarrage (les tests UI les remplacent).</summary>
    public static AppOptions Options { get; set; } = new();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.WriteLine($"[BeninConnect] CRASH {e.ExceptionObject}");
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Console.WriteLine($"[BeninConnect] UI ERROR {e.Exception}");
            e.Handled = true;
        };

        Control root;
        MainViewModel? mainViewModel = null;
        try
        {
            Services = BuildServices(ResolveOptions(Options));
            Services.GetRequiredService<IThemeService>().Initialize();
            mainViewModel = Services.GetRequiredService<MainViewModel>();
            root = new MainView { DataContext = mainViewModel };
        }
        catch (Exception ex)
        {
            // Démarrage impossible : on affiche l'erreur au lieu d'un écran blanc.
            Console.WriteLine($"[BeninConnect] STARTUP ERROR {ex}");
            root = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = "Erreur au démarrage :\n\n" + ex,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(16, 60, 16, 16),
                    FontSize = 12
                }
            };
        }

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                // Hôte de développement : fenêtre au format téléphone.
                desktop.MainWindow = new Window { Title = "BéninConnect (dev)", Width = 400, Height = 860, Content = root };
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = root;
                break;
        }

        mainViewModel?.Start();
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Applique la source de données (démo / API) choisie dans les paramètres.</summary>
    private static AppOptions ResolveOptions(AppOptions options) =>
        options.PreferencesFilePath is { } path
            ? options.WithDataSourceFrom(new JsonFilePreferencesService(path))
            : options;

    public static ServiceProvider BuildServices(AppOptions options)
    {
        var services = new ServiceCollection();
        services.AddBeninConnect(options);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}
