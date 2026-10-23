using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Ntca.Core;

namespace Ntca.Web.Services;

public interface ISmtpTransport
{
    Task SendAsync(EmailOptions email, string toEmail, string subject, string textBody, string? htmlBody, CancellationToken ct = default);
}

public sealed class MailKitSmtpTransport : ISmtpTransport
{
    public async Task SendAsync(
        EmailOptions email,
        string toEmail,
        string subject,
        string textBody,
        string? htmlBody,
        CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        var body = new BodyBuilder
        {
            TextBody = textBody,
            HtmlBody = htmlBody ?? $"<pre>{System.Net.WebUtility.HtmlEncode(textBody)}</pre>"
        };
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = 30_000;
        var port = email.SmtpPort > 0 ? email.SmtpPort : 587;
        await client.ConnectAsync(email.SmtpHost, port, SecureSocketOptions.StartTls, ct);
        if (!string.IsNullOrWhiteSpace(email.SmtpUser))
            await client.AuthenticateAsync(email.SmtpUser, email.SmtpPassword ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
