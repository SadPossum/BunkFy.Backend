namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Properties.Contracts;

internal static class GuestPropertyProjectionMappings
{
    public static PropertyGovernancePolicyBinding? ToContract(
        this GuestPropertyPolicyBinding? policy) =>
        policy is null
            ? null
            : new PropertyGovernancePolicyBinding(
                policy.OperatingCountryCode,
                policy.PolicyId,
                policy.PolicyVersion,
                policy.DataRegionId,
                policy.TransferProfileId,
                policy.RetentionPolicyId,
                policy.RetentionPolicyVersion,
                policy.ContentSha256,
                policy.PolicyEffectiveAtUtc,
                policy.PolicyExpiresAtUtc,
                policy.ActivatedAtUtc,
                policy.Acknowledgements.Select(acknowledgement =>
                    new PropertyGovernanceAcknowledgement(
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion)).ToArray());
}
