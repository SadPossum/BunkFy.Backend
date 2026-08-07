namespace BunkFy.Modules.Staff.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.ValueObjects;

internal static class StaffLifecycleChangeFingerprint
{
    public static string Compute(
        StaffMemberMutationKind kind,
        Guid staffMemberId,
        long expectedVersion,
        StaffChangeReason reason,
        DateOnly? effectiveOn) => StaffMutationFingerprint.Compute(
        "staff-lifecycle-change-v1",
        ((int)kind).ToString(CultureInfo.InvariantCulture),
        staffMemberId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture),
        reason.Value,
        effectiveOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}
