namespace BunkFy.Modules.Reservations.Contracts;

using System.Collections.Frozen;

public static class ReservationDataHoldReasonCodes
{
    public const string LegalObligation = "legal-obligation";
    public const string RegulatoryRequest = "regulatory-request";
    public const string Dispute = "dispute";
    public const string SecurityInvestigation = "security-investigation";

    public static IReadOnlySet<string> All { get; } =
        new[]
        {
            LegalObligation,
            RegulatoryRequest,
            Dispute,
            SecurityInvestigation
        }.ToFrozenSet(StringComparer.Ordinal);
}

public enum ReservationDataHoldStatus
{
    Unknown = 0,
    Active = 1,
    Released = 2
}

public enum ReservationDataHoldAction
{
    Unknown = 0,
    Place = 1,
    Release = 2
}

public sealed record ReservationDataHoldDto(
    Guid HoldId,
    Guid PropertyId,
    Guid ReservationId,
    string ReasonCode,
    ReservationDataHoldStatus Status,
    string PlacedBy,
    DateTimeOffset PlacedAtUtc,
    string? ReleasedBy,
    DateTimeOffset? ReleasedAtUtc,
    long Version);

public sealed record ReservationDataHoldReceiptDto(
    Guid ReceiptId,
    Guid HoldId,
    ReservationDataHoldAction Action,
    Guid PropertyId,
    Guid ReservationId,
    string ReasonCode,
    long SelectedReservationVersion,
    long SelectedDetailsRevision,
    long ResultingHoldVersion,
    DateTimeOffset CompletedAtUtc);

public sealed record ReservationDataHoldListResponse(
    IReadOnlyCollection<ReservationDataHoldDto> Holds,
    int Page,
    int PageSize);
