namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Pagination;

public interface IWorkspaceStaffAccessProcessRepository
{
    Task<WorkspaceStaffAccessProcess?> GetAsync(Guid processId, CancellationToken cancellationToken);
    Task<WorkspaceStaffAccessProcess?> GetByStaffVersionAsync(
        Guid staffMemberId,
        long targetStaffVersion,
        CancellationToken cancellationToken);
    Task<WorkspaceStaffAccessProcess?> GetOpenByStaffAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);
    Task<WorkspaceStaffAccessProcess?> GetLatestCompletedSuspensionAsync(
        Guid staffMemberId,
        string subjectId,
        CancellationToken cancellationToken);
    Task<WorkspaceStaffAccessProcessListResponse> ListOpenAsync(
        PageRequest page,
        CancellationToken cancellationToken);
    Task AddAsync(WorkspaceStaffAccessProcess process, CancellationToken cancellationToken);
}
