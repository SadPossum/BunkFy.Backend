namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class GetOwnWorkspaceStaffOnboardingQueryHandler(
    IWorkspaceStaffOnboardingRepository applications)
    : IQueryHandler<GetOwnWorkspaceStaffOnboardingQuery, WorkspaceStaffOnboardingDto>
{
    public async Task<Result<WorkspaceStaffOnboardingDto>> HandleAsync(
        GetOwnWorkspaceStaffOnboardingQuery query,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffOnboarding? application = await applications.GetBySourceAndSubjectAsync(
            query.SourceKind.ToDomain(),
            query.SourceId,
            query.SubjectId,
            cancellationToken).ConfigureAwait(false);
        return application is null
            ? Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.ApplicationNotFound)
            : Result.Success(application.ToDto());
    }
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
