namespace BeninConnect.Modules.Jobs.Domain;

/// <summary>Ordre = priorité d'affichage entre doublons (France Travail d'abord).</summary>
public enum JobSource
{
    FranceTravail,
    ReliefWeb,
    Adzuna,
    Remotive
}

public enum ContractType
{
    Cdi,
    Cdd,
    Interim,
    Stage,
    Alternance,
    Freelance,
    Autre
}

public enum JobOfferStatus
{
    Active,
    Expired,

    /// <summary>Masquée : un réimport ne la réaffiche JAMAIS.</summary>
    Hidden
}

public enum HiddenReason
{
    None,
    AlertKeyword,
    Reports,
    Admin
}

public static class JobSources
{
    public static string Label(JobSource source) => source switch
    {
        JobSource.FranceTravail => "France Travail",
        JobSource.ReliefWeb => "ReliefWeb",
        JobSource.Adzuna => "Adzuna",
        JobSource.Remotive => "Remotive",
        _ => source.ToString()
    };

    /// <summary>Intervalle minimal entre deux imports (quotas des API).</summary>
    public static int MinIntervalMinutes(JobSource source) => source switch
    {
        JobSource.FranceTravail => 30,
        JobSource.ReliefWeb => 60,
        _ => 360
    };

    public static int DefaultIntervalMinutes(JobSource source) => source == JobSource.FranceTravail ? 120 : 360;
}
