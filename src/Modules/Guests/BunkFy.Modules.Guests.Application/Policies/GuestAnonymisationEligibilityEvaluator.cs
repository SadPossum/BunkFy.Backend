namespace BunkFy.Modules.Guests.Application.Policies;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class GuestAnonymisationEligibilityEvaluator(
    IGuestAnonymisationEligibilityRepository eligibility,
    CountryPolicyRegistry countryPolicies,
    IScopeContext scopeContext,
    ISystemClock clock)
    : IGuestAnonymisationEligibilityEvaluator
{
    public async Task<GuestAnonymisationEligibilityResult> EvaluateAsync(
        GuestAnonymisationEligibilityRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset evaluatedAtUtc = ToPersistencePrecision(clock.UtcNow);
        if (request is null)
        {
            return Blocked(
                GuestAnonymisationBlockerCode.RequestInvalid,
                guestVersion: 0,
                affectedPropertyCount: 0,
                evaluatedAtUtc);
        }

        if (request.ContractVersion != GuestAnonymisationEligibilityContract.CurrentVersion)
        {
            return Blocked(
                GuestAnonymisationBlockerCode.ContractUnsupported,
                request.SelectedGuestVersion,
                affectedPropertyCount: 0,
                evaluatedAtUtc);
        }

        if (!IsRequestValid(request))
        {
            return Blocked(
                GuestAnonymisationBlockerCode.RequestInvalid,
                request.SelectedGuestVersion,
                affectedPropertyCount: 0,
                evaluatedAtUtc);
        }

        if (!scopeContext.IsEnabled ||
            !TenantIds.TryNormalize(request.TenantId, out string? requestedTenant) ||
            !string.Equals(requestedTenant, scopeContext.ScopeId, StringComparison.Ordinal))
        {
            return Blocked(
                GuestAnonymisationBlockerCode.TenantMismatch,
                request.SelectedGuestVersion,
                affectedPropertyCount: 0,
                evaluatedAtUtc);
        }

        GuestAnonymisationEligibilitySnapshot? snapshot = await eligibility.LoadAsync(
            request.GuestId,
            cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return Blocked(
                GuestAnonymisationBlockerCode.GuestNotFound,
                guestVersion: 0,
                affectedPropertyCount: 0,
                evaluatedAtUtc);
        }

        Guid[] propertyIds = snapshot.Stays.Select(stay => stay.PropertyId)
            .Append(snapshot.OriginPropertyId)
            .Concat(snapshot.ActiveHoldPropertyIds)
            .Distinct()
            .OrderBy(propertyId => propertyId)
            .ToArray();
        int propertyCount = propertyIds.Length;
        if (snapshot.GuestVersion != request.SelectedGuestVersion)
        {
            return Blocked(
                GuestAnonymisationBlockerCode.GuestVersionChanged,
                snapshot.GuestVersion,
                propertyCount,
                evaluatedAtUtc);
        }

        if (snapshot.GuestState != GuestProfileState.Active)
        {
            return Blocked(
                GuestAnonymisationBlockerCode.GuestNotActive,
                snapshot.GuestVersion,
                propertyCount,
                evaluatedAtUtc);
        }

        if (!propertyIds.Contains(request.RoutingPropertyId))
        {
            return Blocked(
                GuestAnonymisationBlockerCode.RoutingPropertyNotAssociated,
                snapshot.GuestVersion,
                propertyCount,
                evaluatedAtUtc);
        }

        if (propertyCount is 0 or > GuestAnonymisationEligibilityContract.MaximumAffectedProperties)
        {
            return Blocked(
                GuestAnonymisationBlockerCode.AffectedPropertySetTooLarge,
                snapshot.GuestVersion,
                propertyCount,
                evaluatedAtUtc);
        }

        if (snapshot.ActiveHoldPropertyIds.Count > 0)
        {
            return Blocked(
                GuestAnonymisationBlockerCode.ActiveDataHold,
                snapshot.GuestVersion,
                propertyCount,
                evaluatedAtUtc);
        }

        if (snapshot.Stays.Any(stay => !IsSupported(stay)))
        {
            return Blocked(
                GuestAnonymisationBlockerCode.StayProjectionUnsupported,
                snapshot.GuestVersion,
                propertyCount,
                evaluatedAtUtc);
        }

        if (snapshot.Stays.Any(stay =>
                stay.IsCurrentParticipant && IsOperational(stay.Status)))
        {
            return Blocked(
                GuestAnonymisationBlockerCode.ActiveOrFutureStay,
                snapshot.GuestVersion,
                propertyCount,
                evaluatedAtUtc);
        }

        if (snapshot.Properties.Count != propertyCount ||
            snapshot.Properties.Select(property => property.PropertyId).Distinct().Count() != propertyCount)
        {
            return Blocked(
                GuestAnonymisationBlockerCode.PropertyProjectionMissing,
                snapshot.GuestVersion,
                propertyCount,
                evaluatedAtUtc);
        }

        Dictionary<Guid, GuestAnonymisationPropertySnapshot> properties =
            snapshot.Properties.ToDictionary(property => property.PropertyId);
        List<(Guid PropertyId, long PolicySourceVersion, CountryPolicyEvidence Evidence)> policyEvidence = [];
        foreach (Guid propertyId in propertyIds)
        {
            if (!properties.TryGetValue(propertyId, out GuestAnonymisationPropertySnapshot? property) ||
                !property.IsKnown ||
                property.TopologySourceVersion < 1)
            {
                return Blocked(
                    GuestAnonymisationBlockerCode.PropertyProjectionMissing,
                    snapshot.GuestVersion,
                    propertyCount,
                    evaluatedAtUtc);
            }

            if (!property.IsActive)
            {
                return Blocked(
                    GuestAnonymisationBlockerCode.PropertyInactive,
                    snapshot.GuestVersion,
                    propertyCount,
                    evaluatedAtUtc);
            }

            if (property.ProcessingStatus != PropertyProcessingStatus.Enabled ||
                property.PolicySourceVersion < 1 ||
                property.GovernancePolicy is null)
            {
                return Blocked(
                    GuestAnonymisationBlockerCode.PropertyPolicyUnavailable,
                    snapshot.GuestVersion,
                    propertyCount,
                    evaluatedAtUtc);
            }

            CountryPolicyDecision decision = countryPolicies.EvaluateOperation(
                CreatePolicyRequest(property.GovernancePolicy, evaluatedAtUtc));
            if (!decision.IsAllowed || decision.Evidence is null)
            {
                return Blocked(
                    MapPolicyBlocker(decision.Reason),
                    snapshot.GuestVersion,
                    propertyCount,
                    evaluatedAtUtc);
            }

            if (propertyId == request.RoutingPropertyId)
            {
                GuestAnonymisationBlockerCode routeBlocker = CompareRoutingEvidence(
                    request.RoutingPolicy,
                    property.PolicySourceVersion,
                    decision.Evidence);
                if (routeBlocker != GuestAnonymisationBlockerCode.None)
                {
                    return Blocked(
                        routeBlocker,
                        snapshot.GuestVersion,
                        propertyCount,
                        evaluatedAtUtc);
                }
            }

            policyEvidence.Add((propertyId, property.PolicySourceVersion, decision.Evidence));
        }

        return new(
            GuestAnonymisationEligibilityContract.CurrentVersion,
            GuestAnonymisationEligibilityStatus.Eligible,
            GuestAnonymisationBlockerCode.None,
            snapshot.GuestVersion,
            propertyCount,
            HashLines(propertyIds.Select(propertyId => propertyId.ToString("N"))),
            HashLines(policyEvidence.Select(FormatPolicyEvidence)),
            evaluatedAtUtc);
    }

    private static bool IsRequestValid(GuestAnonymisationEligibilityRequest request) =>
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        request.RoutingPropertyId != Guid.Empty &&
        request.GuestId != Guid.Empty &&
        request.SelectedGuestVersion > 0 &&
        GuestAnonymisationPolicyEvidence.IsValid(request.RoutingPolicy);

    private static bool IsSupported(GuestAnonymisationStaySnapshot stay) =>
        stay.PropertyId != Guid.Empty &&
        stay.ReservationId != Guid.Empty &&
        stay.Role == GuestStayRole.Primary &&
        stay.Status != GuestStayStatus.Unknown &&
        Enum.IsDefined(stay.Status) &&
        stay.ReservationVersion > 0 &&
        stay.ProjectionContractVersion == GuestsModuleMetadata.StayHistoryProjectionVersion;

    private static bool IsOperational(GuestStayStatus status) => status is
        GuestStayStatus.PendingAllocation or
        GuestStayStatus.Confirmed or
        GuestStayStatus.CancellationPending or
        GuestStayStatus.CheckedIn or
        GuestStayStatus.NoShowPending or
        GuestStayStatus.CheckoutPending;

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
            GuestCountryPolicyAdmission.AccommodationType,
            GuestCountryPolicyAdmission.DataRightsAnonymisationPurpose,
            CountryPolicySurface.Erasure,
            GuestCountryPolicyAdmission.AuthorizedOperatorProvenance,
            evaluatedAtUtc);

    private static GuestAnonymisationBlockerCode MapPolicyBlocker(
        CountryPolicyDecisionReason reason) => reason switch
        {
            CountryPolicyDecisionReason.MissingBinding =>
                GuestAnonymisationBlockerCode.PropertyPolicyUnavailable,
            CountryPolicyDecisionReason.RetentionPolicyNotPermitted =>
                GuestAnonymisationBlockerCode.PropertyRetentionPolicyNotAllowed,
            CountryPolicyDecisionReason.PolicyNotEffective =>
                GuestAnonymisationBlockerCode.PropertyPolicyNotEffective,
            CountryPolicyDecisionReason.PolicyExpired =>
                GuestAnonymisationBlockerCode.PropertyPolicyExpired,
            _ => GuestAnonymisationBlockerCode.PropertyPolicyNotAllowed
        };

    private static GuestAnonymisationBlockerCode CompareRoutingEvidence(
        GuestAnonymisationRoutingPolicyEvidence expected,
        long currentPolicySourceVersion,
        CountryPolicyEvidence current)
    {
        if (expected.PropertyPolicySourceVersion != currentPolicySourceVersion)
        {
            return GuestAnonymisationBlockerCode.RoutingPolicyProjectionChanged;
        }

        if (!string.Equals(
                expected.OperatingCountryCode.Trim(),
                current.OperatingCountryCode,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(expected.PolicyId.Trim(), current.PolicyId, StringComparison.OrdinalIgnoreCase) ||
            expected.PolicyVersion != current.PolicyVersion)
        {
            return GuestAnonymisationBlockerCode.RoutingPolicyChanged;
        }

        if (!string.Equals(
                expected.RetentionPolicyId.Trim(),
                current.RetentionPolicyId,
                StringComparison.OrdinalIgnoreCase) ||
            expected.RetentionPolicyVersion != current.RetentionPolicyVersion)
        {
            return GuestAnonymisationBlockerCode.RoutingRetentionPolicyChanged;
        }

        if (!string.Equals(
                expected.ContentSha256.Trim(),
                current.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return GuestAnonymisationBlockerCode.RoutingPolicyDigestChanged;
        }

        if (!string.Equals(
                expected.PurposeCode.Trim(),
                GuestCountryPolicyAdmission.DataRightsAnonymisationPurpose,
                StringComparison.OrdinalIgnoreCase))
        {
            return GuestAnonymisationBlockerCode.RoutingPurposeChanged;
        }

        if (!string.Equals(
                expected.Surface.Trim(),
                "erasure",
                StringComparison.OrdinalIgnoreCase))
        {
            return GuestAnonymisationBlockerCode.RoutingSurfaceChanged;
        }

        if (!string.Equals(
                expected.SourceProvenance.Trim(),
                GuestCountryPolicyAdmission.AuthorizedOperatorProvenance,
                StringComparison.OrdinalIgnoreCase))
        {
            return GuestAnonymisationBlockerCode.RoutingSourceProvenanceChanged;
        }

        return expected.EvaluatedAtUtc >= current.EffectiveAtUtc &&
               expected.EvaluatedAtUtc < current.ExpiresAtUtc &&
               expected.EvaluatedAtUtc <= current.EvaluatedAtUtc
            ? GuestAnonymisationBlockerCode.None
            : GuestAnonymisationBlockerCode.RoutingPolicyEvaluationInvalid;
    }

    private static string FormatPolicyEvidence(
        (Guid PropertyId, long PolicySourceVersion, CountryPolicyEvidence Evidence) item) =>
        string.Join(
            '|',
            item.PropertyId.ToString("N"),
            item.PolicySourceVersion.ToString(CultureInfo.InvariantCulture),
            item.Evidence.OperatingCountryCode,
            item.Evidence.PolicyId,
            item.Evidence.PolicyVersion.ToString(CultureInfo.InvariantCulture),
            item.Evidence.RetentionPolicyId,
            item.Evidence.RetentionPolicyVersion.ToString(CultureInfo.InvariantCulture),
            item.Evidence.ContentSha256);

    private static string HashLines(IEnumerable<string> values)
    {
        string canonical = string.Join('\n', values);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static GuestAnonymisationEligibilityResult Blocked(
        GuestAnonymisationBlockerCode blockerCode,
        long guestVersion,
        int affectedPropertyCount,
        DateTimeOffset evaluatedAtUtc) =>
        new(
            GuestAnonymisationEligibilityContract.CurrentVersion,
            GuestAnonymisationEligibilityStatus.Blocked,
            blockerCode,
            guestVersion,
            affectedPropertyCount,
            AffectedPropertySetSha256: null,
            PolicySetSha256: null,
            evaluatedAtUtc);

    private static DateTimeOffset ToPersistencePrecision(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(value.Ticks - (value.Ticks % ticksPerMicrosecond), value.Offset);
    }
}
