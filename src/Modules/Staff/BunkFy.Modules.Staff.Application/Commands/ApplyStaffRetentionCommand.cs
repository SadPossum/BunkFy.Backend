namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;

internal sealed record ApplyStaffRetentionCommand(
    Guid ExecutionId,
    Guid StaffMemberId,
    long ExpectedStaffVersion)
    : ITransactionalCommand<StaffRetentionMutationResult>;
