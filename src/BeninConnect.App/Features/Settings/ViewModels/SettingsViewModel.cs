using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Profile.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Settings.ViewModels;

/// <summary>Compte, confidentialité, notifications, apparence (clair/sombre fonctionnel), langue, à propos, déconnexion.</summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly ISessionService _session;
    private readonly IPreferencesService _preferences;
    private readonly IThemeService _theme;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toast;
    private readonly IDeveloperOptions _developer;
    private readonly AppOptions _options;
    private bool _loading;

    public SettingsViewModel(
        INavigationService navigation,
        ISessionService session,
        IPreferencesService preferences,
        IThemeService theme,
        IDialogService dialogs,
        IToastService toast,
        IDeveloperOptions developer,
        AppOptions options) : base(navigation)
    {
        _options = options;
        _session = session;
        _preferences = preferences;
        _theme = theme;
        _dialogs = dialogs;
        _toast = toast;
        _developer = developer;
    }

    public string Email => _session.CurrentUser?.Email ?? string.Empty;
    public string DisplayName => _session.CurrentUser?.DisplayName ?? string.Empty;
    public string Initials => _session.CurrentUser?.Initials ?? string.Empty;
    public string AvatarColor => _session.CurrentUser?.AvatarColor ?? "#087F5B";
    public bool IsDeveloperAvailable => _developer.IsAvailable;
    public string Version => "1.0.0 (démonstration)";

    // Confidentialité
    [ObservableProperty] private bool _privateProfile;
    [ObservableProperty] private bool _showCity = true;
    [ObservableProperty] private bool _allowMessages = true;

    // Notifications
    [ObservableProperty] private bool _notifySocial = true;
    [ObservableProperty] private bool _notifyCommunities = true;
    [ObservableProperty] private bool _notifyMessages = true;
    [ObservableProperty] private bool _notifyEvents = true;

    // Apparence
    [ObservableProperty] private bool _isLightTheme;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private bool _isSystemTheme;

    // Développeur
    [ObservableProperty] private bool _simulateErrors;

    // Source de données (appliquée au prochain démarrage)
    [ObservableProperty] private bool _useApi;
    [ObservableProperty] private bool _useDemoData = true;
    [ObservableProperty] private string _apiUrl = AppOptions.DefaultApiUrl;

    public string ActiveSourceText => _options.UseMockData
        ? "Source active : données de démonstration (hors ligne)"
        : $"Source active : API {_options.ApiBaseUrl}";

    public override Task OnNavigatedToAsync(object? parameter)
    {
        _loading = true;
        PrivateProfile = _preferences.GetBool(PreferenceKeys.PrivateProfile);
        ShowCity = _preferences.GetBool(PreferenceKeys.ShowCity, true);
        AllowMessages = _preferences.GetBool(PreferenceKeys.AllowMessages, true);
        NotifySocial = _preferences.GetBool(PreferenceKeys.NotifySocial, true);
        NotifyCommunities = _preferences.GetBool(PreferenceKeys.NotifyCommunities, true);
        NotifyMessages = _preferences.GetBool(PreferenceKeys.NotifyMessages, true);
        NotifyEvents = _preferences.GetBool(PreferenceKeys.NotifyEvents, true);

        IsLightTheme = _theme.Current == AppTheme.Light;
        IsDarkTheme = _theme.Current == AppTheme.Dark;
        IsSystemTheme = _theme.Current == AppTheme.System;
        SimulateErrors = _developer.SimulateErrors;
        UseApi = _preferences.Get(PreferenceKeys.DataSource) == "api";
        UseDemoData = !UseApi;
        ApiUrl = _preferences.Get(PreferenceKeys.ApiUrl) ?? _options.ApiBaseUrl?.ToString().TrimEnd('/') ?? AppOptions.DefaultApiUrl;
        _loading = false;
        return Task.CompletedTask;
    }

    partial void OnPrivateProfileChanged(bool value) => Save(PreferenceKeys.PrivateProfile, value);
    partial void OnShowCityChanged(bool value) => Save(PreferenceKeys.ShowCity, value);
    partial void OnAllowMessagesChanged(bool value) => Save(PreferenceKeys.AllowMessages, value);
    partial void OnNotifySocialChanged(bool value) => Save(PreferenceKeys.NotifySocial, value);
    partial void OnNotifyCommunitiesChanged(bool value) => Save(PreferenceKeys.NotifyCommunities, value);
    partial void OnNotifyMessagesChanged(bool value) => Save(PreferenceKeys.NotifyMessages, value);
    partial void OnNotifyEventsChanged(bool value) => Save(PreferenceKeys.NotifyEvents, value);

    partial void OnIsLightThemeChanged(bool value)
    {
        if (value && !_loading)
        {
            _theme.Apply(AppTheme.Light);
        }
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (value && !_loading)
        {
            _theme.Apply(AppTheme.Dark);
        }
    }

    partial void OnIsSystemThemeChanged(bool value)
    {
        if (value && !_loading)
        {
            _theme.Apply(AppTheme.System);
        }
    }

    partial void OnSimulateErrorsChanged(bool value)
    {
        _developer.SimulateErrors = value;
        if (!_loading)
        {
            _toast.Show(value
                ? "Erreurs simulées activées : environ un appel sur deux échouera."
                : "Erreurs simulées désactivées.");
        }
    }

    private void Save(string key, bool value)
    {
        if (!_loading)
        {
            _preferences.SetBool(key, value);
        }
    }

    [RelayCommand]
    private void ApplyDataSource()
    {
        if (UseApi && !Uri.TryCreate(ApiUrl.Trim(), UriKind.Absolute, out _))
        {
            _toast.Show("Adresse d'API invalide (ex. http://192.168.1.20:5080).", ToastKind.Error);
            return;
        }

        _preferences.Set(PreferenceKeys.DataSource, UseApi ? "api" : "demo");
        _preferences.Set(PreferenceKeys.ApiUrl, ApiUrl.Trim());
        _toast.Show("Enregistré. Fermez puis relancez l'application pour changer de source.", ToastKind.Success);
    }

    [RelayCommand]
    private void EditProfile() => Navigation.NavigateTo<EditProfileViewModel>();

    [RelayCommand]
    private void SelectFrench() => _toast.Show("Le français est la langue de cette version. L'anglais arrivera dans une prochaine version.");

    [RelayCommand]
    private Task ShowAboutAsync() => _dialogs.AlertAsync(
        "À propos de BéninConnect",
        "BéninConnect Community — prototype de démonstration (version 1.0).\n\n" +
        "Toutes les personnes, communautés, publications et événements sont fictifs. " +
        "Aucune donnée ne quitte votre appareil et aucun serveur n'est contacté.\n\n" +
        "Icônes : Material Design Icons (Apache 2.0). Police : Inter (SIL OFL). Images : illustrations générées pour le projet.");

    [RelayCommand]
    private Task ShowPrivacyInfoAsync() => _dialogs.AlertAsync(
        "Vos données",
        "Dans cette démonstration, vos réglages sont enregistrés uniquement sur cet appareil. " +
        "Les contenus que vous publiez restent en mémoire et disparaissent à la fermeture de l'application.");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!await _dialogs.ConfirmAsync("Se déconnecter ?", "Vous reviendrez à l'écran de connexion.", "Se déconnecter", isDestructive: true))
        {
            return;
        }

        ProfileViewModel.SignOut(_session, _preferences, Navigation);
    }
}
