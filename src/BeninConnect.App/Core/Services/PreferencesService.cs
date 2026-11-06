using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeninConnect.App.Core.Services;

/// <summary>Préférences locales persistées (thème, langue, onboarding, compte mémorisé...). Aucun secret.</summary>
public interface IPreferencesService
{
    string? Get(string key);

    void Set(string key, string? value);

    bool GetBool(string key, bool defaultValue = false);

    void SetBool(string key, bool value);
}

public static class PreferenceKeys
{
    public const string Theme = "theme";
    public const string Language = "language";
    public const string OnboardingCompleted = "onboarding.completed";
    public const string RememberedAccount = "auth.remembered-account";
    public const string PrivateProfile = "privacy.private-profile";
    public const string ShowCity = "privacy.show-city";
    public const string AllowMessages = "privacy.allow-messages";
    public const string NotifySocial = "notifications.social";
    public const string NotifyCommunities = "notifications.communities";
    public const string NotifyMessages = "notifications.messages";
    public const string NotifyEvents = "notifications.events";
    public const string DataSource = "dev.data-source";
    public const string ApiUrl = "dev.api-url";
    public const string ApiToken = "auth.api-token";
}

public abstract class PreferencesServiceBase : IPreferencesService
{
    protected Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public string? Get(string key)
    {
        lock (Values)
        {
            return Values.TryGetValue(key, out var value) ? value : null;
        }
    }

    public void Set(string key, string? value)
    {
        lock (Values)
        {
            if (value is null)
            {
                Values.Remove(key);
            }
            else
            {
                Values[key] = value;
            }

            Persist();
        }
    }

    public bool GetBool(string key, bool defaultValue = false) =>
        Get(key) is { } raw && bool.TryParse(raw, out var value) ? value : defaultValue;

    public void SetBool(string key, bool value) => Set(key, value ? "true" : "false");

    protected abstract void Persist();
}

public sealed class InMemoryPreferencesService : PreferencesServiceBase
{
    protected override void Persist()
    {
    }
}

/// <summary>Stockage JSON dans le dossier applicatif local (fonctionne sur Android, iOS et desktop).</summary>
public sealed class JsonFilePreferencesService : PreferencesServiceBase
{
    private readonly string _path;

    public JsonFilePreferencesService(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize(json, PreferencesJsonContext.Default.DictionaryStringString);
                if (loaded is not null)
                {
                    foreach (var (k, v) in loaded)
                    {
                        Values[k] = v;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fichier corrompu : on repart des valeurs par défaut.
        }
    }

    protected override void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Values, PreferencesJsonContext.Default.DictionaryStringString));
        }
        catch (Exception)
        {
            // Les préférences sont un confort : un échec d'écriture ne doit pas bloquer l'app.
        }
    }
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class PreferencesJsonContext : JsonSerializerContext;
