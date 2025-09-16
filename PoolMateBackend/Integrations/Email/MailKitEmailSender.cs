using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;

namespace PoolMate.Api.Integrations.Email;

public class MailKitEmailSender(IOptions<EmailSettings> opt) : IEmailSender
{
    private readonly EmailSettings _cfg = opt.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("PoolMate", _cfg.SenderEmail));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_cfg.SmtpServer, _cfg.SmtpPort, false, ct);
        await client.AuthenticateAsync(_cfg.SenderEmail, _cfg.SenderPassword, ct);
        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);
    }
}
