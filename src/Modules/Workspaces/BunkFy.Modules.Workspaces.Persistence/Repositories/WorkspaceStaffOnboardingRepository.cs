namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
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

    public Task<WorkspaceStaffOnboarding?> GetBySourceAndSubjectAsync(
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
                application.Status != WorkspaceStaffOnboardingState.Superseded)
            .OrderBy(application => application.CreatedAtUtc)
            .ThenBy(application => application.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task<WorkspaceStaffOnboardingListResponse> ListActionableAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffOnboarding[] rows = await dbContext.StaffOnboardingApplications
            .AsNoTracking()
            .Where(application =>
                (application.Status == WorkspaceStaffOnboardingState.PendingApproval &&
                    application.ClaimId != null) ||
                application.Status == WorkspaceStaffOnboardingState.Failed)
            .OrderBy(application => application.CreatedAtUtc)
            .ThenBy(application => application.Id)
            .Skip(page.SkipCount)
            .Take(page.PageSize)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new WorkspaceStaffOnboardingListResponse(
            rows.Select(application => application.ToDto()).ToArray(),
            page.Page,
            page.PageSize);
    }

    public Task AddAsync(
        WorkspaceStaffOnboarding application,
        CancellationToken cancellationToken)
    {
        dbContext.StaffOnboardingApplications.Add(application);
        return Task.CompletedTask;
    }
}
