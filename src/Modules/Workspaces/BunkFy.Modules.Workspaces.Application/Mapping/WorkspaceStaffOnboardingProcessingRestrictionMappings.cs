namespace BunkFy.Modules.Workspaces.Application.Mapping;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;

internal static class
    WorkspaceStaffOnboardingProcessingRestrictionMappings
{
    public static WorkspaceStaffOnboardingProcessingRestrictionReceiptDto
        ToDto(
            this WorkspaceStaffOnboardingProcessingRestrictionReceipt receipt)
        => new(
            receipt.Id,
            receipt.RestrictionId,
            ToDto(receipt.Action),
            receipt.ApplicationId,
            receipt.CaseId,
            receipt.ApprovalRevision,
            receipt.SelectedOnboardingVersion,
            receipt.ResultingRestrictionVersion,
            receipt.ResultingProjectionRevision,
            receipt.EffectiveRestricted,
            receipt.ActorId,
            receipt.EventId,
            receipt.CompletedAtUtc);

    private static WorkspaceStaffOnboardingProcessingRestrictionActionDto
        ToDto(WorkspaceStaffOnboardingProcessingRestrictionAction action) =>
        action switch
        {
            WorkspaceStaffOnboardingProcessingRestrictionAction.Apply =>
                WorkspaceStaffOnboardingProcessingRestrictionActionDto.Apply,
            WorkspaceStaffOnboardingProcessingRestrictionAction.Release =>
                WorkspaceStaffOnboardingProcessingRestrictionActionDto.Release,
            _ =>
                WorkspaceStaffOnboardingProcessingRestrictionActionDto.Unknown
        };
}
