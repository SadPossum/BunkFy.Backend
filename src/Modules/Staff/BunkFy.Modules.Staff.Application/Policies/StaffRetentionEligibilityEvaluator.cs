namespace BunkFy.Modules.Staff.Application.Policies;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.DataGovernance;
using BunkFy.Modules.Staff.Application.Contributors;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class StaffRetentionEligibilityEvaluator(
    CountryPolicyRegistry countryPolicies)
{
    public StaffRetentionEligibilityResult Evaluate(
        StaffRetentionCandidateSnapshot snapshot,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (evaluatedAtUtc == default ||
            snapshot.StaffMemberId == Guid.Empty ||
            snapshot.StaffVersion < 1 ||
            snapshot.ProjectionOrdinal < 1 ||
            snapshot.ActiveHoldCount < 0 ||
            snapshot.OperationLockRevision is null or < 1)
        {
            return Failed(
                StaffRetentionEligibilityCode.ProjectionUnavailable);
        }

        if (snapshot.Status == StaffMemberState.Anonymised)
        {
            return NotDue(
                StaffRetentionEligibilityCode.AlreadyAnonymised);
        }

        if (snapshot.Status != StaffMemberState.Departed ||
            !snapshot.DepartedAtUtc.HasValue ||
            !snapshot.DepartureEffectiveOn.HasValue)
        {
            return NotDue(StaffRetentionEligibilityCode.NotDeparted);
        }

        if (snapshot.HasCurrentAssignments)
        {
            return NotDue(
                StaffRetentionEligibilityCode.CurrentAssignment);
        }

        if (snapshot.ProcessingRestrictionContractVersion !=
                StaffProcessingRestrictionContract.CurrentVersion ||
            snapshot.ProcessingRestrictionRevision is null or < 0 ||
            (snapshot.ActiveHoldCount == 0 &&
             snapshot.EarliestHoldPlacedAtUtc.HasValue) ||
            (snapshot.ActiveHoldCount > 0 &&
             !snapshot.EarliestHoldPlacedAtUtc.HasValue))
        {
            return Failed(
                StaffRetentionEligibilityCode.ProjectionUnavailable);
        }

        StaffRetentionGovernanceSnapshot? governance =
            snapshot.Governance;
        if (governance is null ||
            governance.GovernanceVersion < 1 ||
            governance.SelectedStaffVersion != snapshot.StaffVersion ||
            governance.ConfiguredAtUtc == default ||
            governance.Acknowledgements is null)
        {
            return Failed(
                StaffRetentionEligibilityCode.PolicyUnavailable);
        }

        CountryPolicyRetentionDecision decision =
            countryPolicies.EvaluateRetention(
                new(
                    ToBinding(governance),
                    StaffRetentionCoordinates.AccommodationType,
                    StaffRetentionCoordinates.Purpose,
                    StaffRetentionCoordinates.SourceProvenance,
                    StaffRetentionCoordinates.DataClassKey,
                    StaffRetentionCoordinates.Trigger,
                    evaluatedAtUtc));
        if (!decision.IsAllowed ||
            decision.Evidence is null ||
            decision.RetentionRule is null ||
            !MatchesCurrentPolicyEvidence(
                governance,
                decision.Evidence,
                evaluatedAtUtc))
        {
            return Failed(
                StaffRetentionEligibilityCode.PolicyUnavailable);
        }

        DateTimeOffset departedAtUtc =
            snapshot.DepartedAtUtc.Value.ToUniversalTime();
        DateTimeOffset deadline;
        try
        {
            deadline =
                departedAtUtc.Add(decision.RetentionRule.Period);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Failed(
                StaffRetentionEligibilityCode.PolicyUnavailable);
        }

        if (evaluatedAtUtc.ToUniversalTime() < deadline)
        {
            return new(
                StaffRetentionEligibilityStatus.NotDue,
                StaffRetentionEligibilityCode.PeriodNotElapsed,
                departedAtUtc,
                deadline,
                PolicyEvidenceSha256: null,
                HoldReviewDueAtUtc: null);
        }

        if (snapshot.ActiveHoldCount > 0)
        {
            return new(
                StaffRetentionEligibilityStatus.Blocked,
                StaffRetentionEligibilityCode.ActiveDataHold,
                departedAtUtc,
                deadline,
                PolicyEvidenceSha256: null,
                snapshot.EarliestHoldPlacedAtUtc);
        }

        return new(
            StaffRetentionEligibilityStatus.Eligible,
            StaffRetentionEligibilityCode.None,
            departedAtUtc,
            deadline,
            HashPolicy(
                snapshot,
                governance,
                decision.Evidence,
                decision.RetentionRule,
                deadline),
            HoldReviewDueAtUtc: null);
    }

    private static CountryPolicyBinding ToBinding(
        StaffRetentionGovernanceSnapshot governance) =>
        new(
            governance.OperatingCountryCode,
            governance.PolicyId,
            governance.PolicyVersion,
            governance.DataRegionId,
            governance.TransferProfileId,
            governance.RetentionPolicyId,
            governance.RetentionPolicyVersion,
            governance.ContentSha256,
            governance.Acknowledgements.Select(acknowledgement =>
                new CountryPolicyAcknowledgement(
                    acknowledgement.AcknowledgementId,
                    acknowledgement.AcknowledgementVersion)).ToArray());

    private static string HashPolicy(
        StaffRetentionCandidateSnapshot snapshot,
        StaffRetentionGovernanceSnapshot governance,
        CountryPolicyEvidence evidence,
        CountryPolicyRetentionRuleEvidence rule,
        DateTimeOffset deadline)
    {
        IEnumerable<string> acknowledgementLines =
            governance.Acknowledgements
                .OrderBy(
                    acknowledgement =>
                        acknowledgement.AcknowledgementId,
                    StringComparer.Ordinal)
                .ThenBy(
                    acknowledgement =>
                        acknowledgement.AcknowledgementVersion)
                .Select(acknowledgement =>
                    string.Join(
                        ':',
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion.ToString(
                            CultureInfo.InvariantCulture)));
        string canonical = string.Join(
            '|',
            snapshot.StaffMemberId.ToString("N"),
            snapshot.StaffVersion.ToString(
                CultureInfo.InvariantCulture),
            governance.GovernanceVersion.ToString(
                CultureInfo.InvariantCulture),
            governance.SelectedStaffVersion.ToString(
                CultureInfo.InvariantCulture),
            snapshot.ProcessingRestrictionContractVersion!.Value.ToString(
                CultureInfo.InvariantCulture),
            snapshot.ProcessingRestrictionRevision!.Value.ToString(
                CultureInfo.InvariantCulture),
            snapshot.OperationLockRevision!.Value.ToString(
                CultureInfo.InvariantCulture),
            evidence.OperatingCountryCode,
            evidence.PolicyId,
            evidence.PolicyVersion.ToString(
                CultureInfo.InvariantCulture),
            evidence.ContentSha256,
            governance.PolicyEffectiveAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            governance.PolicyExpiresAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            governance.EvaluatedAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            governance.ConfiguredAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            rule.RetentionPolicyId,
            rule.RetentionPolicyVersion.ToString(
                CultureInfo.InvariantCulture),
            rule.DataClass,
            rule.Trigger,
            rule.Period.Ticks.ToString(
                CultureInfo.InvariantCulture),
            snapshot.DepartedAtUtc!.Value.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            deadline.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            string.Join(',', acknowledgementLines));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static bool MatchesCurrentPolicyEvidence(
        StaffRetentionGovernanceSnapshot governance,
        CountryPolicyEvidence evidence,
        DateTimeOffset evaluatedAtUtc)
    {
        DateTimeOffset effectiveAtUtc =
            governance.PolicyEffectiveAtUtc.ToUniversalTime();
        DateTimeOffset expiresAtUtc =
            governance.PolicyExpiresAtUtc.ToUniversalTime();
        DateTimeOffset governanceEvaluatedAtUtc =
            governance.EvaluatedAtUtc.ToUniversalTime();
        DateTimeOffset configuredAtUtc =
            governance.ConfiguredAtUtc.ToUniversalTime();
        DateTimeOffset observedAtUtc =
            evaluatedAtUtc.ToUniversalTime();

        return effectiveAtUtc ==
                evidence.EffectiveAtUtc.ToUniversalTime() &&
            expiresAtUtc ==
                evidence.ExpiresAtUtc.ToUniversalTime() &&
            evidence.EvaluatedAtUtc.ToUniversalTime() ==
                observedAtUtc &&
            governanceEvaluatedAtUtc >= effectiveAtUtc &&
            governanceEvaluatedAtUtc < expiresAtUtc &&
            governanceEvaluatedAtUtc <= observedAtUtc &&
            configuredAtUtc >= governanceEvaluatedAtUtc &&
            configuredAtUtc < expiresAtUtc &&
            configuredAtUtc <= observedAtUtc;
    }

    private static StaffRetentionEligibilityResult NotDue(
        StaffRetentionEligibilityCode code) =>
        new(
            StaffRetentionEligibilityStatus.NotDue,
            code,
            DepartedAtUtc: null,
            RetentionDeadlineUtc: null,
            PolicyEvidenceSha256: null,
            HoldReviewDueAtUtc: null);

    private static StaffRetentionEligibilityResult Failed(
        StaffRetentionEligibilityCode code) =>
        new(
            StaffRetentionEligibilityStatus.Failed,
            code,
            DepartedAtUtc: null,
            RetentionDeadlineUtc: null,
            PolicyEvidenceSha256: null,
            HoldReviewDueAtUtc: null);
}

internal sealed record StaffRetentionEligibilityResult(
    StaffRetentionEligibilityStatus Status,
    StaffRetentionEligibilityCode Code,
    DateTimeOffset? DepartedAtUtc,
    DateTimeOffset? RetentionDeadlineUtc,
    string? PolicyEvidenceSha256,
    DateTimeOffset? HoldReviewDueAtUtc);

internal enum StaffRetentionEligibilityStatus
{
    NotDue = 1,
    Eligible = 2,
    Blocked = 3,
    Failed = 4
}

internal enum StaffRetentionEligibilityCode
{
    None = 0,
    NotDeparted = 1,
    AlreadyAnonymised = 2,
    CurrentAssignment = 3,
    PeriodNotElapsed = 4,
    ActiveDataHold = 5,
    ProjectionUnavailable = 6,
    PolicyUnavailable = 7
}
