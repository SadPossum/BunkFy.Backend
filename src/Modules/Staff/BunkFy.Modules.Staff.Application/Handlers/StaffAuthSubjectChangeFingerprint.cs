namespace BunkFy.Modules.Staff.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Staff.Domain.ValueObjects;

internal static class StaffAuthSubjectChangeFingerprint
{
    public static string Compute(
        Guid staffMemberId,
        long expectedVersion,
        StaffAuthSubject authSubject) => StaffMutationFingerprint.Compute(
        "staff-auth-subject-change-v1",
        staffMemberId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture),
        authSubject.Value);
}
