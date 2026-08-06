namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Scoping;

internal sealed class TenantTerminationMutationCoordinator(
    ITenantTerminationRepository processes,
    ITenantTerminationCaseRepository cases,
    IDataRightsOperationLock operationLock,
    IScopeContext scopeContext)
{
    public async Task<TenantTerminationMutationState?> AcquireAdmissionAsync(
        Guid processId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (!this.HasValidCoordinates(processId) || caseId == Guid.Empty)
        {
            return null;
        }

        await operationLock.AcquireTenantControlAsync(cancellationToken)
            .ConfigureAwait(false);
        await operationLock.AcquireProcessWriteAsync(
                processId,
                cancellationToken)
            .ConfigureAwait(false);
        TenantTerminationProcess? process = this.Validate(
            await processes.GetProcessAsync(processId, cancellationToken)
                .ConfigureAwait(false),
            processId);
        await operationLock.AcquireCaseWriteAsync(caseId, cancellationToken)
            .ConfigureAwait(false);
        DataRightsCase? dataRightsCase = this.Validate(
            await cases.GetAsync(caseId, cancellationToken)
                .ConfigureAwait(false),
            caseId);
        return new(process, dataRightsCase);
    }

    public async Task<TenantTerminationProcess?> AcquireProcessAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        if (!this.HasValidCoordinates(processId))
        {
            return null;
        }

        await operationLock.AcquireProcessWriteAsync(
                processId,
                cancellationToken)
            .ConfigureAwait(false);
        return this.Validate(
            await processes.GetProcessAsync(processId, cancellationToken)
                .ConfigureAwait(false),
            processId);
    }

    public async Task<TenantTerminationProcess?> AcquireProcessReadAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        if (!this.HasValidCoordinates(processId))
        {
            return null;
        }

        await operationLock.AcquireProcessReadAsync(
                processId,
                cancellationToken)
            .ConfigureAwait(false);
        return this.Validate(
            await processes.GetProcessAsync(processId, cancellationToken)
                .ConfigureAwait(false),
            processId);
    }

    public async Task<TenantTerminationMutationState?> AcquireProcessAndCaseAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await this.AcquireProcessAsync(
            processId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return null;
        }

        await operationLock.AcquireCaseWriteAsync(
                process.CaseId,
                cancellationToken).ConfigureAwait(false);
        DataRightsCase? dataRightsCase = this.Validate(
            await cases.GetAsync(process.CaseId, cancellationToken)
                .ConfigureAwait(false),
            process.CaseId);
        return new(process, dataRightsCase);
    }

    public async Task<TenantTerminationProcess?> AcquireOwnerWorkAsync(
        Guid processId,
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        if (!this.HasValidCoordinates(processId) || workItemId == Guid.Empty)
        {
            return null;
        }

        await operationLock.AcquireProcessReadAsync(
                processId,
                cancellationToken)
            .ConfigureAwait(false);
        await operationLock.AcquireOwnerWorkItemAsync(
                workItemId,
                cancellationToken)
            .ConfigureAwait(false);
        return this.Validate(
            await processes.GetProcessAsync(processId, cancellationToken)
                .ConfigureAwait(false),
            processId);
    }

    private bool HasValidCoordinates(Guid id) =>
        id != Guid.Empty &&
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId);

    private T? Validate<T>(T? aggregate, Guid id)
        where T : class => aggregate switch
        {
            TenantTerminationProcess process
                when process.Id == id && this.IsCurrentScope(process.ScopeId) =>
                aggregate,
            DataRightsCase dataRightsCase
                when dataRightsCase.Id == id &&
                     this.IsCurrentScope(dataRightsCase.ScopeId) => aggregate,
            _ => null
        };

    private bool IsCurrentScope(string scopeId) => string.Equals(
        scopeId,
        scopeContext.ScopeId,
        StringComparison.Ordinal);
}

internal sealed record TenantTerminationMutationState(
    TenantTerminationProcess? Process,
    DataRightsCase? Case);
