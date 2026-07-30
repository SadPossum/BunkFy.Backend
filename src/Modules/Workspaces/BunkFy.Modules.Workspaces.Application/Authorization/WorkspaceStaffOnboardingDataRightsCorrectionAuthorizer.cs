namespace BunkFy.Modules.Workspaces.Application.Authorization;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class WorkspaceStaffOnboardingDataRightsCorrectionAuthorizer(
    IDataRightsCorrectionExecutionGate executionGate,
    IScopeContext scopeContext)
{
    public Result Validate(
        Guid executionId,
        Guid caseId,
        long approvalRevision,
        Guid applicationId,
        long expectedVersion,
        string actorId)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors.ScopeRequired);
        }

        if (executionId == Guid.Empty ||
            caseId == Guid.Empty ||
            approvalRevision < 1 ||
            applicationId == Guid.Empty ||
            expectedVersion < 1 ||
            string.IsNullOrWhiteSpace(actorId))
        {
            return Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors
                    .CorrectionRequestInvalid);
        }

        return Result.Success();
    }

    public async Task<Result> AuthorizeAsync(
        Guid executionId,
        Guid caseId,
        long approvalRevision,
        Guid applicationId,
        long expectedVersion,
        string actorId,
        CancellationToken cancellationToken)
    {
        Result valid = this.Validate(
            executionId,
            caseId,
            approvalRevision,
            applicationId,
            expectedVersion,
            actorId);
        if (valid.IsFailure)
        {
            return valid;
        }

        string? tenantId = scopeContext.ScopeId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors.ScopeRequired);
        }

        DataRightsCorrectionExecutionGateResult execution =
            await executionGate.EvaluateAsync(
                new DataRightsCorrectionExecutionGateRequest(
                    tenantId,
                    DataRightsCaseType.StaffRights,
                    PropertyId: null,
                    caseId,
                    approvalRevision,
                    executionId,
                    new DataRightsSubjectCoordinate(
                        WorkspacesDataRightsCoordinates.Owner,
                        WorkspacesDataRightsCoordinates
                            .StaffOnboardingRecordType,
                        applicationId,
                        expectedVersion),
                    WorkspacesDataRightsCoordinates
                        .StaffOnboardingCorrectionFieldPolicyKey,
                    actorId),
                cancellationToken).ConfigureAwait(false);
        return execution.IsAllowed
            ? Result.Success()
            : Result.Failure(
                WorkspaceStaffOnboardingApplicationErrors
                    .DataRightsApprovalRequired);
    }
}
