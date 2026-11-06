namespace BeninConnect.App.Core.Utilities;

/// <summary>Erreur réseau simulée (mode développeur).</summary>
public sealed class SimulatedFailureException()
    : Exception("Connexion simulée interrompue. Réessayez dans un instant.");

/// <summary>Erreur métier lisible par l'utilisateur (ex : événement complet).</summary>
public class DomainException(string message) : Exception(message);

public sealed class NotFoundException(string what) : Exception($"{what} introuvable.");

public static class ErrorMessages
{
    public const string Generic = "Une erreur inattendue est survenue. Réessayez.";

    public static string From(Exception exception) => exception switch
    {
        SimulatedFailureException or DomainException or NotFoundException => exception.Message,
        _ => Generic
    };
}
