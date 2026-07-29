namespace BunkFy.Modules.Guests.Application.Policies;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Application.Contributors;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;

internal sealed class GuestRetentionEligibilityEvaluator(
    CountryPolicyRegistry countryPolicies)
{
    public GuestRetentionEligibilityResult Evaluate(
        GuestRetentionCandidateSnapshot snapshot,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (evaluatedAtUtc == default ||
            snapshot.GuestId == Guid.Empty ||
            snapshot.GuestVersion < 1 ||
            snapshot.ProjectionOrdinal < 1 ||
            snapshot.OriginPropertyId == Guid.Empty ||
            snapshot.GuestState is not (
                GuestProfileState.Active or GuestProfileState.Archived))
        {
            return Failed(GuestRetentionEligibilityCode.ProjectionUnavailable);
        }

        if (snapshot.Stays.Any(stay => !IsSupported(stay)))
        {
            return Failed(GuestRetentionEligibilityCode.ProjectionUnavailable);
        }

        if (snapshot.Stays.Any(stay => stay.HasOperationalStay))
        {
            return NotDue(GuestRetentionEligibilityCode.OperationalStay);
        }

        GuestRetentionStaySnapshot[] terminalStays = snapshot.Stays
            .Where(stay => stay.LatestTerminalBusinessDate.HasValue)
            .ToArray();
        if (terminalStays.Length == 0)
        {
            return NotDue(GuestRetentionEligibilityCode.NoTerminalStay);
        }

        Guid[] propertyIds = snapshot.Stays
            .Select(stay => stay.PropertyId)
            .Append(snapshot.OriginPropertyId)
            .Concat(snapshot.ActiveHolds.Select(hold => hold.PropertyId))
            .Distinct()
            .OrderBy(propertyId => propertyId)
            .ToArray();
        if (propertyIds.Length is 0 or >
            GuestAnonymisationEligibilityContract.MaximumAffectedProperties)
        {
            return Failed(GuestRetentionEligibilityCode.ProjectionUnavailable);
        }

        if (snapshot.Properties.Count != propertyIds.Length ||
            snapshot.Properties
                .Select(property => property.PropertyId)
                .Distinct()
                .Count() != propertyIds.Length)
        {
            return Failed(GuestRetentionEligibilityCode.ProjectionUnavailable);
        }

        Dictionary<Guid, GuestRetentionPropertySnapshot> properties =
            snapshot.Properties.ToDictionary(property => property.PropertyId);
        Dictionary<Guid, DateTimeOffset> propertyTriggers = [];
        foreach (GuestRetentionStaySnapshot stay in terminalStays)
        {
            if (!properties.TryGetValue(
                    stay.PropertyId,
                    out GuestRetentionPropertySnapshot? property) ||
                !TryTrigger(
                    property.TimeZoneId,
                    stay.LatestTerminalBusinessDate!.Value,
                    out DateTimeOffset trigger))
            {
                return Failed(GuestRetentionEligibilityCode.ProjectionUnavailable);
            }

            propertyTriggers[stay.PropertyId] = trigger;
        }

        DateTimeOffset globalTrigger = propertyTriggers.Values.Max();
        List<string> policyLines = [];
        DateTimeOffset latestDeadline = default;
        foreach (Guid propertyId in propertyIds)
        {
            if (!properties.TryGetValue(
                    propertyId,
                    out GuestRetentionPropertySnapshot? property) ||
                !IsPolicyProjectionUsable(property) ||
                property.GovernancePolicy is null)
            {
                return Failed(GuestRetentionEligibilityCode.PolicyUnavailable);
            }

            CountryPolicyRetentionDecision decision =
                countryPolicies.EvaluateRetention(
                    new(
                        ToBinding(property.GovernancePolicy),
                        GuestCountryPolicyAdmission.AccommodationType,
                        GuestRetentionCoordinates.Purpose,
                        GuestRetentionCoordinates.SourceProvenance,
                        GuestRetentionCoordinates.DataClassKey,
                        GuestRetentionCoordinates.Trigger,
                        evaluatedAtUtc));
            if (!decision.IsAllowed ||
                decision.Evidence is null ||
                decision.RetentionRule is null)
            {
                return Failed(GuestRetentionEligibilityCode.PolicyUnavailable);
            }

            DateTimeOffset trigger = propertyTriggers.GetValueOrDefault(
                propertyId,
                globalTrigger);
            DateTimeOffset deadline;
            try
            {
                deadline = trigger.Add(decision.RetentionRule.Period);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Failed(GuestRetentionEligibilityCode.PolicyUnavailable);
            }

            latestDeadline = deadline > latestDeadline
                ? deadline
                : latestDeadline;
            policyLines.Add(FormatPolicyLine(
                property,
                decision.Evidence,
                decision.RetentionRule,
                trigger,
                deadline));
        }

        if (latestDeadline == default)
        {
            return Failed(GuestRetentionEligibilityCode.PolicyUnavailable);
        }

        if (evaluatedAtUtc < latestDeadline)
        {
            return new(
                GuestRetentionEligibilityStatus.NotDue,
                GuestRetentionEligibilityCode.PeriodNotElapsed,
                propertyIds.Length,
                latestDeadline,
                PolicySetSha256: null,
                HoldReviewDueAtUtc: null);
        }

        if (snapshot.ActiveHolds.Count > 0)
        {
            return new(
                GuestRetentionEligibilityStatus.Blocked,
                GuestRetentionEligibilityCode.ActiveDataHold,
                propertyIds.Length,
                latestDeadline,
                PolicySetSha256: null,
                snapshot.ActiveHolds.Min(hold => hold.PlacedAtUtc));
        }

        return new(
            GuestRetentionEligibilityStatus.Eligible,
            GuestRetentionEligibilityCode.None,
            propertyIds.Length,
            latestDeadline,
            HashLines(policyLines),
            HoldReviewDueAtUtc: null);
    }

    private static bool IsSupported(GuestRetentionStaySnapshot stay) =>
        stay.PropertyId != Guid.Empty &&
        stay.ProjectionSupported;

    private static bool IsPolicyProjectionUsable(
        GuestRetentionPropertySnapshot property) =>
        property.IsKnown &&
        property.Status is PropertyStatus.Active or PropertyStatus.Retired &&
        property.ProcessingStatus is
            PropertyProcessingStatus.Enabled or
            PropertyProcessingStatus.Suspended &&
        property.TopologySourceVersion > 0 &&
        property.PolicySourceVersion > 0 &&
        !string.IsNullOrWhiteSpace(property.TimeZoneId);

    private static bool TryTrigger(
        string? timeZoneId,
        DateOnly terminalDate,
        out DateTimeOffset trigger)
    {
        trigger = default;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            TimeZoneInfo timeZone =
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            DateTime nextLocalMidnight = terminalDate
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            trigger = new(
                TimeZoneInfo.ConvertTimeToUtc(
                    nextLocalMidnight,
                    timeZone),
                TimeSpan.Zero);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

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

    private static string FormatPolicyLine(
        GuestRetentionPropertySnapshot property,
        CountryPolicyEvidence evidence,
        CountryPolicyRetentionRuleEvidence rule,
        DateTimeOffset trigger,
        DateTimeOffset deadline) =>
        string.Join(
            '|',
            property.PropertyId.ToString("N"),
            property.TopologySourceVersion.ToString(CultureInfo.InvariantCulture),
            property.PolicySourceVersion.ToString(CultureInfo.InvariantCulture),
            property.TimeZoneId,
            evidence.OperatingCountryCode,
            evidence.PolicyId,
            evidence.PolicyVersion.ToString(CultureInfo.InvariantCulture),
            evidence.ContentSha256,
            rule.RetentionPolicyId,
            rule.RetentionPolicyVersion.ToString(CultureInfo.InvariantCulture),
            rule.DataClass,
            rule.Trigger,
            rule.Period.Ticks.ToString(CultureInfo.InvariantCulture),
            trigger.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            deadline.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string HashLines(IEnumerable<string> values)
    {
        string canonical = string.Join(
            '\n',
            values.OrderBy(value => value, StringComparer.Ordinal));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static GuestRetentionEligibilityResult NotDue(
        GuestRetentionEligibilityCode code) =>
        new(
            GuestRetentionEligibilityStatus.NotDue,
            code,
            AffectedPropertyCount: 0,
            RetentionDeadlineUtc: null,
            PolicySetSha256: null,
            HoldReviewDueAtUtc: null);

    private static GuestRetentionEligibilityResult Failed(
        GuestRetentionEligibilityCode code) =>
        new(
            GuestRetentionEligibilityStatus.Failed,
            code,
            AffectedPropertyCount: 0,
            RetentionDeadlineUtc: null,
            PolicySetSha256: null,
            HoldReviewDueAtUtc: null);
}

internal sealed record GuestRetentionEligibilityResult(
    GuestRetentionEligibilityStatus Status,
    GuestRetentionEligibilityCode Code,
    int AffectedPropertyCount,
    DateTimeOffset? RetentionDeadlineUtc,
    string? PolicySetSha256,
    DateTimeOffset? HoldReviewDueAtUtc);

internal enum GuestRetentionEligibilityStatus
{
    NotDue = 1,
    Eligible = 2,
    Blocked = 3,
    Failed = 4
}

internal enum GuestRetentionEligibilityCode
{
    None = 0,
    NoTerminalStay = 1,
    OperationalStay = 2,
    PeriodNotElapsed = 3,
    ActiveDataHold = 4,
    ProjectionUnavailable = 5,
    PolicyUnavailable = 6
}
