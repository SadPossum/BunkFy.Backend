namespace BunkFy.Modules.Ingestion.Contracts;

public enum ChangeProposalStatus
{
    Unknown = 0,
    Pending = 1,
    Applying = 2,
    Applied = 3,
    Rejected = 4,
    Superseded = 5,
    Stale = 6,
    Failed = 7
}

public enum SensitiveHistoryStatus
{
    Unknown = 0,
    Available = 1,
    Redacted = 2
}

public sealed record ChangeProposalDto(
    Guid ProposalId,
    Guid PropertyId,
    Guid ConnectionId,
    Guid ReceiptId,
    Guid ReservationId,
    long BaseReservationDetailsRevision,
    string ReasonCode,
    string? Diff,
    SensitiveHistoryStatus SensitiveHistoryStatus,
    DateTimeOffset? SensitiveDataRetainUntilUtc,
    DateTimeOffset? SensitiveDataRedactedAtUtc,
    ChangeProposalStatus Status,
    string? DecisionActor,
    string? DecisionReason,
    Guid? ProductOperationId,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record ChangeProposalSummaryDto(
    Guid ProposalId,
    Guid PropertyId,
    Guid ConnectionId,
    Guid ReceiptId,
    Guid ReservationId,
    long BaseReservationDetailsRevision,
    string ReasonCode,
    SensitiveHistoryStatus SensitiveHistoryStatus,
    DateTimeOffset? SensitiveDataRetainUntilUtc,
    DateTimeOffset? SensitiveDataRedactedAtUtc,
    ChangeProposalStatus Status,
    string? DecisionActor,
    string? DecisionReason,
    Guid? ProductOperationId,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record ChangeProposalListResponse(
    IReadOnlyCollection<ChangeProposalSummaryDto> Proposals,
    int Page,
    int PageSize,
    long TotalCount);
