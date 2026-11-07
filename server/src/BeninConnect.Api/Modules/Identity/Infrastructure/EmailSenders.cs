using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using BeninConnect.Modules.Identity.Application;
using Microsoft.Extensions.Logging;

namespace BeninConnect.Modules.Identity.Infrastructure;

/// <summary>Section « Email ». Provider : log (développement), resend ou smtp (production).</summary>
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

    /// <summary>Clé API Resend (re_...), domaine d'envoi vérifié dans Resend.</summary>
    public string ResendApiKey { get; set; } = string.Empty;

    public bool IsSmtp => Provider.Equals("smtp", StringComparison.OrdinalIgnoreCase);
    public bool IsResend => Provider.Equals("resend", StringComparison.OrdinalIgnoreCase);
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

/// <summary>Production : API HTTP Resend (https://resend.com), port 443, pas de SMTP à ouvrir.</summary>
public sealed class ResendEmailSender(HttpClient http, EmailSettings settings, ILogger<ResendEmailSender> logger) : IEmailSender
{
    public const string BaseAddress = "https://api.resend.com/";

    public async Task SendAsync(string to, string subject, string textBody, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{settings.FromName} <{settings.From}>",
                to = new[] { to },
                subject,
                text = textBody,
                html = ToHtml(textBody)
            })
        };
        request.Headers.Authorization = new("Bearer", settings.ResendApiKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Pas d'échec visible pour le membre (il peut redemander le code) ; l'erreur reste dans les journaux.
                logger.LogError("Resend a refusé l'email vers {To} ({Subject}) : {Status} {Body}",
                    to, subject, (int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Resend injoignable pour {To} ({Subject})", to, subject);
        }
    }

    /// <summary>Version HTML minimale du texte (meilleur affichage et délivrabilité que le texte seul).</summary>
    internal static string ToHtml(string text) =>
        "<div style=\"font-family:Arial,sans-serif;font-size:15px;line-height:1.5;color:#1f2933\">"
        + string.Join("<br>", WebUtility.HtmlEncode(text).Split('\n'))
        + "</div>";
}
