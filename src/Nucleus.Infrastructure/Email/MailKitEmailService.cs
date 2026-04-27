using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Infrastructure.Email;

/// <summary>
/// Sends email via MailKit (SMTP). Configure with:
///   SMTP_HOST, SMTP_PORT (default 587), SMTP_USER, SMTP_PASS, SMTP_FROM
/// All five are optional — if SMTP_HOST is absent the service is disabled and no-ops gracefully.
/// </summary>
public class MailKitEmailService(IConfiguration config, ILogger<MailKitEmailService> logger) : IEmailService
{
    private readonly string? _host = config["SMTP_HOST"];
    private readonly int _port = int.TryParse(config["SMTP_PORT"], out var p) ? p : 587;
    private readonly string? _user = config["SMTP_USER"];
    private readonly string? _pass = config["SMTP_PASS"];
    private readonly string _from = config["SMTP_FROM"] ?? "noreply@nucleus.app";

    public bool IsConfigured => !string.IsNullOrEmpty(_host);

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("SMTP not configured — skipping email to {To} (subject: {Subject})", toEmail, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Nucleus", _from));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_host!, _port, SecureSocketOptions.StartTlsWhenAvailable, ct);

        if (!string.IsNullOrEmpty(_user) && !string.IsNullOrEmpty(_pass))
            await client.AuthenticateAsync(_user, _pass, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Email sent to {To} — {Subject}", toEmail, subject);
    }
}
