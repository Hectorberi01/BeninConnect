using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Administration.Domain;

public enum AdminRole
{
    SuperAdmin,
    Admin,
    Moderator,
    Accountant,
    Developer
}

/// <summary>Compte de la console d'administration : distinct des membres de l'application.</summary>
public sealed class AdminUser
{
    private AdminUser()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public AdminRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public static AdminUser Create(Guid id, string email, string displayName, string passwordHash, AdminRole role, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Le nom est obligatoire.");
        }

        return new AdminUser
        {
            Id = id,
            Email = NormalizeEmail(email),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = now
        };
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;

    public void Update(string displayName, AdminRole role, bool isActive)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? DisplayName : displayName.Trim();
        Role = role;
        IsActive = isActive;
    }
}

/// <summary>Trace d'une action d'administration (qui, quoi, sur quoi, quand).</summary>
public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid AdminId { get; private set; }
    public string AdminName { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid? TargetId { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public DateTimeOffset At { get; private set; }

    public static AuditEntry Create(AdminUser admin, string action, string targetType, Guid? targetId, string details, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        AdminId = admin.Id,
        AdminName = admin.DisplayName,
        Action = action,
        TargetType = targetType,
        TargetId = targetId,
        Details = details.Length > 500 ? details[..500] : details,
        At = now
    };
}
