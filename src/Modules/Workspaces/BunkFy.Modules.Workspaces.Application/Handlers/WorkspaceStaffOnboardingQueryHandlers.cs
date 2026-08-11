namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class GetOwnWorkspaceStaffOnboardingQueryHandler(
    WorkspaceStaffOnboardingMutationCoordinator mutations,
    IWorkspaceStaffOnboardingSerializedReadBoundary readBoundary,
    IStaffWorkspaceOnboardingIdentityAnchorOutcomeReader anchorOutcomes,
    WorkspaceOperationalAdmissionEvaluator operationalAdmission,
    IScopeContext scopeContext)
    : IQueryHandler<GetOwnWorkspaceStaffOnboardingQuery, WorkspaceStaffOnboardingDto>
{
    public async Task<Result<WorkspaceStaffOnboardingDto>> HandleAsync(
        GetOwnWorkspaceStaffOnboardingQuery query,
        CancellationToken cancellationToken)
        => await readBoundary.RunAsync(
            async readToken =>
        {
            Result admitted = WorkspaceOperationalAdmissionGuard.RequireAllowed(
                await operationalAdmission.EvaluateAsync(
                    scopeContext.ScopeId ?? string.Empty,
                    readToken).ConfigureAwait(false));
            if (admitted.IsFailure)
            {
                return Result.Failure<WorkspaceStaffOnboardingDto>(
                    admitted.Error);
            }

            WorkspaceStaffOnboardingMutationLease lease =
                await mutations.AcquireApplicantAsync(
                    query.SourceKind.ToDomain(),
                    query.SourceId,
                    query.SubjectId,
                    WorkspaceStaffOnboardingSourceLockMode.Read,
                    requireOperational: true,
                    readToken).ConfigureAwait(false);
            if (lease.CoordinateExists && lease.Application is null)
            {
                return Result.Failure<WorkspaceStaffOnboardingDto>(
                    WorkspaceStaffOnboardingApplicationErrors
                        .ProcessingRestricted);
            }

            WorkspaceStaffOnboarding? application = lease.Application;
            if (application is null)
            {
                return Result.Failure<WorkspaceStaffOnboardingDto>(
                    WorkspaceStaffOnboardingApplicationErrors
                        .ApplicationNotFound);
            }

            StaffWorkspaceOnboardingIdentityAnchorOutcome outcome =
                await anchorOutcomes.ReadAsync(
                    new StaffWorkspaceOnboardingIdentityAnchorOutcomeRequest(
                        application.Id,
                        application.SubjectId),
                    readToken).ConfigureAwait(false);
            return WorkspaceStaffOnboardingProfileMutationAuthority
                .IsExactAbsent(application, outcome)
                    ? Result.Success(application.ToDto())
                    : Result.Failure<WorkspaceStaffOnboardingDto>(
                        WorkspaceStaffOnboardingApplicationErrors
                            .ProfileMutationAuthorityUnavailable);
        }, cancellationToken).ConfigureAwait(false);
}

internal sealed class ListActionableWorkspaceStaffOnboardingQueryHandler(
    IWorkspaceStaffOnboardingRepository applications)
    : IQueryHandler<ListActionableWorkspaceStaffOnboardingQuery, WorkspaceStaffOnboardingListResponse>
{
    public async Task<Result<WorkspaceStaffOnboardingListResponse>> HandleAsync(
        ListActionableWorkspaceStaffOnboardingQuery query,
        CancellationToken cancellationToken) => Result.Success(
        await applications.ListActionableAsync(
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
}
