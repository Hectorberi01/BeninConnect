using System.Net;
using System.Net.Mail;
using BeninConnect.Modules.Identity.Application;
using Microsoft.Extensions.Logging;

namespace BeninConnect.Modules.Identity.Infrastructure;

/// <summary>Section « Email ». Provider : log (développement) ou smtp (production).</summary>
public sealed class EmailSettings
{
    public const string Section = "Email";

    public string Provider { get; set; } = "log";
    public string From { get; set; } = "no-reply@beninconnect.app";
    public string FromName { get; set; } = "BéninConnect";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;

    public bool IsSmtp => Provider.Equals("smtp", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Développement : l'email est écrit dans les journaux (le code y est lisible).</summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string textBody, CancellationToken ct)
    {
        logger.LogInformation("Email (non envoyé) à {To} : {Subject}\n{Body}", to, subject, textBody);
        return Task.CompletedTask;
    }
}

/// <summary>Production : SMTP authentifié (Brevo, Mailjet, OVH, Gmail Workspace…), STARTTLS sur 587.</summary>
public sealed class SmtpEmailSender(EmailSettings settings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string textBody, CancellationToken ct)
    {
        using var message = new MailMessage(new MailAddress(settings.From, settings.FromName), new MailAddress(to))
        {
            Subject = subject,
            Body = textBody,
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = string.IsNullOrEmpty(settings.Username) ? null : new NetworkCredential(settings.Username, settings.Password)
        };

        try
        {
            await client.SendMailAsync(message, ct);
        }
        catch (SmtpException ex)
        {
            // Pas d'échec visible pour le membre (il peut redemander le code) ; l'erreur reste dans les journaux.
            logger.LogError(ex, "Envoi d'email impossible vers {To} ({Subject})", to, subject);
        }
    }
}
