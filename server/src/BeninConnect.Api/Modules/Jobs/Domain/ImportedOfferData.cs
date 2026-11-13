namespace BeninConnect.Modules.Jobs.Domain;

/// <summary>Offre lue chez une source, déjà normalisée (texte brut, pays ISO2, contrat).</summary>
public sealed record ImportedOfferData(
    string ExternalId,
    string Title,
    string Description,
    string? CompanyName,
    string? CompanyLogoUrl,
    string Country,
    string LocationLabel,
    string? PostalCode,
    string? Department,
    double? Latitude,
    double? Longitude,
    ContractType ContractType,
    string? ContractLabel,
    bool IsRemote,
    string? WorkingTime,
    string? SalaryLabel,
    string? SectorLabel,
    string ApplyUrl,
    string? SourceLabel,
    DateTimeOffset PublishedAt,
    DateTimeOffset? ClosingAt);

/// <summary>Recherche d'import configurée dans la console. Les champs utiles dépendent de la source.</summary>
public sealed record JobSearchDefinition(
    string Key,
    string? Keywords = null,
    string? Region = null,
    string? Department = null,
    string? ContractType = null,
    string? Country = null,
    string? Category = null,
    string? Location = null);
