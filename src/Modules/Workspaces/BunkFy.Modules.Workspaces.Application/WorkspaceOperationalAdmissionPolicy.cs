namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal sealed partial class WorkspaceOperationalAdmissionEvaluator(
    IWorkspaceTerminationFenceReader fences,
    IScopeContext scopeContext,
    ILogger<WorkspaceOperationalAdmissionEvaluator> logger)
{
    public async ValueTask<WorkspaceOperationalAdmissionDecision> EvaluateAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        string normalized = tenantId?.Trim() ?? string.Empty;
        if (!Guid.TryParseExact(normalized, "D", out Guid parsedTenantId) ||
            parsedTenantId == Guid.Empty ||
            !scopeContext.IsEnabled ||
            !Guid.TryParseExact(
                scopeContext.ScopeId,
                "D",
                out Guid scopedTenantId) ||
            scopedTenantId != parsedTenantId)
        {
            return WorkspaceOperationalAdmissionDecision.Unavailable;
        }

        try
        {
            WorkspaceTerminationFenceSnapshot? fence =
                await fences.GetCurrentAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (fence is null)
            {
                return WorkspaceOperationalAdmissionDecision.Allowed;
            }

            return IsValid(fence)
                ? WorkspaceOperationalAdmissionDecision.Restricted
                : WorkspaceOperationalAdmissionDecision.Unavailable;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            LogEvaluationFailure(logger, exception.GetType().Name);
            return WorkspaceOperationalAdmissionDecision.Unavailable;
        }
    }

    private static bool IsValid(WorkspaceTerminationFenceSnapshot fence) =>
        fence.ProcessId != Guid.Empty &&
        fence.TerminationEpoch != Guid.Empty &&
        fence.Version > 0 &&
        fence.State is
            WorkspaceTerminationFenceState.Frozen or
            WorkspaceTerminationFenceState.DestructionStarted or
            WorkspaceTerminationFenceState.Closed;

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Error,
        Message = "Workspace operational admission evaluation failed because {ExceptionType} was raised.")]
    private static partial void LogEvaluationFailure(
        ILogger logger,
        string exceptionType);
}

internal sealed partial class WorkspaceOperationalAdmissionPolicy(
    IWorkspaceAuthoritativeScope authoritativeScope,
    ILogger<WorkspaceOperationalAdmissionPolicy> logger)
    : IWorkspaceOperationalAdmissionPolicy
{
    public async ValueTask<WorkspaceOperationalAdmissionDecision> EvaluateAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        string normalized = tenantId?.Trim() ?? string.Empty;
        if (!Guid.TryParseExact(normalized, "D", out Guid organizationId) ||
            organizationId == Guid.Empty)
        {
            return WorkspaceOperationalAdmissionDecision.Unavailable;
        }

        string canonicalTenantId = organizationId.ToString("D");
        try
        {
            return await authoritativeScope.RunAsync(
                organizationId,
                async services => await services
                    .GetRequiredService<WorkspaceOperationalAdmissionEvaluator>()
                    .EvaluateAsync(canonicalTenantId, cancellationToken)
                    .ConfigureAwait(false)).ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            LogCoordinationFailure(logger, exception.GetType().Name);
            return WorkspaceOperationalAdmissionDecision.Unavailable;
        }
    }

    [LoggerMessage(
        EventId = 6102,
        Level = LogLevel.Error,
        Message = "Workspace operational admission coordination failed because {ExceptionType} was raised.")]
    private static partial void LogCoordinationFailure(
        ILogger logger,
        string exceptionType);
}
