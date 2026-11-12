using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Identity.Domain;

public sealed class User
{
    public const int MaxInterests = 6;

    private User()
    {
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Profession { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string OriginCity { get; private set; } = string.Empty;
    public string Bio { get; private set; } = string.Empty;
    public List<string> Interests { get; private set; } = [];
    public string AvatarColor { get; private set; } = "#087F5B";
    public string? CoverImage { get; private set; }
    public string? AvatarImage { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public string? VerificationCode { get; private set; }
    public string? PasswordResetCode { get; private set; }
    public DateTimeOffset? PasswordResetExpiresAt { get; private set; }
    public int PasswordResetAttempts { get; private set; }
    public bool IsSuspended { get; private set; }

    /// <summary>Compte officiel BéninConnect (publications éditoriales) : badge vérifié, aucune connexion possible.</summary>
    public bool IsOfficial { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTimeOffset? SuspendedAt { get; private set; }

    public string DisplayName => $"{FirstName} {LastName}".Trim();

    public static User Register(
        Guid id,
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        string country,
        string city,
        DateTimeOffset now,
        string avatarColor,
        string verificationCode) =>
        new()
        {
            Id = id,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            Country = country.Trim(),
            City = city.Trim(),
            JoinedAt = now,
            AvatarColor = avatarColor,
            CoverImage = "Profile/cover_02.jpg",
            VerificationCode = verificationCode
        };

    /// <summary>Profil complet déjà vérifié (jeu de démonstration).</summary>
    public static User CreateVerified(
        Guid id, string firstName, string lastName, string email, string passwordHash,
        string profession, string country, string city, string originCity, string bio,
        IEnumerable<string> interests, string avatarColor, string? coverImage, DateTimeOffset joinedAt) =>
        new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            Profession = profession,
            Country = country,
            City = city,
            OriginCity = originCity,
            Bio = bio,
            Interests = interests.ToList(),
            AvatarColor = avatarColor,
            CoverImage = coverImage,
            JoinedAt = joinedAt,
            IsEmailVerified = true
        };

    /// <summary>Compte officiel : vérifié, mot de passe aléatoire jamais communiqué (aucune connexion).</summary>
    public static User CreateOfficial(Guid id, string name, string email, string passwordHash, string bio, DateTimeOffset now) =>
        new()
        {
            Id = id,
            FirstName = name.Trim(),
            LastName = string.Empty,
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            Bio = bio,
            AvatarColor = "#087F5B",
            CoverImage = "Profile/cover_01.jpg",
            JoinedAt = now,
            IsEmailVerified = true,
            IsOfficial = true
        };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void VerifyEmail(string code)
    {
        if (IsEmailVerified)
        {
            return;
        }

        if (VerificationCode is null || code.Trim() != VerificationCode)
        {
            throw new DomainException("Code de vérification incorrect.");
        }

        IsEmailVerified = true;
        VerificationCode = null;
    }

    public void RenewVerificationCode(string code)
    {
        if (!IsEmailVerified)
        {
            VerificationCode = code;
        }
    }

    public const int MaxResetAttempts = 5;

    public void RequestPasswordReset(string code, DateTimeOffset expiresAt)
    {
        PasswordResetCode = code;
        PasswordResetExpiresAt = expiresAt;
        PasswordResetAttempts = 0;
    }

    /// <summary>Code à usage unique, valable jusqu'à expiration, 5 essais maximum.</summary>
    public void ResetPassword(string code, string newPasswordHash, DateTimeOffset now)
    {
        if (PasswordResetCode is null || PasswordResetExpiresAt is null || now > PasswordResetExpiresAt || PasswordResetAttempts >= MaxResetAttempts)
        {
            ClearReset();
            throw new DomainException("Code expiré : demandez un nouveau code.");
        }

        if (code.Trim() != PasswordResetCode)
        {
            PasswordResetAttempts++;
            throw new DomainException("Code incorrect.");
        }

        PasswordHash = newPasswordHash;
        IsEmailVerified = true; // le code a été reçu par email
        VerificationCode = null;
        ClearReset();
    }

    private void ClearReset()
    {
        PasswordResetCode = null;
        PasswordResetExpiresAt = null;
        PasswordResetAttempts = 0;
    }

    public void UpdateProfile(string firstName, string lastName, string profession, string country, string city, string bio, IReadOnlyCollection<string> interests)
    {
        if (interests.Count > MaxInterests)
        {
            throw new DomainException($"Maximum {MaxInterests} centres d'intérêt.");
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Profession = profession.Trim();
        Country = country.Trim();
        City = city.Trim();
        Bio = bio.Trim();
        Interests = interests.Distinct().ToList();
    }

    public void Suspend(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Indiquez le motif de la suspension.");
        }

        IsSuspended = true;
        SuspensionReason = reason.Trim();
        SuspendedAt = now;
    }

    public void Reactivate()
    {
        IsSuspended = false;
        SuspensionReason = null;
        SuspendedAt = null;
    }

    /// <summary>null : inchangé ; chaîne vide : retirée.</summary>
    public void UpdateImages(string? avatarImage, string? coverImage)
    {
        if (avatarImage is not null)
        {
            AvatarImage = avatarImage.Length == 0 ? null : avatarImage;
        }

        if (coverImage is not null)
        {
            CoverImage = coverImage.Length == 0 ? null : coverImage;
        }
    }
}
