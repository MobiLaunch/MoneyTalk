using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MoneyTalk.Core.Accounting;

/// <summary>SMTP connection settings a shop enters for its own mail account (Gmail app password,
/// Office 365, a custom mail server, etc.) — the same "bring your own credentials" pattern already
/// used for Square/QuickBooks/Gemini, just via SMTP instead of OAuth.</summary>
public class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "MoneyTalk";
}

/// <summary>Sends plain-text notification emails (new ticket, house call scheduled, etc.) via
/// MailKit rather than the obsolete <c>System.Net.Mail.SmtpClient</c>. Replaces NovaOps's
/// EmailJS+Gmail-API dual path with one consistent approach that needs no external service
/// account beyond the SMTP credentials the shop already has.</summary>
public class EmailService
{
    private readonly EmailOptions _options;

    public EmailService(EmailOptions options)
    {
        _options = options;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.FromAddress);

    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Email notifications aren't configured yet — set them up on the Integrations page.");
        if (string.IsNullOrWhiteSpace(toAddress)) throw new InvalidOperationException("No recipient email address was provided.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
