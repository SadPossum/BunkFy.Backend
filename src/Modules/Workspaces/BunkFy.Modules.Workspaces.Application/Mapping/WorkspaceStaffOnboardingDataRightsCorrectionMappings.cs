namespace BunkFy.Modules.Workspaces.Application.Mapping;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;

internal static class WorkspaceStaffOnboardingDataRightsCorrectionMappings
{
    public static WorkspaceStaffOnboardingDataRightsCorrectionReceiptDto ToDto(
        this WorkspaceStaffOnboardingCorrectionReceipt receipt) => new(
        receipt.Id,
        receipt.ExecutionId,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.ApplicationId,
        receipt.SelectedRecordVersion,
        receipt.CurrentRecordVersion,
        receipt.ChangedFields.Select(ToFieldKey).ToArray(),
        receipt.CompletedAtUtc);

    public static string ToFieldKey(
        this WorkspaceStaffOnboardingApplicantField field) => field switch
        {
            WorkspaceStaffOnboardingApplicantField.DisplayName =>
                WorkspaceStaffOnboardingDataRightsFieldKeys.DisplayName,
            WorkspaceStaffOnboardingApplicantField.LegalName =>
                WorkspaceStaffOnboardingDataRightsFieldKeys.LegalName,
            WorkspaceStaffOnboardingApplicantField.WorkEmail =>
                WorkspaceStaffOnboardingDataRightsFieldKeys.WorkEmail,
            WorkspaceStaffOnboardingApplicantField.WorkPhone =>
                WorkspaceStaffOnboardingDataRightsFieldKeys.WorkPhone,
            WorkspaceStaffOnboardingApplicantField.EmployeeNumber =>
                WorkspaceStaffOnboardingDataRightsFieldKeys.EmployeeNumber,
            WorkspaceStaffOnboardingApplicantField.JobTitle =>
                WorkspaceStaffOnboardingDataRightsFieldKeys.JobTitle,
            WorkspaceStaffOnboardingApplicantField.Department =>
                WorkspaceStaffOnboardingDataRightsFieldKeys.Department,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
}
