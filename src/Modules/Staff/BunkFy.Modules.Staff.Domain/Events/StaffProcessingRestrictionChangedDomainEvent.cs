namespace BunkFy.Modules.Staff.Domain.Events;

using Gma.Framework.Domain;

public sealed record StaffProcessingRestrictionChangedDomainEvent : ScopedDomainEvent
{
    public StaffProcessingRestrictionChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid staffMemberId,
        int contractVersion,
        long projectionRevision,
        bool isRestricted)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.StaffMemberId = staffMemberId;
        this.ContractVersion = contractVersion;
        this.ProjectionRevision = projectionRevision;
        this.IsRestricted = isRestricted;
    }

    public Guid StaffMemberId { get; }
    public int ContractVersion { get; }
    public long ProjectionRevision { get; }
    public bool IsRestricted { get; }
}
