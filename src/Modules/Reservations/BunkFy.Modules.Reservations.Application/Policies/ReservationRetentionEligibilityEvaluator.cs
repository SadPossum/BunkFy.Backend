namespace BunkFy.Modules.Reservations.Application.Policies;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.DataGovernance;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Contributors;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class ReservationRetentionEligibilityEvaluator(
    CountryPolicyRegistry countryPolicies)
{
    public ReservationRetentionEligibilityResult Evaluate(
        ReservationRetentionCandidateSnapshot snapshot,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (evaluatedAtUtc == default ||
            snapshot.ReservationId == Guid.Empty ||
            snapshot.PropertyId == Guid.Empty ||
            snapshot.ReservationVersion < 1 ||
            snapshot.DetailsRevision < 1 ||
            snapshot.ProjectionOrdinal < 1)
        {
            return Failed(
                ReservationRetentionEligibilityCode
                    .ProjectionUnavailable);
        }

        if (snapshot.IsAnonymised)
        {
            return NotDue(
                ReservationRetentionEligibilityCode.AlreadyAnonymised);
        }

        if (!IsTerminal(snapshot.Status) ||
            !snapshot.TerminalAtUtc.HasValue)
        {
            return NotDue(
                ReservationRetentionEligibilityCode.NotTerminal);
        }

        if (snapshot.HasPendingAllocationAmendment)
        {
            return NotDue(
                ReservationRetentionEligibilityCode
                    .OperationPending);
        }

        if (snapshot.ProcessingRestrictionContractVersion !=
            ReservationProcessingRestrictionContract.CurrentVersion)
        {
            return Failed(
                ReservationRetentionEligibilityCode
                    .ProjectionUnavailable);
        }

        ReservationRetentionPropertySnapshot? property =
            snapshot.Property;
        if (property is null ||
            !property.IsKnown ||
            property.TopologySourceVersion < 1 ||
            property.PolicySourceVersion < 1 ||
            property.ProcessingStatus is not (
                PropertyProcessingStatus.Enabled or
                PropertyProcessingStatus.Suspended) ||
            property.GovernancePolicy is null)
        {
            return Failed(
                ReservationRetentionEligibilityCode
                    .PolicyUnavailable);
        }

        CountryPolicyRetentionDecision decision =
            countryPolicies.EvaluateRetention(
                new(
                    ToBinding(property.GovernancePolicy),
                    ReservationCountryPolicyAdmission.AccommodationType,
                    ReservationRetentionCoordinates.Purpose,
                    ReservationRetentionCoordinates.SourceProvenance,
                    ReservationRetentionCoordinates.DataClassKey,
                    ReservationRetentionCoordinates.Trigger,
                    evaluatedAtUtc));
        if (!decision.IsAllowed ||
            decision.Evidence is null ||
            decision.RetentionRule is null)
        {
            return Failed(
                ReservationRetentionEligibilityCode
                    .PolicyUnavailable);
        }

        DateTimeOffset terminalAtUtc =
            snapshot.TerminalAtUtc.Value.ToUniversalTime();
        DateTimeOffset deadline;
        try
        {
            deadline =
                terminalAtUtc.Add(decision.RetentionRule.Period);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Failed(
                ReservationRetentionEligibilityCode
                    .PolicyUnavailable);
        }

        if (evaluatedAtUtc < deadline)
        {
            return new(
                ReservationRetentionEligibilityStatus.NotDue,
                ReservationRetentionEligibilityCode.PeriodNotElapsed,
                terminalAtUtc,
                deadline,
                PolicyEvidenceSha256: null,
                HoldReviewDueAtUtc: null);
        }

        if (snapshot.ActiveHoldCount > 0)
        {
            if (!snapshot.EarliestHoldPlacedAtUtc.HasValue)
            {
                return Failed(
                    ReservationRetentionEligibilityCode
                        .ProjectionUnavailable);
            }

            return new(
                ReservationRetentionEligibilityStatus.Blocked,
                ReservationRetentionEligibilityCode.ActiveDataHold,
                terminalAtUtc,
                deadline,
                PolicyEvidenceSha256: null,
                snapshot.EarliestHoldPlacedAtUtc);
        }

        return new(
            ReservationRetentionEligibilityStatus.Eligible,
            ReservationRetentionEligibilityCode.None,
            terminalAtUtc,
            deadline,
            HashPolicy(
                snapshot,
                property,
                decision.Evidence,
                decision.RetentionRule,
                deadline),
            HoldReviewDueAtUtc: null);
    }

    private static bool IsTerminal(ReservationState state) =>
        state is ReservationState.AllocationRejected or
            ReservationState.Cancelled or
            ReservationState.NoShow or
            ReservationState.CheckedOut;

    private static CountryPolicyBinding ToBinding(
        PropertyGovernancePolicyBinding policy) =>
        new(
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
                    acknowledgement.AcknowledgementVersion)).ToArray());

    private static string HashPolicy(
        ReservationRetentionCandidateSnapshot snapshot,
        ReservationRetentionPropertySnapshot property,
        CountryPolicyEvidence evidence,
        CountryPolicyRetentionRuleEvidence rule,
        DateTimeOffset deadline)
    {
        string canonical = string.Join(
            '|',
            snapshot.PropertyId.ToString("N"),
            property.TopologySourceVersion.ToString(
                CultureInfo.InvariantCulture),
            property.PolicySourceVersion.ToString(
                CultureInfo.InvariantCulture),
            evidence.OperatingCountryCode,
            evidence.PolicyId,
            evidence.PolicyVersion.ToString(
                CultureInfo.InvariantCulture),
            evidence.ContentSha256,
            rule.RetentionPolicyId,
            rule.RetentionPolicyVersion.ToString(
                CultureInfo.InvariantCulture),
            rule.DataClass,
            rule.Trigger,
            rule.Period.Ticks.ToString(
                CultureInfo.InvariantCulture),
            snapshot.TerminalAtUtc!.Value.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            deadline.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static ReservationRetentionEligibilityResult NotDue(
        ReservationRetentionEligibilityCode code) =>
        new(
            ReservationRetentionEligibilityStatus.NotDue,
            code,
            TerminalAtUtc: null,
            RetentionDeadlineUtc: null,
            PolicyEvidenceSha256: null,
            HoldReviewDueAtUtc: null);

    private static ReservationRetentionEligibilityResult Failed(
        ReservationRetentionEligibilityCode code) =>
        new(
            ReservationRetentionEligibilityStatus.Failed,
            code,
            TerminalAtUtc: null,
            RetentionDeadlineUtc: null,
            PolicyEvidenceSha256: null,
            HoldReviewDueAtUtc: null);
}

internal sealed record ReservationRetentionEligibilityResult(
    ReservationRetentionEligibilityStatus Status,
    ReservationRetentionEligibilityCode Code,
    DateTimeOffset? TerminalAtUtc,
    DateTimeOffset? RetentionDeadlineUtc,
    string? PolicyEvidenceSha256,
    DateTimeOffset? HoldReviewDueAtUtc);

internal enum ReservationRetentionEligibilityStatus
{
    NotDue = 1,
    Eligible = 2,
    Blocked = 3,
    Failed = 4
}

internal enum ReservationRetentionEligibilityCode
{
    None = 0,
    NotTerminal = 1,
    AlreadyAnonymised = 2,
    OperationPending = 3,
    PeriodNotElapsed = 4,
    ActiveDataHold = 5,
    ProjectionUnavailable = 6,
    PolicyUnavailable = 7
}
