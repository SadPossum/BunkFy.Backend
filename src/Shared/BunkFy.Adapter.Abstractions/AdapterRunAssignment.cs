namespace BunkFy.Adapter.Abstractions;

public sealed record AdapterRunAssignment
{
    public AdapterRunAssignment(
        Guid runId,
        Guid leaseId,
        Guid connectionId,
        string scopeId,
        Guid propertyId,
        string adapterType,
        AdapterExecutionMode executionMode,
        DateTimeOffset assignedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        string? checkpoint)
    {
        this.RunId = AdapterProtocolGuards.Required(runId, nameof(runId));
        this.LeaseId = AdapterProtocolGuards.Required(leaseId, nameof(leaseId));
        this.ConnectionId = AdapterProtocolGuards.Required(connectionId, nameof(connectionId));
        this.ScopeId = AdapterProtocolGuards.Required(scopeId, 128, nameof(scopeId));
        this.PropertyId = AdapterProtocolGuards.Required(propertyId, nameof(propertyId));
        this.AdapterType = AdapterProtocolGuards.StableKey(
            adapterType,
            AdapterProtocolLimits.AdapterTypeMaxLength,
            nameof(adapterType));
        if (executionMode == AdapterExecutionMode.Unknown || !Enum.IsDefined(executionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode));
        }

        if (leaseExpiresAtUtc <= assignedAtUtc)
        {
            throw new ArgumentException("The lease must expire after assignment.", nameof(leaseExpiresAtUtc));
        }

        this.ExecutionMode = executionMode;
        this.AssignedAtUtc = assignedAtUtc;
        this.LeaseExpiresAtUtc = leaseExpiresAtUtc;
        this.Checkpoint = AdapterProtocolGuards.Optional(
            checkpoint,
            AdapterProtocolLimits.CheckpointMaxLength,
            nameof(checkpoint));
    }

    public Guid RunId { get; }
    public Guid LeaseId { get; }
    public Guid ConnectionId { get; }
    public string ScopeId { get; }
    public Guid PropertyId { get; }
    public string AdapterType { get; }
    public AdapterExecutionMode ExecutionMode { get; }
    public DateTimeOffset AssignedAtUtc { get; }
    public DateTimeOffset LeaseExpiresAtUtc { get; }
    public string? Checkpoint { get; }
}
