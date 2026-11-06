using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

/// <summary>Socle commun : accès au store partagé, latence et erreurs simulées, notifications de changement.</summary>
public abstract class MockRepositoryBase(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
{
    private static readonly Random FailureRandom = new();

    protected MockDataStore Store { get; } = store;

    protected IMessenger Messenger { get; } = messenger;

    protected TimeProvider Clock { get; } = clock;

    protected DateTimeOffset Now => Clock.GetLocalNow();

    /// <summary>Simule un aller-retour réseau. Ne pas utiliser ConfigureAwait(false) : la suite s'exécute sur le thread UI.</summary>
    protected async Task SimulateNetworkAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (developer.Latency > TimeSpan.Zero)
        {
            await Task.Delay(developer.Latency, cancellationToken);
        }

        if (developer.SimulateErrors && FailureRandom.NextDouble() < developer.FailureRate)
        {
            throw new SimulatedFailureException();
        }
    }

    protected static Guid NewId() => Guid.NewGuid();

    protected static bool Matches(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
        || (value is not null && Normalize(value).Contains(Normalize(query), StringComparison.OrdinalIgnoreCase));

    /// <summary>Recherche insensible aux accents (« benin » trouve « Bénin »).</summary>
    protected static string Normalize(string value)
    {
        var decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var chars = decomposed.Where(c =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray());
    }
}
