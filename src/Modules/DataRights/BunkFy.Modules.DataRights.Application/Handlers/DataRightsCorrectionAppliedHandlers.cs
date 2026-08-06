namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
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

[IntegrationEventHandler(
    DataRightsModuleMetadata.StaffCorrectionAppliedHandlerName,
    RequiresExplicitProducerBinding = true)]
internal sealed class StaffDataRightsCorrectionAppliedHandler(
    DataRightsCorrectionCompletionCoordinator coordinator)
    : IIntegrationEventHandler<DataRightsTenantCorrectionAppliedIntegrationEvent>
{
    public Task HandleAsync(
        DataRightsTenantCorrectionAppliedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        coordinator.CompleteAsync(integrationEvent, cancellationToken);
}

[IntegrationEventHandler(
    DataRightsModuleMetadata.WorkspacesCorrectionAppliedHandlerName,
    RequiresExplicitProducerBinding = true)]
internal sealed class WorkspacesDataRightsCorrectionAppliedHandler(
    DataRightsCorrectionCompletionCoordinator coordinator)
    : IIntegrationEventHandler<
        DataRightsTenantCorrectionAppliedIntegrationEvent>
{
    public Task HandleAsync(
        DataRightsTenantCorrectionAppliedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        coordinator.CompleteAsync(integrationEvent, cancellationToken);
}

internal sealed class DataRightsCorrectionCompletionCoordinator(
    DataRightsCaseMutationCoordinator mutations,
    IDataRightsCorrectionExecutionRepository executions,
    ISystemClock clock)
{
    private const string SystemActor = "system:data-rights-correction";

    public async Task CompleteAsync(
        DataRightsCorrectionAppliedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        await this.CompleteAsync(
            DataRightsCaseScope.ForProperty(integrationEvent.PropertyId),
            new CorrectionCompletion(
                integrationEvent.TenantId,
                integrationEvent.ExecutionId,
                integrationEvent.CaseId,
                integrationEvent.ApprovalRevision,
                integrationEvent.OwnerKey,
                integrationEvent.RecordType,
                integrationEvent.RecordId,
                integrationEvent.SelectedRecordVersion,
                integrationEvent.CurrentRecordVersion,
                integrationEvent.FieldPolicyKey,
                integrationEvent.ReceiptContractVersion,
                integrationEvent.ReceiptId,
                integrationEvent.ChangedFieldKeys.Count,
                integrationEvent.ChangedFieldsSha256,
                integrationEvent.ReceiptSha256,
                integrationEvent.OccurredAtUtc),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteAsync(
        DataRightsTenantCorrectionAppliedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (!DataRightsCaseScope.TryCreate(
                integrationEvent.CaseType,
                propertyId: null,
                out DataRightsCaseScope? scope))
        {
            throw new InvalidOperationException(
                "DataRights.CorrectionCompletionCoordinatesInvalid");
        }

        await this.CompleteAsync(
            scope!,
            new CorrectionCompletion(
                integrationEvent.TenantId,
                integrationEvent.ExecutionId,
                integrationEvent.CaseId,
                integrationEvent.ApprovalRevision,
                integrationEvent.OwnerKey,
                integrationEvent.RecordType,
                integrationEvent.RecordId,
                integrationEvent.SelectedRecordVersion,
                integrationEvent.CurrentRecordVersion,
                integrationEvent.FieldPolicyKey,
                integrationEvent.ReceiptContractVersion,
                integrationEvent.ReceiptId,
                integrationEvent.ChangedFieldKeys.Count,
                integrationEvent.ChangedFieldsSha256,
                integrationEvent.ReceiptSha256,
                integrationEvent.OccurredAtUtc),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteAsync(
        DataRightsCaseScope scope,
        CorrectionCompletion completion,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await mutations.AcquireAsync(
            scope,
            completion.CaseId,
            cancellationToken).ConfigureAwait(false);
        DataRightsCorrectionExecution? execution = await executions.GetAsync(
            completion.CaseId,
            completion.ExecutionId,
            cancellationToken).ConfigureAwait(false);
        if (execution is null ||
            dataRightsCase is null ||
            !string.Equals(
                execution.ScopeId,
                completion.TenantId,
                StringComparison.Ordinal) ||
            !execution.MatchesCompletion(
                completion.ExecutionId,
                (DataRightsCaseKind)scope.CaseType,
                scope.PropertyId,
                completion.CaseId,
                completion.ApprovalRevision,
                completion.OwnerKey,
                completion.RecordType,
                completion.RecordId,
                completion.SelectedRecordVersion,
                completion.FieldPolicyKey) ||
            dataRightsCase.ExecutionRevision != execution.ExecutionRevision ||
            completion.OccurredAtUtc >
                clock.UtcNow.Add(
                    DataRightsCase.MaximumCorrectionCompletionClockSkew))
        {
            throw new InvalidOperationException(
                "DataRights.CorrectionCompletionCoordinatesInvalid");
        }

        Result ownerCompleted = execution.Complete(
            execution.Version,
            completion.ReceiptContractVersion,
            completion.ReceiptId,
            completion.CurrentRecordVersion,
            completion.ChangedFieldCount,
            completion.ChangedFieldsSha256,
            completion.ReceiptSha256,
            completion.OccurredAtUtc);
        if (ownerCompleted.IsFailure)
        {
            throw new InvalidOperationException(ownerCompleted.Error.Code);
        }

        Result caseCompleted = dataRightsCase.CompleteCorrectionExecution(
            dataRightsCase.Version,
            completion.ApprovalRevision,
            SystemActor,
            completion.OccurredAtUtc,
            clock.UtcNow);
        if (caseCompleted.IsFailure)
        {
            throw new InvalidOperationException(caseCompleted.Error.Code);
        }
    }

    private sealed record CorrectionCompletion(
        string TenantId,
        Guid ExecutionId,
        Guid CaseId,
        long ApprovalRevision,
        string OwnerKey,
        string RecordType,
        Guid RecordId,
        long SelectedRecordVersion,
        long CurrentRecordVersion,
        string FieldPolicyKey,
        int ReceiptContractVersion,
        Guid ReceiptId,
        int ChangedFieldCount,
        string ChangedFieldsSha256,
        string ReceiptSha256,
        DateTimeOffset OccurredAtUtc);
}
