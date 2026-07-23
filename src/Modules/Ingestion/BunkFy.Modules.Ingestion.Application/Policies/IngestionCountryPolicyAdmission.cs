namespace BunkFy.Modules.Ingestion.Application.Policies;

using BunkFy.DataGovernance;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Runtime.Time;

internal sealed class IngestionCountryPolicyAdmission(
    IIngestionPropertyProjectionRepository properties,
    CountryPolicyRegistry countryPolicies,
    ISystemClock clock)
    : IIngestionCountryPolicyAdmission
{
    public const string AccommodationType = "hostel";
    public const string ReservationIngestionPurpose = "reservation-ingestion";
    public const string AuthorizedOperatorProvenance = "authorized-workspace-operator";
    public const string ApprovedAdapterProvenance = "approved-adapter";
    public const string ApprovedParserProvenance = "approved-parser";

    public async Task<CountryPolicyDecision> EvaluateAsync(
        Guid propertyId,
        string purposeCode,
        CountryPolicySurface surface,
        string sourceProvenance,
        CancellationToken cancellationToken)
    {
        IngestionPropertyPolicySnapshot? property = await properties.GetPolicyAsync(propertyId, cancellationToken)
            .ConfigureAwait(false);
        if (property is not { IsKnown: true, IsActive: true, ProcessingStatus: PropertyProcessingStatus.Enabled } ||
            property.GovernancePolicy is null)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.MissingBinding);
        }

        PropertyGovernancePolicyBinding policy = property.GovernancePolicy;
        CountryPolicyBinding binding = new(
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
                acknowledgement.AcknowledgementVersion)).ToArray());

        return countryPolicies.EvaluateOperation(new CountryPolicyOperationRequest(
            binding,
            AccommodationType,
            purposeCode,
            surface,
            sourceProvenance,
            clock.UtcNow));
    }
}
