using ECommerce.Application.Interfaces.Infrastructure;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ECommerce.Infrastructure.Email;

/// <summary>
/// SMTP settings.  The password is deliberately NOT committed to the repo —
/// in production it's supplied via the <c>Email__Password</c> environment
/// variable (systemd) so it never leaks to the public GitHub repo.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "smtp.hostinger.com";
    public int Port { get; set; } = 465;
    /// <summary>Implicit SSL (port 465).  Set false for STARTTLS (port 587).</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>SMTP login — the full mailbox address, e.g. info@code994.az.</summary>
    public string? User { get; set; }
    /// <summary>SMTP password — set via env var in production.</summary>
    public string? Password { get; set; }

    /// <summary>Display name shown to recipients.</summary>
    public string FromName { get; set; } = "Code994";
    /// <summary>From address — defaults to <see cref="User"/> when empty.</summary>
    public string? FromEmail { get; set; }
    /// <summary>Where admin notifications go — defaults to <see cref="User"/>.</summary>
    public string? AdminEmail { get; set; }
}

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _s;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> options, ILogger<SmtpEmailService> logger)
    {
        _s = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_s.User) && !string.IsNullOrWhiteSpace(_s.Password);

    public string? AdminEmail =>
        string.IsNullOrWhiteSpace(_s.AdminEmail) ? _s.User : _s.AdminEmail;

    public async Task<bool> SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogDebug("Email not configured — skipping send to {To}.", toEmail);
            return false;
        }
        if (string.IsNullOrWhiteSpace(toEmail))
            return false;

        // Build the message once; reuse across retry attempts.
        var fromAddress = string.IsNullOrWhiteSpace(_s.FromEmail) ? _s.User! : _s.FromEmail;
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_s.FromName, fromAddress));
        msg.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(toName) ? toEmail : toName, toEmail));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        // Retry transient SMTP failures (cold connection after restart, brief
        // network/TLS hiccups, Hostinger throttling).  Up to 3 attempts with a
        // short backoff.  Without this, the very first send after an app
        // restart sometimes failed while a manual "resend" succeeded.
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = new SmtpClient();
                var socketOptions = _s.UseSsl
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(25));

                await client.ConnectAsync(_s.Host, _s.Port, socketOptions, timeout.Token);
                await client.AuthenticateAsync(_s.User, _s.Password, timeout.Token);
                await client.SendAsync(msg, timeout.Token);
                await client.DisconnectAsync(true, timeout.Token);

                _logger.LogInformation(
                    "Email sent to {To} on attempt {Attempt}: {Subject}", toEmail, attempt, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "Email attempt {Attempt}/{Max} to {To} failed.", attempt, maxAttempts, toEmail);
                if (attempt < maxAttempts)
                {
                    // Linear backoff: 2s, 4s.
                    try { await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct); }
                    catch { /* cancelled — fall through to final failure */ }
                }
            }
        }

        _logger.LogError("Email to {To} failed after {Max} attempts.", toEmail, maxAttempts);
        return false;
    }
}
