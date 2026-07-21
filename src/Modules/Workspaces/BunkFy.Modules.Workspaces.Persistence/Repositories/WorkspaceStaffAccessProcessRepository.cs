namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspaceStaffAccessProcessRepository(WorkspacesDbContext dbContext)
    : IWorkspaceStaffAccessProcessRepository
{
    public Task<WorkspaceStaffAccessProcess?> GetAsync(
        Guid processId,
        CancellationToken cancellationToken) =>
        dbContext.StaffAccessProcesses.SingleOrDefaultAsync(
            process => process.Id == processId,
            cancellationToken);

    public Task<WorkspaceStaffAccessProcess?> GetByStaffVersionAsync(
        Guid staffMemberId,
        long targetStaffVersion,
        CancellationToken cancellationToken) =>
        dbContext.StaffAccessProcesses.SingleOrDefaultAsync(
            process => process.StaffMemberId == staffMemberId &&
                process.TargetStaffVersion == targetStaffVersion,
            cancellationToken);

    public Task<WorkspaceStaffAccessProcess?> GetOpenByStaffAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        dbContext.StaffAccessProcesses.SingleOrDefaultAsync(
            process => process.StaffMemberId == staffMemberId &&
                process.State != WorkspaceStaffAccessProcessState.Completed,
            cancellationToken);

    public Task<WorkspaceStaffAccessProcess?> GetLatestCompletedSuspensionAsync(
        Guid staffMemberId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        string normalizedSubject = subjectId.Trim();
        return dbContext.StaffAccessProcesses
            .Where(process => process.StaffMemberId == staffMemberId &&
                process.SubjectId == normalizedSubject &&
                process.TargetState == WorkspaceStaffAccessTargetState.Suspended &&
                process.State == WorkspaceStaffAccessProcessState.Completed)
            .OrderByDescending(process => process.TargetStaffVersion)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkspaceStaffAccessProcessListResponse> ListOpenAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessProcess[] rows = await dbContext.StaffAccessProcesses
            .AsNoTracking()
            .Where(process => process.State != WorkspaceStaffAccessProcessState.Completed)
            .OrderBy(process => process.CreatedAtUtc)
            .ThenBy(process => process.Id)
            .Skip(page.SkipCount)
            .Take(page.PageSize)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new WorkspaceStaffAccessProcessListResponse(
            rows.Select(process => process.ToDto()).ToArray(),
            page.Page,
            page.PageSize);
    }

    public Task AddAsync(
        WorkspaceStaffAccessProcess process,
        CancellationToken cancellationToken)
    {
        dbContext.StaffAccessProcesses.Add(process);
        return Task.CompletedTask;
    }
}
