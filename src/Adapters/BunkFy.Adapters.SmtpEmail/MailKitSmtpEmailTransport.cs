namespace BunkFy.Adapters.SmtpEmail;

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

internal sealed class MailKitSmtpEmailTransport : ISmtpEmailTransport
{
    public async ValueTask SendAsync(
        SmtpEmailEnvelope envelope,
        SmtpEmailOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(options);

        MimeMessage message = CreateMessage(envelope);
        using SmtpClient client = new()
        {
            Timeout = checked(options.TimeoutSeconds * 1000)
        };

        try
        {
            await client.ConnectAsync(
                options.Host!,
                options.Port,
                ToSocketOptions(options.SecurityMode),
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(options.Username))
            {
                await client.AuthenticateAsync(
                    options.Username,
                    options.Password!,
                    cancellationToken).ConfigureAwait(false);
            }

            _ = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(quit: true, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // SMTP acceptance is final; cleanup failure must not schedule a duplicate send.
                }
            }
        }
    }

    private static MimeMessage CreateMessage(SmtpEmailEnvelope envelope)
    {
        MimeMessage message = new()
        {
            Subject = envelope.Subject,
            MessageId = envelope.MessageId,
            Body = new BodyBuilder
            {
                TextBody = envelope.TextBody,
                HtmlBody = envelope.HtmlBody
            }.ToMessageBody()
        };
        message.From.Add(new MailboxAddress(envelope.SenderName, envelope.SenderAddress));
        message.To.Add(MailboxAddress.Parse(envelope.RecipientAddress));
        return message;
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurityMode securityMode) =>
        securityMode switch
        {
            SmtpSecurityMode.None => SecureSocketOptions.None,
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new InvalidOperationException("SMTP security mode was not validated.")
        };
}
