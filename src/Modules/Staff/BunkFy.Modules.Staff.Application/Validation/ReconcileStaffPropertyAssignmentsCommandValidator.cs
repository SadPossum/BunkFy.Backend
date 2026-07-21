namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

internal sealed class ReconcileStaffPropertyAssignmentsCommandValidator
    : ICommandValidator<ReconcileStaffPropertyAssignmentsCommand>
{
    public IEnumerable<string> Validate(ReconcileStaffPropertyAssignmentsCommand command)
    {
        if (command.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        if (command.PropertyIds is null ||
            command.PropertyIds.Count > StaffContractLimits.PropertyAssignmentPlanMaxCount ||
            command.PropertyIds.Any(propertyId => propertyId == Guid.Empty))
        {
            yield return "PropertyIds must contain no more than the supported number of valid identifiers.";
        }

        foreach (string error in StaffValidation.Common(null, command.ActorId))
        {
            yield return error;
        }

        foreach (string error in StaffValidation.Reason(command.Reason))
        {
            yield return error;
        }
    }
}
