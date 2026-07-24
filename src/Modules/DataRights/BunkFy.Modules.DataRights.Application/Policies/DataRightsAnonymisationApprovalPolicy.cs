namespace BunkFy.Modules.DataRights.Application.Policies;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class DataRightsAnonymisationApprovalPolicy(
    IDataRightsPropertyProjectionRepository properties,
    CountryPolicyRegistry countryPolicies,
    ISystemClock clock)
    : IDataRightsAnonymisationApprovalPolicy
{
    public const string AccommodationType = "hostel";
    public const string PurposeCode = "data-rights-anonymisation";
    public const string Surface = "erasure";
    public const string SourceProvenance = "authorized-workspace-operator";

    public async Task<Result<DataRightsApprovalPolicyEvidence>> EvaluateAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        DataRightsPropertyPolicySnapshot? property = await properties.GetPolicyAsync(
            propertyId,
            cancellationToken).ConfigureAwait(false);
        if (property is not
            {
                IsKnown: true,
                IsActive: true,
                ProcessingStatus: PropertyProcessingStatus.Enabled,
                GovernancePolicy: not null,
                PolicySourceVersion: > 0
            })
        {
            return Result.Failure<DataRightsApprovalPolicyEvidence>(
                DataRightsApplicationErrors.AnonymisationApprovalPolicyDenied);
        }

        PropertyGovernancePolicyBinding binding = property.GovernancePolicy;
        CountryPolicyDecision decision = countryPolicies.EvaluateOperation(
            new CountryPolicyOperationRequest(
                new CountryPolicyBinding(
                    binding.OperatingCountryCode,
                    binding.PolicyId,
                    binding.PolicyVersion,
                    binding.DataRegionId,
                    binding.TransferProfileId,
                    binding.RetentionPolicyId,
                    binding.RetentionPolicyVersion,
                    binding.ContentSha256,
                    binding.Acknowledgements.Select(acknowledgement =>
                        new CountryPolicyAcknowledgement(
                            acknowledgement.AcknowledgementId,
                            acknowledgement.AcknowledgementVersion)).ToArray()),
                AccommodationType,
                PurposeCode,
                CountryPolicySurface.Erasure,
                SourceProvenance,
                clock.UtcNow));
        if (!decision.IsAllowed || decision.Evidence is null)
        {
            return Result.Failure<DataRightsApprovalPolicyEvidence>(
                DataRightsApplicationErrors.AnonymisationApprovalPolicyDenied);
        }

        CountryPolicyEvidence evidence = decision.Evidence;
        return DataRightsApprovalPolicyEvidence.Create(
            propertyId,
            property.PolicySourceVersion,
            evidence.OperatingCountryCode,
            evidence.PolicyId,
            evidence.PolicyVersion,
            evidence.RetentionPolicyId,
            evidence.RetentionPolicyVersion,
            evidence.ContentSha256,
            evidence.PurposeCode,
            Surface,
            evidence.SourceProvenance,
            evidence.EvaluatedAtUtc);
    }
}
