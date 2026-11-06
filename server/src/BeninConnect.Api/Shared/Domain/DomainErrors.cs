namespace BeninConnect.Shared.Domain;

/// <summary>Règle métier violée : message lisible renvoyé tel quel au client (HTTP 400/409).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Ressource absente (HTTP 404).</summary>
public sealed class NotFoundException(string resource) : Exception($"{resource} introuvable.");

/// <summary>Action interdite pour l'utilisateur courant (HTTP 403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>Conflit d'unicité ou d'état (HTTP 409).</summary>
public sealed class ConflictException(string message) : DomainException(message);
