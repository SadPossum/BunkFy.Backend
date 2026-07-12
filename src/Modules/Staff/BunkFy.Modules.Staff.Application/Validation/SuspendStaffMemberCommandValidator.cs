namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;

internal sealed class SuspendStaffMemberCommandValidator : ICommandValidator<SuspendStaffMemberCommand>
{
    public IEnumerable<string> Validate(SuspendStaffMemberCommand command) => StaffValidation.Lifecycle(
        command.StaffMemberId, command.Reason, command.ExpectedVersion, command.ActorId);
}
