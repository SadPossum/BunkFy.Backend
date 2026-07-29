namespace BunkFy.Modules.DataRights.Application.Policies;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using ContractEvidenceBinding =
    BunkFy.Modules.DataRights.Contracts.DataRightsApprovalEvidenceBinding;
using DomainEvidenceBinding =
    BunkFy.Modules.DataRights.Domain.ValueObjects.DataRightsApprovalEvidenceBinding;
using SelectedSubject =
    BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate;

internal sealed class DataRightsAnonymisationApprovalPolicy(
    IDataRightsPropertyProjectionRepository properties,
    IEnumerable<IDataRightsAnonymisationPolicyContributor> contributors,
    CountryPolicyRegistry countryPolicies,
    ISystemClock clock)
    : IDataRightsAnonymisationApprovalPolicy
{
    public const string AccommodationType = "hostel";
    public const string PurposeCode = "data-rights-anonymisation";
    public const string Surface = "erasure";
    public const string SourceProvenance = "authorized-workspace-operator";

    public async Task<Result<DataRightsApprovalPolicyEvidence>> EvaluateAsync(
        string tenantId,
        DataRightsCaseScope scope,
        Guid caseId,
        IReadOnlyCollection<SelectedSubject> subjects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(subjects);

        return scope.CaseType switch
        {
            DataRightsCaseType.GuestRights
                when scope.PropertyId is Guid propertyId =>
                await this.EvaluateGuestAsync(
                    propertyId,
                    cancellationToken).ConfigureAwait(false),
            DataRightsCaseType.StaffRights
                when scope.PropertyId is null =>
                await this.EvaluateOwnerAsync(
                    scope,
                    tenantId,
                    caseId,
                    subjects,
                    cancellationToken).ConfigureAwait(false),
            _ => Denied()
        };
    }

    private async Task<Result<DataRightsApprovalPolicyEvidence>>
        EvaluateGuestAsync(
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
            return Denied();
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
            return Denied();
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

    private async Task<Result<DataRightsApprovalPolicyEvidence>>
        EvaluateOwnerAsync(
            DataRightsCaseScope scope,
            string tenantId,
            Guid caseId,
            IReadOnlyCollection<SelectedSubject> subjects,
            CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty || subjects.Count != 1)
        {
            return Denied();
        }

        SelectedSubject subject = subjects.Single();
        IDataRightsAnonymisationPolicyContributor[] matches = contributors
            .Where(contributor =>
                contributor is not null &&
                contributor.ContractVersion ==
                    DataRightsAnonymisationPolicyContract.CurrentVersion &&
                contributor.CaseType == scope.CaseType &&
                string.Equals(
                    contributor.OwnerKey,
                    subject.OwnerKey,
                    StringComparison.Ordinal) &&
                string.Equals(
                    contributor.RecordType,
                    subject.RecordType,
                    StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            return Denied();
        }

        DataRightsAnonymisationPolicyContributionResult? contribution =
            await matches[0].EvaluateAsync(
                new DataRightsAnonymisationPolicyContributionRequest(
                    DataRightsAnonymisationPolicyContract.CurrentVersion,
                    tenantId,
                    scope.CaseType,
                    scope.PropertyId,
                    caseId,
                    new(
                        subject.OwnerKey,
                        subject.RecordType,
                        subject.RecordId,
                        subject.RecordVersion)),
                cancellationToken).ConfigureAwait(false);
        if (contribution is null ||
            contribution.ContractVersion !=
                DataRightsAnonymisationPolicyContract.CurrentVersion ||
            contribution.Status !=
                DataRightsAnonymisationPolicyContributionStatus.Approved ||
            contribution.Evidence is null ||
            contribution.Evidence.StateBindings is null ||
            contribution.Evidence.StateBindings.Count is <= 0 or
                > DataRightsAnonymisationPolicyContract.MaximumStateBindings ||
            contribution.OutcomeCode is not null)
        {
            return Denied();
        }

        List<DomainEvidenceBinding> bindings =
            new(contribution.Evidence.StateBindings.Count);
        foreach (ContractEvidenceBinding binding
            in contribution.Evidence.StateBindings)
        {
            Result<DomainEvidenceBinding> created =
                DomainEvidenceBinding.Create(
                    binding.Key,
                    binding.Version,
                    binding.Sha256);
            if (created.IsFailure)
            {
                return Denied();
            }

            bindings.Add(created.Value);
        }

        DataRightsAnonymisationPolicyContributionEvidence evidence =
            contribution.Evidence;
        Result<DataRightsApprovalPolicyEvidence> frozen =
            DataRightsApprovalPolicyEvidence.CreateScoped(
                (DataRightsCaseKind)scope.CaseType,
                DataRightsCaseScopeKind.Tenant,
                propertyId: null,
                propertyVersion: 0,
                evidence.OperatingCountryCode,
                evidence.PolicyId,
                evidence.PolicyVersion,
                evidence.RetentionPolicyId,
                evidence.RetentionPolicyVersion,
                evidence.ContentSha256,
                evidence.PurposeCode,
                evidence.Surface,
                evidence.SourceProvenance,
                evidence.RetentionDataClass,
                evidence.RetentionTrigger,
                evidence.RetentionTriggeredAtUtc,
                evidence.RetentionDeadlineUtc,
                evidence.EvaluatedAtUtc,
                bindings,
                evidence.RequiresDistinctExecutor);
        return frozen.IsSuccess ? frozen : Denied();
    }

    private static Result<DataRightsApprovalPolicyEvidence> Denied() =>
        Result.Failure<DataRightsApprovalPolicyEvidence>(
            DataRightsApplicationErrors.AnonymisationApprovalPolicyDenied);
}
