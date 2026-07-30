namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain.DataRights;

public interface IWorkspaceStaffOnboardingCorrectionReceiptRepository
{
    Task<WorkspaceStaffOnboardingCorrectionReceipt?> FindByExecutionIdAsync(
        Guid executionId,
        CancellationToken cancellationToken);

    Task AddAsync(
        WorkspaceStaffOnboardingCorrectionReceipt receipt,
        CancellationToken cancellationToken);
}
