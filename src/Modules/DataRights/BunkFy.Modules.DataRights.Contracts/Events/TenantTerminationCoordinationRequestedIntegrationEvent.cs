namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

public enum TenantTerminationCoordinationAction
{
    Unknown = 0,
    BeginOwnerPhase = 1,
    ReconcileOwnerPhase = 2,
    Verify = 3
}

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record TenantTerminationCoordinationRequestedIntegrationEvent
    : TenantIntegrationEvent
{
    public const string EventType =
        "data-rights-tenant-termination-coordination-requested";
    public const int EventVersion = 1;

    public TenantTerminationCoordinationRequestedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid processId,
        long processVersion,
        long operationRevision,
        TenantTerminationCoordinationAction action,
        TenantTerminationContributionPhase phase)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ProcessId = IntegrationEventContractGuards.RequireId(
            processId,
            nameof(processId));
        this.ProcessVersion = processVersion > 0
            ? processVersion
            : throw new ArgumentOutOfRangeException(nameof(processVersion));
        this.OperationRevision = operationRevision > 0
            ? operationRevision
            : throw new ArgumentOutOfRangeException(nameof(operationRevision));
        (this.Action, this.Phase) = RequireCoordinates(action, phase);
    }

    public Guid ProcessId { get; }
    public long ProcessVersion { get; }
    public long OperationRevision { get; }
    public TenantTerminationCoordinationAction Action { get; }
    public TenantTerminationContributionPhase Phase { get; }

    private static (
        TenantTerminationCoordinationAction Action,
        TenantTerminationContributionPhase Phase) RequireCoordinates(
            TenantTerminationCoordinationAction action,
            TenantTerminationContributionPhase phase) =>
        (action, phase) switch
        {
            (TenantTerminationCoordinationAction.BeginOwnerPhase or
                TenantTerminationCoordinationAction.ReconcileOwnerPhase,
                TenantTerminationContributionPhase.Freeze or
                TenantTerminationContributionPhase.Export or
                TenantTerminationContributionPhase.Destroy or
                TenantTerminationContributionPhase.Restore) =>
                (action, phase),
            (TenantTerminationCoordinationAction.Verify,
                TenantTerminationContributionPhase.Unknown) =>
                (action, phase),
            _ => throw new ArgumentException(
                "The tenant-termination coordination coordinates are invalid.",
                nameof(action))
        };
}
