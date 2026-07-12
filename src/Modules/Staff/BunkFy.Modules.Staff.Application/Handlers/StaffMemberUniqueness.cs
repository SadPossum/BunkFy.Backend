namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Ports;

internal static class StaffMemberUniqueness
{
    public static async Task<Result> EnsureAsync(IStaffMemberRepository members,
        string? employeeNumber, string? authSubjectId, Guid? exceptStaffMemberId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(employeeNumber) && await members.EmployeeNumberExistsAsync(
            employeeNumber.Trim(), exceptStaffMemberId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(StaffApplicationErrors.EmployeeNumberConflict);
        }

        if (!string.IsNullOrWhiteSpace(authSubjectId) && await members.AuthSubjectExistsAsync(
            authSubjectId.Trim(), exceptStaffMemberId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(StaffApplicationErrors.AuthSubjectConflict);
        }

        return Result.Success();
    }
}
