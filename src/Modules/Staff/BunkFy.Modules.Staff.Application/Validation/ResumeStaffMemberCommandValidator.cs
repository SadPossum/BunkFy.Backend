namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;

internal sealed class ResumeStaffMemberCommandValidator : ICommandValidator<ResumeStaffMemberCommand>
{
    public IEnumerable<string> Validate(ResumeStaffMemberCommand command) => StaffValidation.Lifecycle(
        command.StaffMemberId, command.Reason, command.ExpectedVersion, command.ActorId);
}
