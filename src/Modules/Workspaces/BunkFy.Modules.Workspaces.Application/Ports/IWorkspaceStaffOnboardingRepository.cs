namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Pagination;

public interface IWorkspaceStaffOnboardingRepository
{
    Task<WorkspaceStaffOnboarding?> GetAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<WorkspaceStaffOnboarding?> GetBySourceAndSubjectAsync(
        WorkspaceStaffOnboardingSource sourceKind,
        Guid sourceId,
        string subjectId,
        CancellationToken cancellationToken);
    Task<WorkspaceStaffOnboarding?> GetByClaimAsync(Guid claimId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkspaceStaffOnboarding>> ListActiveBySourceAsync(
        WorkspaceStaffOnboardingSource sourceKind,
        Guid sourceId,
        CancellationToken cancellationToken);
    Task<WorkspaceStaffOnboardingListResponse> ListActionableAsync(
        PageRequest page,
        CancellationToken cancellationToken);
    Task AddAsync(WorkspaceStaffOnboarding application, CancellationToken cancellationToken);
}
