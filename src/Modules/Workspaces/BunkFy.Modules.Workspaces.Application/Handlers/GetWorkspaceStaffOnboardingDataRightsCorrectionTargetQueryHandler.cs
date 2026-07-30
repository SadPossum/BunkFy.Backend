namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Authorization;
using BunkFy.Modules.Workspaces.Application.Models;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class
    GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQueryHandler(
        IWorkspaceStaffOnboardingDataRightsCorrectionTargetReader targets,
        WorkspaceStaffOnboardingDataRightsCorrectionAuthorizer authorizer)
    : IQueryHandler<
        GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQuery,
        WorkspaceStaffOnboardingDataRightsCorrectionTargetDto>
{
    public async Task<
        Result<WorkspaceStaffOnboardingDataRightsCorrectionTargetDto>>
        HandleAsync(
            GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQuery query,
            CancellationToken cancellationToken)
    {
        Result authorized = await authorizer.AuthorizeAsync(
            query.ExecutionId,
            query.CaseId,
            query.ApprovalRevision,
            query.ApplicationId,
            query.ExpectedVersion,
            query.ActorId,
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Result.Failure<
                WorkspaceStaffOnboardingDataRightsCorrectionTargetDto>(
                authorized.Error);
        }

        WorkspaceStaffOnboardingDataRightsCorrectionTarget? target =
            await targets.GetAsync(
                query.ApplicationId,
                cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return Result.Failure<
                WorkspaceStaffOnboardingDataRightsCorrectionTargetDto>(
                WorkspaceStaffOnboardingApplicationErrors
                    .ApplicationNotFound);
        }

        if (target.Status != WorkspaceStaffOnboardingState.Submitted ||
            target.Version != query.ExpectedVersion ||
            string.IsNullOrWhiteSpace(target.DisplayName))
        {
            return Result.Failure<
                WorkspaceStaffOnboardingDataRightsCorrectionTargetDto>(
                WorkspaceStaffOnboardingApplicationErrors
                    .CorrectionTargetUnavailable);
        }

        return Result.Success(
            new WorkspaceStaffOnboardingDataRightsCorrectionTargetDto(
                target.ApplicationId,
                target.Version,
                target.DisplayName,
                target.LegalName,
                target.WorkEmail,
                target.WorkPhone,
                target.EmployeeNumber,
                target.JobTitle,
                target.Department));
    }
}
