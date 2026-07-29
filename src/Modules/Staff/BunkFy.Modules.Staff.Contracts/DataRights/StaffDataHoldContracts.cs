namespace BunkFy.Modules.Staff.Contracts;

using System.Collections.Frozen;

public static class StaffDataHoldReasonCodes
{
    public const string LegalObligation = "legal-obligation";
    public const string RegulatoryRequest = "regulatory-request";
    public const string Dispute = "dispute";
    public const string SecurityInvestigation =
        "security-investigation";

    public static IReadOnlySet<string> All { get; } =
        new[]
        {
            LegalObligation,
            RegulatoryRequest,
            Dispute,
            SecurityInvestigation
        }.ToFrozenSet(StringComparer.Ordinal);
}

public enum StaffDataHoldStatus
{
    Unknown = 0,
    Active = 1,
    Released = 2
}

public enum StaffDataHoldActionDto
{
    Unknown = 0,
    Place = 1,
    Release = 2
}

public sealed record StaffDataHoldDto(
    Guid HoldId,
    Guid StaffMemberId,
    string ReasonCode,
    StaffDataHoldStatus Status,
    string PlacedBy,
    DateTimeOffset PlacedAtUtc,
    string? ReleasedBy,
    DateTimeOffset? ReleasedAtUtc,
    long Version);

public sealed record StaffDataHoldReceiptDto(
    Guid ReceiptId,
    Guid IdempotencyKey,
    Guid HoldId,
    StaffDataHoldActionDto Action,
    Guid StaffMemberId,
    string ReasonCode,
    long SelectedStaffVersion,
    long ResultingHoldVersion,
    string ActorId,
    DateTimeOffset CompletedAtUtc);

public sealed record StaffDataHoldListResponse(
    IReadOnlyCollection<StaffDataHoldDto> Holds,
    long TotalCount,
    int Page,
    int PageSize);
