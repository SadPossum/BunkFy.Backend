namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Logging;

internal sealed class WorkspaceIngestionTenantLifecyclePolicy(
    IWorkspaceTerminationFenceReader fences,
    IScopeContext scopeContext,
    ILogger<WorkspaceIngestionTenantLifecyclePolicy> logger)
    : IIngestionTenantLifecyclePolicy
{
    public async ValueTask<IngestionTenantLifecycleDecision> AuthorizeAsync(
        string tenantId,
        IngestionTenantLifecycleOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == IngestionTenantLifecycleOperation.Unknown ||
            !scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId) ||
            !string.Equals(
                scopeContext.ScopeId,
                tenantId?.Trim(),
                StringComparison.Ordinal))
        {
            return IngestionTenantLifecycleDecision.Restricted;
        }

        try
        {
            WorkspaceTerminationFenceSnapshot? fence =
                await fences.GetCurrentAsync(cancellationToken)
                    .ConfigureAwait(false);
            return fence is null
                ? IngestionTenantLifecycleDecision.Allowed
                : IngestionTenantLifecycleDecision.Restricted;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Workspace ingestion lifecycle admission is unavailable because {ExceptionType} was raised.",
                exception.GetType().Name);
            return IngestionTenantLifecycleDecision.Unavailable;
        }
    }
}
