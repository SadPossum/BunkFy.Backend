namespace BunkFy.Modules.Reservations.Application.Policies;

using BunkFy.DataGovernance;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Naming;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ReservationAnonymisationEligibilityEvaluator(
    IReservationAnonymisationEligibilityRepository eligibility,
    CountryPolicyRegistry countryPolicies,
    IScopeContext scopeContext,
    ISystemClock clock)
    : IReservationAnonymisationEligibilityEvaluator
{
    public async Task<ReservationAnonymisationEligibilityResult> EvaluateAsync(
        ReservationAnonymisationEligibilityRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset evaluatedAtUtc = ToPersistencePrecision(clock.UtcNow);
        if (request is null)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.RequestInvalid,
                reservationVersion: 0,
                detailsRevision: 0,
                activeHoldCount: 0,
                evaluatedAtUtc);
        }

        if (request.ContractVersion !=
            ReservationAnonymisationEligibilityContract.CurrentVersion)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.ContractUnsupported,
                request.SelectedReservationVersion,
                request.SelectedDetailsRevision,
                activeHoldCount: 0,
                evaluatedAtUtc);
        }

        if (!IsRequestValid(request))
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.RequestInvalid,
                request.SelectedReservationVersion,
                request.SelectedDetailsRevision,
                activeHoldCount: 0,
                evaluatedAtUtc);
        }

        if (!scopeContext.IsEnabled ||
            !TenantIds.TryNormalize(request.TenantId, out string? tenantId) ||
            !string.Equals(
                tenantId,
                scopeContext.ScopeId,
                StringComparison.Ordinal))
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.TenantMismatch,
                request.SelectedReservationVersion,
                request.SelectedDetailsRevision,
                activeHoldCount: 0,
                evaluatedAtUtc);
        }

        ReservationAnonymisationEligibilitySnapshot? snapshot =
            await eligibility.LoadAsync(
                request.PropertyId,
                request.ReservationId,
                cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.ReservationNotFound,
                reservationVersion: 0,
                detailsRevision: 0,
                activeHoldCount: 0,
                evaluatedAtUtc);
        }

        if (snapshot.ReservationVersion != request.SelectedReservationVersion)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.ReservationVersionChanged,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (snapshot.DetailsRevision != request.SelectedDetailsRevision)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.DetailsRevisionChanged,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (snapshot.IsAnonymised)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.AlreadyRedacted,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (snapshot.ProcessingRestrictionContractVersion !=
            ReservationProcessingRestrictionContract.CurrentVersion)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode
                    .ProcessingRestrictionStateUnavailable,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (snapshot.ActiveHoldCount > 0)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.ActiveDataHold,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (snapshot.HasPendingAllocationAmendment ||
            snapshot.Status is ReservationState.PendingAllocation or
                ReservationState.CancellationPending or
                ReservationState.NoShowPending or
                ReservationState.CheckoutPending)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.AllocationOrReleasePending,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (snapshot.Status is ReservationState.Confirmed or
            ReservationState.CheckedIn)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.ActiveOrFutureStay,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (snapshot.Status is not (
            ReservationState.AllocationRejected or
            ReservationState.Cancelled or
            ReservationState.NoShow or
            ReservationState.CheckedOut))
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.ReservationStateUnsupported,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        ReservationAnonymisationPropertySnapshot? property = snapshot.Property;
        if (property is null ||
            !property.IsKnown ||
            property.TopologySourceVersion < 1)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.PropertyProjectionMissing,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (property.ProcessingStatus != PropertyProcessingStatus.Enabled ||
            property.PolicySourceVersion < 1 ||
            property.GovernancePolicy is null)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.PropertyPolicyUnavailable,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        CountryPolicyDecision decision = countryPolicies.EvaluateOperation(
            CreatePolicyRequest(property.GovernancePolicy, evaluatedAtUtc));
        if (!decision.IsAllowed || decision.Evidence is null)
        {
            return Blocked(
                MapPolicyBlocker(decision.Reason),
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        ReservationAnonymisationBlockerCode evidenceBlocker =
            CompareRoutingEvidence(
                request.RoutingPolicy,
                property.PolicySourceVersion,
                decision.Evidence);
        if (evidenceBlocker != ReservationAnonymisationBlockerCode.None)
        {
            return Blocked(
                evidenceBlocker,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        if (snapshot.Source == ReservationSource.External &&
            snapshot.HasDirectSourceReference)
        {
            return Blocked(
                ReservationAnonymisationBlockerCode.ProviderReferenceRequired,
                snapshot.ReservationVersion,
                snapshot.DetailsRevision,
                snapshot.ActiveHoldCount,
                evaluatedAtUtc);
        }

        ReservationAnonymisationRoutingPolicyEvidence currentEvidence =
            ReservationAnonymisationPolicyEvidence.FromCurrent(
                property.PolicySourceVersion,
                decision.Evidence);
        return new(
            ReservationAnonymisationEligibilityContract.CurrentVersion,
            ReservationAnonymisationEligibilityStatus.Eligible,
            ReservationAnonymisationBlockerCode.None,
            snapshot.ReservationVersion,
            snapshot.DetailsRevision,
            snapshot.ActiveHoldCount,
            ReservationAnonymisationPolicyEvidence.ComputeSha256(
                currentEvidence),
            evaluatedAtUtc);
    }

    private static bool IsRequestValid(
        ReservationAnonymisationEligibilityRequest request) =>
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        request.PropertyId != Guid.Empty &&
        request.ReservationId != Guid.Empty &&
        request.SelectedReservationVersion > 0 &&
        request.SelectedDetailsRevision > 0 &&
        ReservationAnonymisationPolicyEvidence.IsValid(request.RoutingPolicy);

    private static CountryPolicyOperationRequest CreatePolicyRequest(
        PropertyGovernancePolicyBinding policy,
        DateTimeOffset evaluatedAtUtc) =>
        new(
            new CountryPolicyBinding(
                policy.OperatingCountryCode,
                policy.PolicyId,
                policy.PolicyVersion,
                policy.DataRegionId,
                policy.TransferProfileId,
                policy.RetentionPolicyId,
                policy.RetentionPolicyVersion,
                policy.ContentSha256,
                policy.Acknowledgements.Select(acknowledgement =>
                    new CountryPolicyAcknowledgement(
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion)).ToArray()),
            ReservationCountryPolicyAdmission.AccommodationType,
            ReservationCountryPolicyAdmission.DataRightsAnonymisationPurpose,
            CountryPolicySurface.Erasure,
            ReservationCountryPolicyAdmission.AuthorizedOperatorProvenance,
            evaluatedAtUtc);

    private static ReservationAnonymisationBlockerCode MapPolicyBlocker(
        CountryPolicyDecisionReason reason) => reason switch
        {
            CountryPolicyDecisionReason.MissingBinding =>
                ReservationAnonymisationBlockerCode.PropertyPolicyUnavailable,
            CountryPolicyDecisionReason.RetentionPolicyNotPermitted =>
                ReservationAnonymisationBlockerCode
                    .PropertyRetentionPolicyNotAllowed,
            CountryPolicyDecisionReason.PolicyNotEffective =>
                ReservationAnonymisationBlockerCode.PropertyPolicyNotEffective,
            CountryPolicyDecisionReason.PolicyExpired =>
                ReservationAnonymisationBlockerCode.PropertyPolicyExpired,
            _ => ReservationAnonymisationBlockerCode.PropertyPolicyNotAllowed
        };

    private static ReservationAnonymisationBlockerCode CompareRoutingEvidence(
        ReservationAnonymisationRoutingPolicyEvidence expected,
        long currentPolicySourceVersion,
        CountryPolicyEvidence current)
    {
        if (expected.PropertyPolicySourceVersion != currentPolicySourceVersion)
        {
            return ReservationAnonymisationBlockerCode
                .RoutingPolicyProjectionChanged;
        }

        if (!string.Equals(
                expected.OperatingCountryCode.Trim(),
                current.OperatingCountryCode,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                expected.PolicyId.Trim(),
                current.PolicyId,
                StringComparison.OrdinalIgnoreCase) ||
            expected.PolicyVersion != current.PolicyVersion)
        {
            return ReservationAnonymisationBlockerCode.RoutingPolicyChanged;
        }

        if (!string.Equals(
                expected.RetentionPolicyId.Trim(),
                current.RetentionPolicyId,
                StringComparison.OrdinalIgnoreCase) ||
            expected.RetentionPolicyVersion != current.RetentionPolicyVersion)
        {
            return ReservationAnonymisationBlockerCode
                .RoutingRetentionPolicyChanged;
        }

        if (!string.Equals(
                expected.ContentSha256.Trim(),
                current.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return ReservationAnonymisationBlockerCode
                .RoutingPolicyDigestChanged;
        }

        if (!string.Equals(
                expected.PurposeCode.Trim(),
                ReservationCountryPolicyAdmission
                    .DataRightsAnonymisationPurpose,
                StringComparison.OrdinalIgnoreCase))
        {
            return ReservationAnonymisationBlockerCode.RoutingPurposeChanged;
        }

        if (!string.Equals(
                expected.Surface.Trim(),
                "erasure",
                StringComparison.OrdinalIgnoreCase))
        {
            return ReservationAnonymisationBlockerCode.RoutingSurfaceChanged;
        }

        if (!string.Equals(
                expected.SourceProvenance.Trim(),
                ReservationCountryPolicyAdmission.AuthorizedOperatorProvenance,
                StringComparison.OrdinalIgnoreCase))
        {
            return ReservationAnonymisationBlockerCode
                .RoutingSourceProvenanceChanged;
        }

        return expected.EvaluatedAtUtc >= current.EffectiveAtUtc &&
               expected.EvaluatedAtUtc < current.ExpiresAtUtc &&
               expected.EvaluatedAtUtc <= current.EvaluatedAtUtc
            ? ReservationAnonymisationBlockerCode.None
            : ReservationAnonymisationBlockerCode
                .RoutingPolicyEvaluationInvalid;
    }

    private static ReservationAnonymisationEligibilityResult Blocked(
        ReservationAnonymisationBlockerCode blockerCode,
        long reservationVersion,
        long detailsRevision,
        int activeHoldCount,
        DateTimeOffset evaluatedAtUtc) =>
        new(
            ReservationAnonymisationEligibilityContract.CurrentVersion,
            ReservationAnonymisationEligibilityStatus.Blocked,
            blockerCode,
            reservationVersion,
            detailsRevision,
            activeHoldCount,
            PolicyEvidenceSha256: null,
            evaluatedAtUtc);

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(value.Ticks - (value.Ticks % ticksPerMicrosecond), value.Offset);
    }
}
