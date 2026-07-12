namespace BunkFy.Modules.Staff.Domain.Entities;

using Gma.Framework.Domain.Models;
using Gma.Framework.Domain;

public sealed class StaffPropertyAssignment : Entity<Guid>, IScopedEntity
{
    public const int JobTitleMaxLength = 128;
    public const int ActorIdMaxLength = 200;
    public const int ReasonMaxLength = 1000;

    private StaffPropertyAssignment() { }

    internal StaffPropertyAssignment(Guid id, string scopeId, Guid staffMemberId, Guid propertyId, string? propertyJobTitle,
        bool isPrimary, DateOnly effectiveFrom, string actorId, DateTimeOffset nowUtc, long staffVersion)
        : base(id)
    {
        this.ScopeId = scopeId;
        this.StaffMemberId = staffMemberId;
        this.PropertyId = propertyId;
        this.PropertyJobTitle = propertyJobTitle;
        this.IsPrimary = isPrimary;
        this.IsCurrent = true;
        this.EffectiveFrom = effectiveFrom;
        this.AssignedBy = actorId;
        this.AssignedAtUtc = nowUtc;
        this.AssignedAtVersion = staffVersion;
    }

    public Guid StaffMemberId { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public string? PropertyJobTitle { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string AssignedBy { get; private set; } = string.Empty;
    public DateTimeOffset AssignedAtUtc { get; private set; }
    public long AssignedAtVersion { get; private set; }
    public string? UnassignedBy { get; private set; }
    public string? UnassignmentReason { get; private set; }
    public DateTimeOffset? UnassignedAtUtc { get; private set; }
    public long? UnassignedAtVersion { get; private set; }

    internal void DemotePrimary() => this.IsPrimary = false;

    internal void End(DateOnly effectiveTo, string actorId, string reason, DateTimeOffset nowUtc, long staffVersion)
    {
        this.IsCurrent = false;
        this.IsPrimary = false;
        this.EffectiveTo = effectiveTo;
        this.UnassignedBy = actorId;
        this.UnassignmentReason = reason;
        this.UnassignedAtUtc = nowUtc;
        this.UnassignedAtVersion = staffVersion;
    }
}
