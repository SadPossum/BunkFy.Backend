namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

[IntegrationEventHandler(
    DataRightsModuleMetadata.GuestCorrectionAppliedHandlerName,
    RequiresExplicitProducerBinding = true)]
internal sealed class GuestDataRightsCorrectionAppliedHandler(
    DataRightsCorrectionCompletionCoordinator coordinator)
    : IIntegrationEventHandler<DataRightsCorrectionAppliedIntegrationEvent>
{
    public Task HandleAsync(
        DataRightsCorrectionAppliedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        coordinator.CompleteAsync(integrationEvent, cancellationToken);
}

[IntegrationEventHandler(
    DataRightsModuleMetadata.ReservationCorrectionAppliedHandlerName,
    RequiresExplicitProducerBinding = true)]
internal sealed class ReservationDataRightsCorrectionAppliedHandler(
    DataRightsCorrectionCompletionCoordinator coordinator)
    : IIntegrationEventHandler<DataRightsCorrectionAppliedIntegrationEvent>
{
    public Task HandleAsync(
        DataRightsCorrectionAppliedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        coordinator.CompleteAsync(integrationEvent, cancellationToken);
}

internal sealed class DataRightsCorrectionCompletionCoordinator(
    IDataRightsCaseRepository cases,
    IDataRightsCorrectionExecutionRepository executions,
    ISystemClock clock)
{
    private const string SystemActor = "system:data-rights-correction";

    public async Task CompleteAsync(
        DataRightsCorrectionAppliedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        DataRightsCorrectionExecution? execution = await executions.GetAsync(
            integrationEvent.PropertyId,
            integrationEvent.CaseId,
            integrationEvent.ExecutionId,
            cancellationToken).ConfigureAwait(false);
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            DataRightsCaseScope.ForProperty(integrationEvent.PropertyId),
            integrationEvent.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (execution is null ||
            dataRightsCase is null ||
            !string.Equals(
                execution.ScopeId,
                integrationEvent.TenantId,
                StringComparison.Ordinal) ||
            !execution.MatchesCompletion(
                integrationEvent.ExecutionId,
                integrationEvent.PropertyId,
                integrationEvent.CaseId,
                integrationEvent.ApprovalRevision,
                integrationEvent.OwnerKey,
                integrationEvent.RecordType,
                integrationEvent.RecordId,
                integrationEvent.SelectedRecordVersion,
                integrationEvent.FieldPolicyKey) ||
            dataRightsCase.ExecutionRevision != execution.ExecutionRevision ||
            integrationEvent.OccurredAtUtc > clock.UtcNow)
        {
            throw new InvalidOperationException(
                "DataRights.CorrectionCompletionCoordinatesInvalid");
        }

        Result ownerCompleted = execution.Complete(
            execution.Version,
            integrationEvent.ReceiptContractVersion,
            integrationEvent.ReceiptId,
            integrationEvent.CurrentRecordVersion,
            integrationEvent.ChangedFieldKeys.Count,
            integrationEvent.ChangedFieldsSha256,
            integrationEvent.ReceiptSha256,
            integrationEvent.OccurredAtUtc);
        if (ownerCompleted.IsFailure)
        {
            throw new InvalidOperationException(ownerCompleted.Error.Code);
        }

        Result caseCompleted = dataRightsCase.CompleteCorrectionExecution(
            dataRightsCase.Version,
            integrationEvent.ApprovalRevision,
            SystemActor,
            integrationEvent.OccurredAtUtc,
            clock.UtcNow);
        if (caseCompleted.IsFailure)
        {
            throw new InvalidOperationException(caseCompleted.Error.Code);
        }
    }
}
