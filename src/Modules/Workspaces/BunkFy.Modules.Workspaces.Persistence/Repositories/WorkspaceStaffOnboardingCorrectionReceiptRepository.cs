namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspaceStaffOnboardingCorrectionReceiptRepository(
    WorkspacesDbContext dbContext)
    : IWorkspaceStaffOnboardingCorrectionReceiptRepository
{
    public Task<WorkspaceStaffOnboardingCorrectionReceipt?>
        FindByExecutionIdAsync(
            Guid executionId,
            CancellationToken cancellationToken) =>
        dbContext.StaffOnboardingCorrectionReceipts.FirstOrDefaultAsync(
            receipt => receipt.ExecutionId == executionId,
            cancellationToken);

    public Task AddAsync(
        WorkspaceStaffOnboardingCorrectionReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.StaffOnboardingCorrectionReceipts.Add(receipt);
        return Task.CompletedTask;
    }
}
