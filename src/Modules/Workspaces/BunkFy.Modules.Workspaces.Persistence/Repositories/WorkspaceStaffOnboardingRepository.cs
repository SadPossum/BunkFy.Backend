namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspaceStaffOnboardingRepository(WorkspacesDbContext dbContext)
    : IWorkspaceStaffOnboardingRepository
{
    public Task<WorkspaceStaffOnboarding?> GetAsync(
        Guid applicationId,
        CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingApplications.SingleOrDefaultAsync(
            application => application.Id == applicationId,
            cancellationToken);

    public Task<WorkspaceStaffOnboarding?> GetOperationalAsync(
        Guid applicationId,
        CancellationToken cancellationToken) =>
        (
            from application in dbContext.StaffOnboardingApplications
            join projection in
                dbContext.StaffOnboardingProcessingRestrictionProjections
                on new
                {
                    application.ScopeId,
                    ApplicationId = application.Id
                }
                equals new
                {
                    projection.ScopeId,
                    projection.ApplicationId
                }
            where application.Id == applicationId &&
                projection.ContractVersion ==
                    WorkspaceStaffOnboardingProcessingRestrictionContract
                        .CurrentVersion &&
                !projection.IsRestricted
            select application
        ).SingleOrDefaultAsync(cancellationToken);

    public Task<WorkspaceStaffOnboarding?>
        GetOperationalBySourceAndSubjectAsync(
            WorkspaceStaffOnboardingSource sourceKind,
            Guid sourceId,
            string subjectId,
            CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
        return (
            from application in dbContext.StaffOnboardingApplications
                .AsNoTracking()
            join projection in
                dbContext.StaffOnboardingProcessingRestrictionProjections
                    .AsNoTracking()
                on new
                {
                    application.ScopeId,
                    ApplicationId = application.Id
                }
                equals new
                {
                    projection.ScopeId,
                    projection.ApplicationId
                }
            where application.SourceKind == sourceKind &&
                application.SourceId == sourceId &&
                application.SubjectId == normalizedSubject &&
                projection.ContractVersion ==
                    WorkspaceStaffOnboardingProcessingRestrictionContract
                        .CurrentVersion &&
                !projection.IsRestricted
            select application
        ).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<WorkspaceStaffOnboarding?>
        GetBySourceAndSubjectForLifecycleAsync(
        WorkspaceStaffOnboardingSource sourceKind,
        Guid sourceId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
        return dbContext.StaffOnboardingApplications.SingleOrDefaultAsync(
            application => application.SourceKind == sourceKind &&
                application.SourceId == sourceId &&
                application.SubjectId == normalizedSubject,
            cancellationToken);
    }

    public Task<Guid?> FindIdBySourceAndSubjectAsync(
        WorkspaceStaffOnboardingSource sourceKind,
        Guid sourceId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
        return dbContext.StaffOnboardingApplications
            .AsNoTracking()
            .Where(application =>
                application.SourceKind == sourceKind &&
                application.SourceId == sourceId &&
                application.SubjectId == normalizedSubject)
            .Select(application => (Guid?)application.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<WorkspaceStaffOnboarding?> GetByClaimAsync(
        Guid claimId,
        CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingApplications.SingleOrDefaultAsync(
            application => application.ClaimId == claimId,
            cancellationToken);

    public async Task<IReadOnlyList<WorkspaceStaffOnboarding>> ListActiveBySourceAsync(
        WorkspaceStaffOnboardingSource sourceKind,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        await dbContext.StaffOnboardingApplications
            .Where(application => application.SourceKind == sourceKind &&
                application.SourceId == sourceId &&
                application.Status != WorkspaceStaffOnboardingState.Completed &&
                application.Status != WorkspaceStaffOnboardingState.Rejected &&
                application.Status != WorkspaceStaffOnboardingState.Superseded &&
                application.Status != WorkspaceStaffOnboardingState.Expired)
            .OrderBy(application => application.CreatedAtUtc)
            .ThenBy(application => application.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task<WorkspaceStaffOnboardingListResponse> ListActionableAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffOnboarding[] fetched = await (
            from application in dbContext.StaffOnboardingApplications
                .AsNoTracking()
            join projection in
                dbContext.StaffOnboardingProcessingRestrictionProjections
                    .AsNoTracking()
                on new
                {
                    application.ScopeId,
                    ApplicationId = application.Id
                }
                equals new
                {
                    projection.ScopeId,
                    projection.ApplicationId
                }
            where
                (application.Status == WorkspaceStaffOnboardingState.PendingApproval &&
                    application.ClaimId != null) ||
                application.Status == WorkspaceStaffOnboardingState.Failed
            where projection.ContractVersion ==
                    WorkspaceStaffOnboardingProcessingRestrictionContract
                        .CurrentVersion &&
                !projection.IsRestricted
            select application
        )
            .OrderBy(application => application.CreatedAtUtc)
            .ThenBy(application => application.Id)
            .Skip(page.SkipCount)
            .Take(page.PageSize + 1)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        bool hasMore = fetched.Length > page.PageSize;
        return new WorkspaceStaffOnboardingListResponse(
            fetched.Take(page.PageSize).Select(application => application.ToDto()).ToArray(),
            page.Page,
            page.PageSize,
            hasMore);
    }

    public Task ReloadAsync(
        WorkspaceStaffOnboarding application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        return dbContext.Entry(application)
            .ReloadAsync(cancellationToken);
    }

    public Task AddAsync(
        WorkspaceStaffOnboarding application,
        CancellationToken cancellationToken)
    {
        dbContext.StaffOnboardingApplications.Add(application);
        return Task.CompletedTask;
    }
}
