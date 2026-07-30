namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;

internal sealed record
    ScrubWorkspaceStaffRetentionCorrelationCommand(
        Guid ReceiptId,
        Guid ExecutionId,
        string TenantId,
        Guid StaffMemberId,
        long SelectedStaffVersion,
        string? SubjectId,
        DateTimeOffset CompletedAtUtc)
    : ITransactionalCommand<
        WorkspaceStaffRetentionCorrelationReceipt>;
