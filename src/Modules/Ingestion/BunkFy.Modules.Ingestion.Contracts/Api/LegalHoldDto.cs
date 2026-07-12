namespace BunkFy.Modules.Ingestion.Contracts;

public enum LegalHoldStatus
{
    Unknown = 0,
    Active = 1,
    Released = 2
}

public sealed record LegalHoldDto(
    Guid HoldId,
    Guid PropertyId,
    string Reason,
    LegalHoldStatus Status,
    string PlacedBy,
    DateTimeOffset PlacedAtUtc,
    string? ReleasedBy,
    string? ReleaseReason,
    DateTimeOffset? ReleasedAtUtc,
    long Version);

public sealed record LegalHoldListResponse(
    IReadOnlyCollection<LegalHoldDto> LegalHolds,
    int Page,
    int PageSize,
    long TotalCount);
