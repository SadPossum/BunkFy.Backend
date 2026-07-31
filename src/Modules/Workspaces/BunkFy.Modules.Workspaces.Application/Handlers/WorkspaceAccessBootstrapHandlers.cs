namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Queries;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Logging;

internal sealed class BootstrapWorkspaceAccessCommandHandler(
    WorkspaceAccessProvisioner provisioner,
    WorkspaceOperationalAdmissionEvaluator operationalAdmission,
    IScopeContext scopeContext,
    ILogger<BootstrapWorkspaceAccessCommandHandler> logger)
    : ICommandHandler<BootstrapWorkspaceAccessCommand, WorkspaceAccessBootstrapResult>
{
    public async Task<Result<WorkspaceAccessBootstrapResult>> HandleAsync(
        BootstrapWorkspaceAccessCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<WorkspaceAccessBootstrapResult>(
                WorkspaceAccessApplicationErrors.ScopeRequired);
        }

        Result admitted = WorkspaceOperationalAdmissionGuard.RequireAllowed(
            await operationalAdmission.EvaluateAsync(
                scopeContext.ScopeId,
                cancellationToken).ConfigureAwait(false));
        if (admitted.IsFailure)
        {
            return Result.Failure<WorkspaceAccessBootstrapResult>(
                admitted.Error);
        }

        try
        {
            WorkspaceAccessBootstrapResult result = await provisioner.BackfillLegacyMembersAsync(
                    scopeContext.ScopeId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Result.Success(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Workspace access bootstrap failed because {ExceptionType} was raised.",
                exception.GetType().Name);
            return Result.Failure<WorkspaceAccessBootstrapResult>(
                WorkspaceAccessApplicationErrors.BootstrapFailed);
        }
    }
}

internal sealed class GetWorkspaceAccessBootstrapStatusQueryHandler(
    WorkspaceAccessProvisioner provisioner,
    IScopeContext scopeContext)
    : IQueryHandler<GetWorkspaceAccessBootstrapStatusQuery, WorkspaceAccessBootstrapStatus>
{
    public async Task<Result<WorkspaceAccessBootstrapStatus>> HandleAsync(
        GetWorkspaceAccessBootstrapStatusQuery query,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<WorkspaceAccessBootstrapStatus>(
                WorkspaceAccessApplicationErrors.ScopeRequired);
        }

        WorkspaceAccessBootstrapStatus status = await provisioner.InspectAsync(
                scopeContext.ScopeId,
                cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(status);
    }
}
