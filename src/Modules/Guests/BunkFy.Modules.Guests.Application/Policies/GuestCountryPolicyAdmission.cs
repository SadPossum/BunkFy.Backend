namespace BunkFy.Modules.Guests.Application.Policies;

using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Runtime.Time;

internal sealed class GuestCountryPolicyAdmission(
    IGuestPropertyProjectionRepository properties,
    CountryPolicyRegistry countryPolicies,
    ISystemClock clock)
    : IGuestCountryPolicyAdmission
{
    public const string AccommodationType = "hostel";
    public const string GuestProfileManagementPurpose = "guest-profile-management";
    public const string DataRightsCorrectionPurpose = "data-rights-correction";
    public const string DataRightsRestrictionPurpose = "data-rights-restriction";
    public const string AuthorizedOperatorProvenance = "authorized-workspace-operator";

    public async Task<CountryPolicyDecision> EvaluateAsync(
        Guid propertyId,
        string purposeCode,
        CountryPolicySurface surface,
        string sourceProvenance,
        CancellationToken cancellationToken)
    {
        GuestPropertyPolicySnapshot? property = await properties.GetPolicyAsync(
            propertyId,
            cancellationToken).ConfigureAwait(false);
        if (property is not { IsKnown: true, IsActive: true, ProcessingStatus: PropertyProcessingStatus.Enabled } ||
            property.GovernancePolicy is null)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.MissingBinding);
        }

        PropertyGovernancePolicyBinding policy = property.GovernancePolicy;
        return countryPolicies.EvaluateOperation(new CountryPolicyOperationRequest(
            new CountryPolicyBinding(
                policy.OperatingCountryCode,
                policy.PolicyId,
                policy.PolicyVersion,
                policy.DataRegionId,
                policy.TransferProfileId,
                policy.RetentionPolicyId,
                policy.RetentionPolicyVersion,
                policy.ContentSha256,
                policy.Acknowledgements.Select(acknowledgement => new CountryPolicyAcknowledgement(
                    acknowledgement.AcknowledgementId,
                    acknowledgement.AcknowledgementVersion)).ToArray()),
            AccommodationType,
            purposeCode,
            surface,
            sourceProvenance,
            clock.UtcNow));
    }
}
