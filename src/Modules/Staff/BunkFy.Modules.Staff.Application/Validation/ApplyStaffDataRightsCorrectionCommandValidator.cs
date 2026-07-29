namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class ApplyStaffDataRightsCorrectionCommandValidator
    : ICommandValidator<ApplyStaffDataRightsCorrectionCommand>
{
    public IEnumerable<string> Validate(
        ApplyStaffDataRightsCorrectionCommand command)
    {
        if (command.ExecutionId == Guid.Empty)
        {
            yield return "ExecutionId is required.";
        }

        if (command.CaseId == Guid.Empty)
        {
            yield return "CaseId is required.";
        }

        if (command.ApprovalRevision < 1)
        {
            yield return "ApprovalRevision must be positive.";
        }

        if (command.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        foreach (string error in StaffValidation.Profile(
            command.DisplayName,
            command.LegalName,
            command.WorkEmail,
            command.WorkPhone,
            command.EmployeeNumber,
            command.JobTitle,
            command.Department,
            authSubjectId: null,
            command.ExpectedVersion,
            command.ActorId))
        {
            yield return error;
        }
    }
}
