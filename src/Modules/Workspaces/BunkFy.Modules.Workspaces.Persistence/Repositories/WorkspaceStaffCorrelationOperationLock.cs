namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspaceStaffCorrelationOperationLock(
    WorkspacesDbContext dbContext)
    : IWorkspaceStaffCorrelationOperationLock
{
    public async Task<bool> TryAcquireAsync(
        Guid anchorProcessId,
        CancellationToken cancellationToken)
    {
        if (anchorProcessId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Staff correlation operation lock requires an access-process identifier.",
                nameof(anchorProcessId));
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Staff correlation operation lock requires an active database transaction.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return await dbContext.StaffAccessProcesses
                .AsNoTracking()
                .AnyAsync(
                    process => process.Id == anchorProcessId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.AcquireOperationalMutationAdmissionAsync(
            cancellationToken).ConfigureAwait(false);
        int affected = await dbContext.StaffAccessProcesses
            .Where(process => process.Id == anchorProcessId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    process => process.Version,
                    process => process.Version),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }
}
