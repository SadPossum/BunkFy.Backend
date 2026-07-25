namespace BunkFy.Modules.Guests.Contracts;

using System.Collections.Frozen;

public static class GuestDataHoldReasonCodes
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

public enum GuestDataHoldStatus
{
    Unknown = 0,
    Active = 1,
    Released = 2
}

public enum GuestDataHoldAction
{
    Unknown = 0,
    Place = 1,
    Release = 2
}

public sealed record GuestDataHoldDto(
    Guid HoldId,
    Guid PropertyId,
    Guid GuestId,
    string ReasonCode,
    GuestDataHoldStatus Status,
    string PlacedBy,
    DateTimeOffset PlacedAtUtc,
    string? ReleasedBy,
    DateTimeOffset? ReleasedAtUtc,
    long Version);

public sealed record GuestDataHoldReceiptDto(
    Guid ReceiptId,
    Guid IdempotencyKey,
    Guid HoldId,
    GuestDataHoldAction Action,
    Guid PropertyId,
    Guid GuestId,
    string ReasonCode,
    long SelectedGuestVersion,
    long ResultingHoldVersion,
    string ActorId,
    DateTimeOffset CompletedAtUtc);

public sealed record GuestDataHoldListResponse(
    IReadOnlyCollection<GuestDataHoldDto> Holds,
    int Page,
    int PageSize);
