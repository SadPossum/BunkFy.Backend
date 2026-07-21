namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Queries;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Logging;

internal sealed class BootstrapWorkspaceAccessCommandHandler(
    WorkspaceAccessProvisioner provisioner,
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
            logger.LogError(exception, "Workspace access bootstrap failed.");
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
