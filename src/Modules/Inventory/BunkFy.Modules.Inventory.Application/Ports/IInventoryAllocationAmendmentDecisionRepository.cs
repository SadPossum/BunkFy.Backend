namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Contracts;

public interface IInventoryAllocationAmendmentDecisionRepository
{
    Task<InventoryAllocationAmendmentDecisionRecord?> GetAsync(
        Guid amendmentRequestId,
        CancellationToken cancellationToken);

    Task AddAsync(
        InventoryAllocationAmendmentDecisionRecord decision,
        CancellationToken cancellationToken);
}

public sealed record InventoryAllocationAmendmentDecisionRecord(
    Guid AmendmentRequestId,
    string ScopeId,
    Guid AllocationId,
    Guid ReservationId,
    Guid PropertyId,
    string RequestFingerprint,
    bool Confirmed,
    InventoryAllocationRejectionReason? RejectionReason,
    long? AllocationVersion,
    DateTimeOffset DecidedAtUtc);
