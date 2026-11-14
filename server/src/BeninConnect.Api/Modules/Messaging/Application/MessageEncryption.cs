using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Messaging.Application;

/// <summary>État du chiffrement des messages existants (console).</summary>
public sealed record MessageEncryptionStatusDto(
    int TotalMessages, int PlainMessages, bool Running, int Processed, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, string? Error,
    string? StartedBy);

/// <summary>Tâche de fond : chiffre les messages enregistrés avant l'activation du chiffrement.</summary>
public interface IMessageEncryptionJob
{
    Task<MessageEncryptionStatusDto> StatusAsync(CancellationToken ct);

    /// <summary>Faux si une exécution est déjà en cours.</summary>
    bool TryStart(string startedBy);
}

public sealed record GetMessageEncryptionStatusQuery : IQuery<MessageEncryptionStatusDto>;

public sealed record StartMessageEncryptionCommand : ICommand<MessageEncryptionStatusDto>;

/// <summary>Rôles SuperAdmin, Admin et Développeur ; le lancement est journalisé.</summary>
public sealed class MessageEncryptionHandlers(IMessageEncryptionJob job, IAdminSession admin) :
    IRequestHandler<GetMessageEncryptionStatusQuery, MessageEncryptionStatusDto>,
    IRequestHandler<StartMessageEncryptionCommand, MessageEncryptionStatusDto>
{
    private static readonly string[] Roles = ["SuperAdmin", "Admin", "Developer"];

    public async Task<MessageEncryptionStatusDto> Handle(GetMessageEncryptionStatusQuery request, CancellationToken ct)
    {
        await EnsureRoleAsync(ct);
        return await job.StatusAsync(ct);
    }

    public async Task<MessageEncryptionStatusDto> Handle(StartMessageEncryptionCommand request, CancellationToken ct)
    {
        var who = await EnsureRoleAsync(ct);
        if (!job.TryStart(who.DisplayName))
        {
            throw new ConflictException("Le chiffrement des messages est déjà en cours.");
        }

        await admin.LogAsync("Chiffrement des messages existants lancé", "Messaging", null, "Tâche de fond", ct);
        return await job.StatusAsync(ct);
    }

    private async Task<AdminInfo> EnsureRoleAsync(CancellationToken ct)
    {
        var who = await admin.GetAsync(ct);
        return Roles.Contains(who.Role) ? who : throw new ForbiddenException("Réservé aux administrateurs.");
    }
}
