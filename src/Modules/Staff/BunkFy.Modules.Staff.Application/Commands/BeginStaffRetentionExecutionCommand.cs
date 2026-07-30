namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;

internal sealed record BeginStaffRetentionExecutionCommand(
    RetentionContributionRequest Request)
    : ITransactionalCommand<StaffRetentionExecutionStart>;
