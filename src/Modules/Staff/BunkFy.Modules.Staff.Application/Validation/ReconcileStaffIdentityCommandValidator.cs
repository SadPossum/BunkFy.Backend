namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class ReconcileStaffIdentityCommandValidator : ICommandValidator<ReconcileStaffIdentityCommand>
{
    public IEnumerable<string> Validate(ReconcileStaffIdentityCommand command)
    {
        foreach (string error in StaffValidation.Profile(
            command.DisplayName,
            null,
            command.WorkEmail,
            null,
            null,
            null,
            null,
            command.AuthSubjectId,
            null,
            command.ActorId))
        {
            yield return error;
        }

        foreach (string error in StaffValidation.Reason(command.Reason))
        {
            yield return error;
        }
    }
}
