namespace Integration.Tests.Support;

internal sealed record OutboxSnapshot(
    Guid Id,
    DateTimeOffset? ProcessedAtUtc,
    string? LockedBy,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset? NextAttemptAtUtc,
    int Attempts);
