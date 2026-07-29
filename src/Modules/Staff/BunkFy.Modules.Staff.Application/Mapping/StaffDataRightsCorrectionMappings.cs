namespace BunkFy.Modules.Staff.Application.Mapping;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;

internal static class StaffDataRightsCorrectionMappings
{
    public static StaffDataRightsCorrectionReceiptDto ToDto(
        this StaffDataRightsCorrectionReceipt receipt) => new(
        receipt.Id,
        receipt.ExecutionId,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.StaffMemberId,
        receipt.SelectedRecordVersion,
        receipt.CurrentRecordVersion,
        receipt.ChangedFields.Select(ToFieldKey).ToArray(),
        receipt.CompletedAtUtc);

    public static string ToFieldKey(this StaffProfileField field) => field switch
    {
        StaffProfileField.DisplayName => StaffDataRightsFieldKeys.DisplayName,
        StaffProfileField.LegalName => StaffDataRightsFieldKeys.LegalName,
        StaffProfileField.WorkEmail => StaffDataRightsFieldKeys.WorkEmail,
        StaffProfileField.WorkPhone => StaffDataRightsFieldKeys.WorkPhone,
        StaffProfileField.EmployeeNumber => StaffDataRightsFieldKeys.EmployeeNumber,
        StaffProfileField.JobTitle => StaffDataRightsFieldKeys.JobTitle,
        StaffProfileField.Department => StaffDataRightsFieldKeys.Department,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
}
