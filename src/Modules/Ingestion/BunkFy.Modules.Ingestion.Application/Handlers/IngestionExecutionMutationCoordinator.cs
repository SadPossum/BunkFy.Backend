namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Gma.Framework.Scoping;

internal sealed class IngestionExecutionMutationCoordinator(
    IIngestionExecutionLock executionLock,
    IAdapterConnectionRepository connections,
    IIngestionRunRepository runs,
    IScopeContext scopeContext)
{
    public Task AcquireTaskExecutionAsync(
        Guid taskRunId,
        int taskAttempt,
        CancellationToken cancellationToken) => executionLock.AcquireTaskExecutionAsync(
        this.GetRequiredTenantId(),
        taskRunId,
        taskAttempt,
        cancellationToken);

    public Task<AdapterConnection?> AcquireConnectionReadAsync(
        Guid connectionId,
        CancellationToken cancellationToken) => this.AcquireConnectionAsync(
        connectionId,
        executionLock.AcquireConnectionReadAsync,
        cancellationToken);

    public Task<AdapterConnection?> AcquireConnectionWriteAsync(
        Guid connectionId,
        CancellationToken cancellationToken) => this.AcquireConnectionAsync(
        connectionId,
        executionLock.AcquireConnectionWriteAsync,
        cancellationToken);

    public Task<IngestionRun?> AcquireRunReadAsync(
        Guid runId,
        CancellationToken cancellationToken) => this.AcquireRunAsync(
        runId,
        executionLock.AcquireRunReadAsync,
        cancellationToken);

    public Task<IngestionRun?> AcquireRunWriteAsync(
        Guid runId,
        CancellationToken cancellationToken) => this.AcquireRunAsync(
        runId,
        executionLock.AcquireRunWriteAsync,
        cancellationToken);

    private async Task<AdapterConnection?> AcquireConnectionAsync(
        Guid connectionId,
        Func<string, Guid, CancellationToken, Task> acquire,
        CancellationToken cancellationToken)
    {
        string tenantId = this.GetRequiredTenantId(connectionId);
        await acquire(tenantId, connectionId, cancellationToken)
            .ConfigureAwait(false);
        AdapterConnection? connection = await connections.GetAsync(
            connectionId,
            cancellationToken).ConfigureAwait(false);
        return connection is not null &&
            string.Equals(connection.ScopeId, tenantId, StringComparison.Ordinal)
                ? connection
                : null;
    }

    private async Task<IngestionRun?> AcquireRunAsync(
        Guid runId,
        Func<string, Guid, CancellationToken, Task> acquire,
        CancellationToken cancellationToken)
    {
        string tenantId = this.GetRequiredTenantId(runId);
        await acquire(tenantId, runId, cancellationToken)
            .ConfigureAwait(false);
        IngestionRun? run = await runs.GetAsync(runId, cancellationToken)
            .ConfigureAwait(false);
        return run is not null &&
            string.Equals(run.ScopeId, tenantId, StringComparison.Ordinal)
                ? run
                : null;
    }

    private string GetRequiredTenantId(Guid? resourceId = null)
    {
        string tenantId = scopeContext.IsEnabled
            ? scopeContext.ScopeId?.Trim() ?? string.Empty
            : string.Empty;
        if (tenantId.Length == 0 ||
            (resourceId is { } id && id == Guid.Empty))
        {
            throw new InvalidOperationException(
                "An Ingestion execution lock requires valid tenant and resource coordinates.");
        }

        return tenantId;
    }
}
