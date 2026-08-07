namespace BunkFy.Modules.Staff.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.ValueObjects;

internal static class StaffPropertyAssignmentChangeFingerprint
{
    public static string ComputeAssignment(
        Guid staffMemberId,
        Guid propertyId,
        long expectedVersion,
        string? propertyJobTitle,
        bool isPrimary,
        DateOnly effectiveFrom) => StaffMutationFingerprint.Compute(
        "staff-property-assignment-change-v1",
        ((int)StaffMemberMutationKind.AssignProperty).ToString(
            CultureInfo.InvariantCulture),
        staffMemberId.ToString("N"),
        propertyId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture),
        propertyJobTitle,
        isPrimary ? "1" : "0",
        effectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public static string ComputeUnassignment(
        Guid staffMemberId,
        Guid propertyId,
        long expectedVersion,
        DateOnly effectiveTo,
        StaffChangeReason reason) => StaffMutationFingerprint.Compute(
        "staff-property-assignment-change-v1",
        ((int)StaffMemberMutationKind.UnassignProperty).ToString(
            CultureInfo.InvariantCulture),
        staffMemberId.ToString("N"),
        propertyId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture),
        effectiveTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        reason.Value);
}
