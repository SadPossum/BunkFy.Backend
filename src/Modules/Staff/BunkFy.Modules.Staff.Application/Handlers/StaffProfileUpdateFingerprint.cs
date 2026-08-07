namespace BunkFy.Modules.Staff.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Staff.Domain.ValueObjects;

internal static class StaffProfileUpdateFingerprint
{
    public static string Compute(
        Guid staffMemberId,
        long expectedVersion,
        StaffProfile profile) => StaffMutationFingerprint.Compute(
        "staff-profile-update-v1",
        staffMemberId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture),
        profile.DisplayName,
        profile.LegalName,
        profile.WorkEmail,
        profile.WorkPhone,
        profile.EmployeeNumber,
        profile.JobTitle,
        profile.Department);
}
