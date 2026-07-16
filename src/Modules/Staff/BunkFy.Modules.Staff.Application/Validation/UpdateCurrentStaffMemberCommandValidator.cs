namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

internal sealed class UpdateCurrentStaffMemberCommandValidator : ICommandValidator<UpdateCurrentStaffMemberCommand>
{
    public IEnumerable<string> Validate(UpdateCurrentStaffMemberCommand command)
    {
        string subjectId = command.AuthSubjectId?.Trim() ?? string.Empty;
        if (subjectId.Length is 0 or > StaffContractLimits.AuthSubjectIdMaxLength)
        {
            yield return "AuthSubjectId is required and must be within the supported limit.";
        }

        foreach (string error in StaffValidation.Profile(
            command.DisplayName,
            command.LegalName,
            command.WorkEmail,
            command.WorkPhone,
            command.EmployeeNumber,
            command.JobTitle,
            command.Department,
            null,
            command.ExpectedVersion,
            command.ActorId))
        {
            yield return error;
        }
    }
}
