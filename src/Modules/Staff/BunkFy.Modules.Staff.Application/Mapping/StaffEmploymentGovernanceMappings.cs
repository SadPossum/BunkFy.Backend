namespace BunkFy.Modules.Staff.Application.Mapping;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Governance;

internal static class StaffEmploymentGovernanceMappings
{
    public static StaffEmploymentGovernanceDto ToDto(
        this StaffEmploymentGovernance governance) =>
        new(
            governance.GovernanceContractVersion,
            governance.StaffMemberId,
            governance.SelectedStaffVersion,
            governance.Binding.OperatingCountryCode,
            governance.Binding.PolicyId,
            governance.Binding.PolicyVersion,
            governance.Binding.DataRegionId,
            governance.Binding.TransferProfileId,
            governance.Binding.RetentionPolicyId,
            governance.Binding.RetentionPolicyVersion,
            governance.Binding.ContentSha256,
            governance.Binding.PolicyEffectiveAtUtc,
            governance.Binding.PolicyExpiresAtUtc,
            governance.Binding.EvaluatedAtUtc,
            governance.AcceptedAcknowledgements
                .OrderBy(
                    item => item.AcknowledgementId,
                    StringComparer.Ordinal)
                .ThenBy(item => item.AcknowledgementVersion)
                .Select(item =>
                    new StaffEmploymentGovernanceAcknowledgementDto(
                        item.AcknowledgementId,
                        item.AcknowledgementVersion))
                .ToArray(),
            governance.ConfiguredBy,
            governance.ConfiguredAtUtc,
            governance.Version);

    public static StaffEmploymentGovernanceChangeReceiptDto ToDto(
        this StaffEmploymentGovernanceChangeReceipt receipt) =>
        new(
            receipt.Id,
            receipt.IdempotencyKey,
            receipt.StaffMemberId,
            receipt.GovernanceContractVersion,
            receipt.SelectedStaffVersion,
            receipt.PreviousGovernanceVersion,
            receipt.ResultingGovernanceVersion,
            receipt.PolicyContentSha256,
            receipt.AcknowledgementsSha256,
            receipt.ReceiptSha256,
            receipt.ActorId,
            receipt.CompletedAtUtc);
}
