namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Logging;

internal sealed class WorkspacePropertyProcessingLifecyclePolicy(
    IWorkspaceTerminationFenceReader fences,
    IScopeContext scopeContext,
    ILogger<WorkspacePropertyProcessingLifecyclePolicy> logger)
    : IPropertyProcessingLifecyclePolicy
{
    public async ValueTask<PropertyProcessingLifecycleDecision>
        AuthorizeActivationAsync(
            string tenantId,
            Guid propertyId,
            CancellationToken cancellationToken = default)
    {
        if (propertyId == Guid.Empty ||
            !scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId) ||
            !string.Equals(
                scopeContext.ScopeId,
                tenantId?.Trim(),
                StringComparison.Ordinal))
        {
            return PropertyProcessingLifecycleDecision.Restricted;
        }

        try
        {
            WorkspaceTerminationFenceSnapshot? fence =
                await fences.GetCurrentAsync(cancellationToken)
                    .ConfigureAwait(false);
            return fence is null
                ? PropertyProcessingLifecycleDecision.Allowed
                : PropertyProcessingLifecycleDecision.Restricted;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Workspace property lifecycle admission is unavailable because {ExceptionType} was raised.",
                exception.GetType().Name);
            return PropertyProcessingLifecycleDecision.Unavailable;
        }
    }
}
