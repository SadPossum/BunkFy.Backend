namespace Properties.Contracts;

public sealed record PropertyDto(
    Guid PropertyId,
    string Name,
    string Code,
    string TimeZoneId,
    PropertyStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
