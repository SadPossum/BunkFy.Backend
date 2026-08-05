namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;

internal interface ITenantTerminationCoordinationSignal
{
    Task<bool> EnqueueAsync(
        TenantTerminationProcess process,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}

internal sealed class TenantTerminationCoordinationSignal(
    IOutboxWriterRegistry outboxWriters,
    IIdGenerator ids) : ITenantTerminationCoordinationSignal
{
    public async Task<bool> EnqueueAsync(
        TenantTerminationProcess process,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfEqual(
            occurredAtUtc,
            default,
            nameof(occurredAtUtc));

        if (!TryCreateCoordinates(
                process,
                out TenantTerminationCoordinationAction action,
                out TenantTerminationContributionPhase phase))
        {
            return false;
        }

        IOutboxWriter outbox = outboxWriters.GetRequired(
            DataRightsModuleMetadata.Name);
        await outbox.EnqueueAsync(
            new TenantTerminationCoordinationRequestedIntegrationEvent(
                ids.NewId(),
                process.ScopeId,
                occurredAtUtc,
                process.Id,
                process.Version,
                process.OperationRevision,
                action,
                phase),
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool TryCreateCoordinates(
        TenantTerminationProcess process,
        out TenantTerminationCoordinationAction action,
        out TenantTerminationContributionPhase phase)
    {
        action = process.Status switch
        {
            TenantTerminationProcessStatus.Pending =>
                TenantTerminationCoordinationAction.BeginOwnerPhase,
            TenantTerminationProcessStatus.Running =>
                TenantTerminationCoordinationAction.ReconcileOwnerPhase,
            _ => TenantTerminationCoordinationAction.Unknown
        };
        phase = process.Phase switch
        {
            TenantTerminationProcessPhase.Freeze =>
                TenantTerminationContributionPhase.Freeze,
            TenantTerminationProcessPhase.Export =>
                TenantTerminationContributionPhase.Export,
            TenantTerminationProcessPhase.Destroy =>
                TenantTerminationContributionPhase.Destroy,
            TenantTerminationProcessPhase.Restore =>
                TenantTerminationContributionPhase.Restore,
            TenantTerminationProcessPhase.Verify =>
                TenantTerminationContributionPhase.Unknown,
            _ => TenantTerminationContributionPhase.Unknown
        };

        if (process.Status == TenantTerminationProcessStatus.Pending &&
            process.Phase == TenantTerminationProcessPhase.Verify)
        {
            action = TenantTerminationCoordinationAction.Verify;
            return true;
        }

        return action != TenantTerminationCoordinationAction.Unknown &&
            phase != TenantTerminationContributionPhase.Unknown;
    }
}
