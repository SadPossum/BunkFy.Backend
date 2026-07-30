namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;

public interface IWorkspaceStaffRetentionCorrelationRepository
{
    Task<WorkspaceStaffRetentionCorrelationReceipt?> GetAsync(
        Guid staffMemberId,
        long selectedStaffVersion,
        CancellationToken cancellationToken);

    Task<Result<WorkspaceStaffRetentionCorrelationReceipt>> ScrubAsync(
        WorkspaceStaffRetentionCorrelationScrubRequest request,
        CancellationToken cancellationToken);
}

public sealed record WorkspaceStaffRetentionCorrelationScrubRequest(
    Guid ReceiptId,
    Guid ExecutionId,
    string TenantId,
    Guid StaffMemberId,
    long SelectedStaffVersion,
    string? SubjectId,
    DateTimeOffset CompletedAtUtc);
