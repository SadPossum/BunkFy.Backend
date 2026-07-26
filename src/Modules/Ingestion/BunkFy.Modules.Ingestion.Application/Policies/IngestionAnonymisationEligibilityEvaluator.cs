namespace BunkFy.Modules.Ingestion.Application.Policies;

using BunkFy.DataGovernance;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class IngestionAnonymisationEligibilityEvaluator(
    IIngestionAnonymisationEligibilityRepository eligibility,
    CountryPolicyRegistry countryPolicies,
    IScopeContext scopeContext,
    ISystemClock clock)
    : IIngestionAnonymisationEligibilityEvaluator
{
    public async Task<IngestionAnonymisationEligibilityResult> EvaluateAsync(
        IngestionAnonymisationEligibilityRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset evaluatedAtUtc = ToPersistencePrecision(clock.UtcNow);
        if (request is null)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.RequestInvalid,
                snapshot: null,
                evaluatedAtUtc);
        }

        if (request.ContractVersion !=
            IngestionAnonymisationEligibilityContract.CurrentVersion)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.ContractUnsupported,
                snapshot: null,
                evaluatedAtUtc,
                request.SelectedSourceLinkVersion);
        }

        if (!IsRequestValid(request))
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.RequestInvalid,
                snapshot: null,
                evaluatedAtUtc,
                request.SelectedSourceLinkVersion);
        }

        if (!scopeContext.IsEnabled ||
            !TenantIds.TryNormalize(request.TenantId, out string? tenantId) ||
            !string.Equals(
                tenantId,
                scopeContext.ScopeId,
                StringComparison.Ordinal))
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.TenantMismatch,
                snapshot: null,
                evaluatedAtUtc,
                request.SelectedSourceLinkVersion);
        }

        IngestionAnonymisationEligibilityLoadResult loaded =
            await eligibility.LoadAsync(
                request.PropertyId,
                request.SourceLinkId,
                cancellationToken).ConfigureAwait(false);
        if (loaded.Status == IngestionAnonymisationEligibilityLoadStatus.NotFound)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.SourceLinkNotFound,
                snapshot: null,
                evaluatedAtUtc);
        }

        if (loaded.Status == IngestionAnonymisationEligibilityLoadStatus.TooLarge)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.OwnerGraphTooLarge,
                snapshot: null,
                evaluatedAtUtc,
                request.SelectedSourceLinkVersion);
        }

        if (loaded is not
            {
                Status: IngestionAnonymisationEligibilityLoadStatus.Found,
                Snapshot: { } snapshot
            })
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.OwnerGraphUnavailable,
                snapshot: null,
                evaluatedAtUtc,
                request.SelectedSourceLinkVersion);
        }

        if (snapshot.GraphRecordCount >
            IngestionAnonymisationEligibilityContract.MaximumGraphRecords)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.OwnerGraphTooLarge,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.SourceLink.Version != request.SelectedSourceLinkVersion)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.SourceLinkVersionChanged,
                snapshot,
                evaluatedAtUtc);
        }

        IngestionAnonymisationPropertySnapshot? property = snapshot.Property;
        if (property is null ||
            !property.IsKnown ||
            !property.IsActive ||
            property.TopologySourceVersion < 1)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.PropertyProjectionMissing,
                snapshot,
                evaluatedAtUtc);
        }

        if (property.ProcessingStatus != PropertyProcessingStatus.Enabled ||
            property.PolicySourceVersion < 1 ||
            property.GovernancePolicy is null)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.PropertyPolicyUnavailable,
                snapshot,
                evaluatedAtUtc);
        }

        CountryPolicyDecision decision = countryPolicies.EvaluateOperation(
            CreatePolicyRequest(property.GovernancePolicy, evaluatedAtUtc));
        if (!decision.IsAllowed || decision.Evidence is null)
        {
            return Blocked(
                MapPolicyBlocker(decision.Reason),
                snapshot,
                evaluatedAtUtc);
        }

        IngestionAnonymisationBlockerCode evidenceBlocker =
            CompareRoutingEvidence(
                request.RoutingPolicy,
                property.PolicySourceVersion,
                decision.Evidence);
        if (evidenceBlocker != IngestionAnonymisationBlockerCode.None)
        {
            return Blocked(evidenceBlocker, snapshot, evaluatedAtUtc);
        }

        if (snapshot.ActiveLegalHoldCount > 0)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.ActiveLegalHold,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.Receipts.Any(receipt =>
                receipt.RawPayloadRetentionState ==
                RawPayloadRetentionState.Purging))
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.RawPayloadPurgeInProgress,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.Receipts.Any(receipt =>
                receipt.ActiveReprocessingAttemptId.HasValue &&
                receipt.ReprocessingReservationExpiresAtUtc > evaluatedAtUtc))
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.ReprocessingReservationActive,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.Attempts.Any(attempt =>
                attempt.State is ObservationReprocessingState.Queued or
                    ObservationReprocessingState.Running))
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.ReprocessingAttemptActive,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.Receipts.Any(receipt =>
                receipt.State == ObservationReceiptState.Pending))
        {
            return Blocked(
                IngestionAnonymisationBlockerCode
                    .ObservationProcessingInProgress,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.Proposals.Any(proposal =>
                proposal.State is ChangeProposalState.Pending or
                    ChangeProposalState.Applying))
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.ChangeProposalInProgress,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.SourceLink.ActiveProductOperationId.HasValue ||
            snapshot.SourceLink.DeferredReceiptId.HasValue ||
            snapshot.Dispatches.Any(dispatch =>
                dispatch.State is ReservationDispatchState.Pending or
                    ReservationDispatchState.Accepted))
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.ReservationDispatchInProgress,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.Connection.State == AdapterConnectionState.Unknown ||
            snapshot.SourceLink.State is
                ReservationSourceLinkState.Unknown or
                ReservationSourceLinkState.AwaitingCreate)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.SourceLinkStateUnsupported,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.SourceLink.State is
            ReservationSourceLinkState.Linked or
            ReservationSourceLinkState.CancellationPending)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode
                    .ProviderReconciliationRequired,
                snapshot,
                evaluatedAtUtc);
        }

        if (snapshot.SourceLink.State != ReservationSourceLinkState.Cancelled)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.SourceLinkStateUnsupported,
                snapshot,
                evaluatedAtUtc);
        }

        string operationFence =
            IngestionAnonymisationOperationFence.ComputeSha256(snapshot);
        if (operationFence.Length !=
            IngestionAnonymisationEligibilityContract.Sha256Length)
        {
            return Blocked(
                IngestionAnonymisationBlockerCode.OperationFenceUnavailable,
                snapshot,
                evaluatedAtUtc);
        }

        IngestionAnonymisationRoutingPolicyEvidence currentEvidence =
            IngestionAnonymisationPolicyEvidence.FromCurrent(
                property.PolicySourceVersion,
                decision.Evidence);
        return new(
            IngestionAnonymisationEligibilityContract.CurrentVersion,
            IngestionAnonymisationEligibilityStatus.Eligible,
            IngestionAnonymisationBlockerCode.None,
            snapshot.SourceLink.Version,
            property.RetentionFenceVersion,
            snapshot.ActiveLegalHoldCount,
            snapshot.GraphRecordCount,
            IngestionAnonymisationPolicyEvidence.ComputeSha256(
                currentEvidence),
            operationFence,
            evaluatedAtUtc);
    }

    private static bool IsRequestValid(
        IngestionAnonymisationEligibilityRequest request) =>
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        request.PropertyId != Guid.Empty &&
        request.SourceLinkId != Guid.Empty &&
        request.SelectedSourceLinkVersion > 0 &&
        IngestionAnonymisationPolicyEvidence.IsValid(request.RoutingPolicy);

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
            IngestionCountryPolicyAdmission.AccommodationType,
            IngestionCountryPolicyAdmission.DataRightsAnonymisationPurpose,
            CountryPolicySurface.Erasure,
            IngestionCountryPolicyAdmission.AuthorizedOperatorProvenance,
            evaluatedAtUtc);

    private static IngestionAnonymisationBlockerCode MapPolicyBlocker(
        CountryPolicyDecisionReason reason) => reason switch
        {
            CountryPolicyDecisionReason.MissingBinding =>
                IngestionAnonymisationBlockerCode.PropertyPolicyUnavailable,
            CountryPolicyDecisionReason.RetentionPolicyNotPermitted =>
                IngestionAnonymisationBlockerCode
                    .PropertyRetentionPolicyNotAllowed,
            CountryPolicyDecisionReason.PolicyNotEffective =>
                IngestionAnonymisationBlockerCode.PropertyPolicyNotEffective,
            CountryPolicyDecisionReason.PolicyExpired =>
                IngestionAnonymisationBlockerCode.PropertyPolicyExpired,
            _ => IngestionAnonymisationBlockerCode.PropertyPolicyNotAllowed
        };

    private static IngestionAnonymisationBlockerCode CompareRoutingEvidence(
        IngestionAnonymisationRoutingPolicyEvidence expected,
        long currentPolicySourceVersion,
        CountryPolicyEvidence current)
    {
        if (expected.PropertyPolicySourceVersion != currentPolicySourceVersion)
        {
            return IngestionAnonymisationBlockerCode
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
            return IngestionAnonymisationBlockerCode.RoutingPolicyChanged;
        }

        if (!string.Equals(
                expected.RetentionPolicyId.Trim(),
                current.RetentionPolicyId,
                StringComparison.OrdinalIgnoreCase) ||
            expected.RetentionPolicyVersion != current.RetentionPolicyVersion)
        {
            return IngestionAnonymisationBlockerCode
                .RoutingRetentionPolicyChanged;
        }

        if (!string.Equals(
                expected.ContentSha256.Trim(),
                current.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return IngestionAnonymisationBlockerCode
                .RoutingPolicyDigestChanged;
        }

        if (!string.Equals(
                expected.PurposeCode.Trim(),
                IngestionCountryPolicyAdmission.DataRightsAnonymisationPurpose,
                StringComparison.OrdinalIgnoreCase))
        {
            return IngestionAnonymisationBlockerCode.RoutingPurposeChanged;
        }

        if (!string.Equals(
                expected.Surface.Trim(),
                "erasure",
                StringComparison.OrdinalIgnoreCase))
        {
            return IngestionAnonymisationBlockerCode.RoutingSurfaceChanged;
        }

        if (!string.Equals(
                expected.SourceProvenance.Trim(),
                IngestionCountryPolicyAdmission.AuthorizedOperatorProvenance,
                StringComparison.OrdinalIgnoreCase))
        {
            return IngestionAnonymisationBlockerCode
                .RoutingSourceProvenanceChanged;
        }

        return expected.EvaluatedAtUtc >= current.EffectiveAtUtc &&
               expected.EvaluatedAtUtc < current.ExpiresAtUtc &&
               expected.EvaluatedAtUtc <= current.EvaluatedAtUtc
            ? IngestionAnonymisationBlockerCode.None
            : IngestionAnonymisationBlockerCode
                .RoutingPolicyEvaluationInvalid;
    }

    private static IngestionAnonymisationEligibilityResult Blocked(
        IngestionAnonymisationBlockerCode blockerCode,
        IngestionAnonymisationEligibilitySnapshot? snapshot,
        DateTimeOffset evaluatedAtUtc,
        long fallbackSourceLinkVersion = 0) =>
        new(
            IngestionAnonymisationEligibilityContract.CurrentVersion,
            IngestionAnonymisationEligibilityStatus.Blocked,
            blockerCode,
            snapshot?.SourceLink.Version ?? fallbackSourceLinkVersion,
            snapshot?.Property?.RetentionFenceVersion ?? 0,
            snapshot?.ActiveLegalHoldCount ?? 0,
            snapshot?.GraphRecordCount ?? 0,
            PolicyEvidenceSha256: null,
            OperationFenceSha256: null,
            evaluatedAtUtc);

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }
}
