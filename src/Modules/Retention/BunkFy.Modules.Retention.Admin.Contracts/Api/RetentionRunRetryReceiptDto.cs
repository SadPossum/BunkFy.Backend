namespace BunkFy.Modules.Retention.Admin.Contracts;

public sealed record RetentionRunRetryReceiptDto(
    Guid RunId,
    DateTimeOffset? ScheduledAtUtc);
