namespace BunkFy.Modules.Ingestion.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record DispatchNormalizedReservationObservationCommand(
    Guid ReceiptId,
    NormalizedReservationObservation Observation)
    : ITransactionalCommand<ReservationObservationDispatchResult>;

public enum NormalizedReservationObservationKind
{
    Unknown = 0,
    Upsert = 1,
    Cancel = 2
}

public sealed record NormalizedReservationObservation(
    NormalizedReservationObservationKind Kind,
    long? SourceSequence,
    DateOnly? Arrival,
    DateOnly? Departure,
    IReadOnlyCollection<Guid> InventoryUnitIds,
    string? PrimaryGuestName,
    string? Email,
    string? Phone,
    int? GuestCount,
    string? Notes);

public sealed record ReservationObservationDispatchResult(
    Guid ReceiptId,
    Guid SourceLinkId,
    Guid? OperationId,
    ReservationObservationDispatchDisposition Disposition);

public enum ReservationObservationDispatchDisposition
{
    Unknown = 0,
    Dispatched = 1,
    Deferred = 2,
    Replay = 3,
    Stale = 4,
    ProposalRequired = 5
}
