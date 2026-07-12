namespace BunkFy.Modules.Inventory.Persistence;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;

public sealed class InventoryAllocationAmendmentDecision
{
    private InventoryAllocationAmendmentDecision() { }

    internal InventoryAllocationAmendmentDecision(InventoryAllocationAmendmentDecisionRecord record)
    {
        this.Id = record.AmendmentRequestId;
        this.ScopeId = record.ScopeId;
        this.AllocationId = record.AllocationId;
        this.ReservationId = record.ReservationId;
        this.PropertyId = record.PropertyId;
        this.RequestFingerprint = record.RequestFingerprint;
        this.Confirmed = record.Confirmed;
        this.RejectionReason = record.RejectionReason;
        this.AllocationVersion = record.AllocationVersion;
        this.DecidedAtUtc = record.DecidedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid AllocationId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid PropertyId { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public bool Confirmed { get; private set; }
    public InventoryAllocationRejectionReason? RejectionReason { get; private set; }
    public long? AllocationVersion { get; private set; }
    public DateTimeOffset DecidedAtUtc { get; private set; }

    internal InventoryAllocationAmendmentDecisionRecord ToRecord() => new(
        this.Id,
        this.ScopeId,
        this.AllocationId,
        this.ReservationId,
        this.PropertyId,
        this.RequestFingerprint,
        this.Confirmed,
        this.RejectionReason,
        this.AllocationVersion,
        this.DecidedAtUtc);
}
