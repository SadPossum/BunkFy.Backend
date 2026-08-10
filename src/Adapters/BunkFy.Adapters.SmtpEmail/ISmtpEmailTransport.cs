namespace BunkFy.Adapters.SmtpEmail;

internal interface ISmtpEmailTransport
{
    ValueTask SendAsync(
        SmtpEmailEnvelope envelope,
        SmtpEmailOptions options,
        CancellationToken cancellationToken);
}
