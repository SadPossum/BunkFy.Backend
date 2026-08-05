namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Results;

[IntegrationEventHandler(
    DataRightsModuleMetadata.TenantTerminationCoordinationHandlerName)]
internal sealed class TenantTerminationCoordinationRequestedHandler(
    ITenantTerminationRepository repository,
    IRequestDispatcher dispatcher,
    ITenantTerminationTaskScheduler scheduler)
    : IIntegrationEventHandler<
        TenantTerminationCoordinationRequestedIntegrationEvent>
{
    public async Task HandleAsync(
        TenantTerminationCoordinationRequestedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await repository.GetProcessAsync(
            integrationEvent.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null ||
            !string.Equals(
                process.ScopeId,
                integrationEvent.TenantId,
                StringComparison.Ordinal))
        {
            throw InvalidCoordinates();
        }

        if (process.Version > integrationEvent.ProcessVersion)
        {
            return;
        }

        if (process.Version != integrationEvent.ProcessVersion ||
            process.OperationRevision != integrationEvent.OperationRevision ||
            !MatchesCurrentState(process, integrationEvent))
        {
            throw InvalidCoordinates();
        }

        switch (integrationEvent.Action)
        {
            case TenantTerminationCoordinationAction.BeginOwnerPhase:
                await this.BeginPhaseAsync(
                    process,
                    cancellationToken).ConfigureAwait(false);
                return;
            case TenantTerminationCoordinationAction.ReconcileOwnerPhase:
                await this.ReconcilePhaseAsync(
                    process,
                    cancellationToken).ConfigureAwait(false);
                return;
            case TenantTerminationCoordinationAction.Verify:
                await this.BeginVerificationAsync(
                    process,
                    cancellationToken).ConfigureAwait(false);
                return;
            case TenantTerminationCoordinationAction.Unknown:
                throw InvalidCoordinates();
            default:
                throw InvalidCoordinates();
        }
    }

    private async Task BeginPhaseAsync(
        TenantTerminationProcess process,
        CancellationToken cancellationToken)
    {
        Result<TenantTerminationPhaseStart> result =
            await dispatcher.SendAsync<TenantTerminationPhaseStart>(
                new BeginTenantTerminationPhaseCommand(
                    process.Id,
                    process.Phase,
                    process.Version,
                    TenantTerminationCoordination.ExecutorActorId),
                cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw Failure(result.Error);
        }
    }

    private async Task ReconcilePhaseAsync(
        TenantTerminationProcess process,
        CancellationToken cancellationToken)
    {
        Result<TenantTerminationPhaseReconciliation> result =
            await dispatcher.SendAsync<TenantTerminationPhaseReconciliation>(
                new ReconcileTenantTerminationPhaseCommand(
                    process.Id,
                    process.Phase,
                    process.OperationRevision,
                    process.Version,
                    TenantTerminationCoordination.ExecutorActorId),
                cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw Failure(result.Error);
        }

        if (result.Value.ReadyDispatches.Count > 0)
        {
            await scheduler.EnqueueAsync(
                process.ScopeId,
                result.Value.ReadyDispatches,
                cancellationToken).ConfigureAwait(false);
        }

        if (result.Value.ExportArtifactRequired)
        {
            await scheduler.EnqueueExportArtifactAsync(
                process.ScopeId,
                process.Id,
                process.OperationRevision,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task BeginVerificationAsync(
        TenantTerminationProcess process,
        CancellationToken cancellationToken)
    {
        Result<TenantTerminationVerificationPhaseStart> result =
            await dispatcher.SendAsync<
                TenantTerminationVerificationPhaseStart>(
                    new BeginTenantTerminationVerificationCommand(
                        process.Id,
                        process.Version,
                        TenantTerminationCoordination.ExecutorActorId),
                    cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw Failure(result.Error);
        }

        await scheduler.EnqueueVerificationAsync(
            process.ScopeId,
            result.Value.ProcessId,
            result.Value.VerificationOperationRevision,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool MatchesCurrentState(
        TenantTerminationProcess process,
        TenantTerminationCoordinationRequestedIntegrationEvent integrationEvent)
    {
        TenantTerminationProcessPhase phase = integrationEvent.Phase switch
        {
            TenantTerminationContributionPhase.Freeze =>
                TenantTerminationProcessPhase.Freeze,
            TenantTerminationContributionPhase.Export =>
                TenantTerminationProcessPhase.Export,
            TenantTerminationContributionPhase.Destroy =>
                TenantTerminationProcessPhase.Destroy,
            TenantTerminationContributionPhase.Restore =>
                TenantTerminationProcessPhase.Restore,
            TenantTerminationContributionPhase.Unknown
                when integrationEvent.Action ==
                    TenantTerminationCoordinationAction.Verify =>
                TenantTerminationProcessPhase.Verify,
            _ => TenantTerminationProcessPhase.Unknown
        };
        TenantTerminationProcessStatus status = integrationEvent.Action switch
        {
            TenantTerminationCoordinationAction.BeginOwnerPhase or
                TenantTerminationCoordinationAction.Verify =>
                TenantTerminationProcessStatus.Pending,
            TenantTerminationCoordinationAction.ReconcileOwnerPhase =>
                TenantTerminationProcessStatus.Running,
            _ => TenantTerminationProcessStatus.Unknown
        };
        return process.Phase == phase && process.Status == status;
    }

    private static InvalidOperationException InvalidCoordinates() =>
        new("DataRights.TenantTerminationCoordinationCoordinatesInvalid");

    private static InvalidOperationException Failure(Error error) =>
        new($"{error.Code}: {error.Message}");
}
