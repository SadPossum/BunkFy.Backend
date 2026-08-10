namespace BunkFy.Adapters.SmtpEmail;

using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Email;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;

internal sealed class SmtpEmailSender(
    ISmtpEmailTransport transport,
    IOptions<SmtpEmailOptions> options) : IEmailSender
{
    private readonly SmtpEmailOptions settings = options.Value;

    public async ValueTask<EmailSendResult> SendAsync(
        EmailSendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string senderAddress = this.settings.AllowSenderOverride && request.SenderAddress is not null
            ? request.SenderAddress
            : this.settings.DefaultSenderAddress!;
        string? senderName = this.settings.AllowSenderOverride && request.SenderAddress is not null
            ? request.SenderName
            : this.settings.DefaultSenderName;
        string messageId = CreateMessageId(request.IdempotencyKey, senderAddress);
        SmtpEmailEnvelope envelope = new(
            senderAddress,
            senderName,
            request.RecipientAddress,
            request.Subject,
            request.TextBody,
            request.HtmlBody,
            messageId);

        try
        {
            await transport.SendAsync(envelope, this.settings, cancellationToken).ConfigureAwait(false);
            return EmailSendResult.Delivered(messageId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return EmailSendResult.Retry("smtp-timeout");
        }
        catch (SmtpCommandException exception)
        {
            return ClassifyStatusCode(exception.StatusCode);
        }
        catch (AuthenticationException)
        {
            return EmailSendResult.Rejected("smtp-authentication");
        }
        catch (SslHandshakeException)
        {
            return EmailSendResult.Rejected("smtp-tls");
        }
        catch (ServiceNotAuthenticatedException)
        {
            return EmailSendResult.Rejected("smtp-authentication");
        }
        catch (SmtpProtocolException)
        {
            return EmailSendResult.Retry("smtp-protocol");
        }
        catch (ServiceNotConnectedException)
        {
            return EmailSendResult.Retry("smtp-transport");
        }
        catch (TimeoutException)
        {
            return EmailSendResult.Retry("smtp-timeout");
        }
        catch (IOException)
        {
            return EmailSendResult.Retry("smtp-transport");
        }
        catch (SocketException)
        {
            return EmailSendResult.Retry("smtp-transport");
        }
    }

    internal static EmailSendResult ClassifyStatusCode(SmtpStatusCode statusCode)
    {
        int numericStatus = (int)statusCode;
        if (numericStatus is >= 400 and < 500)
        {
            return EmailSendResult.Retry("smtp-transient");
        }

        return numericStatus is >= 500 and < 600
            ? EmailSendResult.Rejected("smtp-rejected")
            : EmailSendResult.Retry("smtp-command");
    }

    internal static string CreateMessageId(string idempotencyKey, string senderAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderAddress);

        byte[] material = Encoding.UTF8.GetBytes($"BunkFy.SmtpEmail.v1\0{idempotencyKey}");
        string digest;
        try
        {
            digest = Convert.ToHexString(SHA256.HashData(material)).ToLowerInvariant();
        }
        finally
        {
            Array.Clear(material);
        }
        string senderDomain = senderAddress[(senderAddress.LastIndexOf('@') + 1)..]
            .ToLowerInvariant();
        return $"bunkfy-{digest}@{senderDomain}";
    }
}
