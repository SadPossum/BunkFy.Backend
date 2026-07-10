namespace Properties.Contracts;

public sealed record PropertyDto(
    Guid PropertyId,
    string Name,
    string Code,
    string TimeZoneId,
    PropertyStatus Status,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? RetiredAtUtc);
