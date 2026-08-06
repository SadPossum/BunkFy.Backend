namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Scoping;

internal sealed class DataRightsExecutionMutationCoordinator(
    IDataRightsCaseRepository cases,
    IDataRightsOperationLock operationLock,
    IScopeContext scopeContext)
{
    public Task<DataRightsCase?> AcquireWorkItemAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        Guid workItemId,
        CancellationToken cancellationToken) => this.AcquireAsync(
        scope,
        caseId,
        workItemId,
        acquireLedger: false,
        cancellationToken);

    public Task<DataRightsCase?> AcquireLedgerWorkItemAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        Guid workItemId,
        CancellationToken cancellationToken) => this.AcquireAsync(
        scope,
        caseId,
        workItemId,
        acquireLedger: true,
        cancellationToken);

    private async Task<DataRightsCase?> AcquireAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        Guid workItemId,
        bool acquireLedger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (caseId == Guid.Empty ||
            workItemId == Guid.Empty ||
            !scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return null;
        }

        await operationLock.AcquireCaseReadAsync(caseId, cancellationToken)
            .ConfigureAwait(false);
        if (acquireLedger)
        {
            await operationLock.AcquireProcessingLedgerAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await operationLock.AcquireExecutionWorkItemAsync(
                workItemId,
                cancellationToken)
            .ConfigureAwait(false);
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            scope,
            caseId,
            cancellationToken).ConfigureAwait(false);
        return dataRightsCase is not null &&
            dataRightsCase.Id == caseId &&
            string.Equals(
                dataRightsCase.ScopeId,
                scopeContext.ScopeId,
                StringComparison.Ordinal)
                ? dataRightsCase
                : null;
    }
}
