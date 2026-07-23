namespace BunkFy.Modules.Reservations.Application.Policies;

using BunkFy.DataGovernance;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using Gma.Framework.Runtime.Time;

internal sealed class ReservationCountryPolicyAdmission(
    IReservationPropertyPolicyRepository properties,
    CountryPolicyRegistry countryPolicies,
    ISystemClock clock)
    : IReservationCountryPolicyAdmission
{
    public const string AccommodationType = "hostel";
    public const string ReservationManagementPurpose = "reservation-management";
    public const string ReservationIngestionPurpose = "reservation-ingestion";
    public const string AuthorizedOperatorProvenance = "authorized-workspace-operator";
    public const string ApprovedIngestionProvenance = "approved-ingestion";

    public async Task<CountryPolicyDecision> EvaluateAsync(
        Guid propertyId,
        string purposeCode,
        CountryPolicySurface surface,
        string sourceProvenance,
        CancellationToken cancellationToken)
    {
        ReservationPropertyPolicySnapshot? property = await properties.GetPolicyAsync(
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
