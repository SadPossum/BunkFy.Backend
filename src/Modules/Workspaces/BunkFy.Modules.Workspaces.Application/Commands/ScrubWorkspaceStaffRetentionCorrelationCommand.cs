namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;

internal sealed record
    ScrubWorkspaceStaffRetentionCorrelationCommand(
        Guid ExecutionId,
        string TenantId,
        Guid StaffMemberId,
        long SelectedStaffVersion)
    : ITransactionalCommand<
        WorkspaceStaffRetentionCorrelationReceipt>;
