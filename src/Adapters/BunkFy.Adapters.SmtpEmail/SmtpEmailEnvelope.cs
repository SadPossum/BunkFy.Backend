namespace BunkFy.Adapters.SmtpEmail;

internal sealed record SmtpEmailEnvelope(
    string SenderAddress,
    string? SenderName,
    string RecipientAddress,
    string Subject,
    string? TextBody,
    string? HtmlBody,
    string MessageId);
