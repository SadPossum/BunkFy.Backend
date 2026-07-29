namespace BunkFy.Modules.Guests.Tests.Application;

using BunkFy.Modules.Guests.Application.Policies;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Properties.Contracts;
using Xunit;

public sealed class GuestRetentionEligibilityEvaluatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Terminal_stay_past_the_exact_policy_period_is_eligible()
    {
        GuestRetentionPolicyFixture fixture =
            GuestRetentionTestData.CreatePolicy();
        GuestRetentionCandidateSnapshot snapshot =
            GuestRetentionTestData.Snapshot(
            fixture.Binding,
            GuestStayStatus.CheckedOut,
            new DateOnly(2025, 7, 26));

        GuestRetentionEligibilityResult result =
            new GuestRetentionEligibilityEvaluator(fixture.Registry)
                .Evaluate(snapshot, Now);

        Assert.Equal(GuestRetentionEligibilityStatus.Eligible, result.Status);
        Assert.Equal(GuestRetentionEligibilityCode.None, result.Code);
        Assert.Equal(1, result.AffectedPropertyCount);
        Assert.Equal(
            new DateTimeOffset(2026, 7, 27, 0, 0, 0, TimeSpan.Zero),
            result.RetentionDeadlineUtc);
        Assert.NotNull(result.PolicySetSha256);
        Assert.Equal(64, result.PolicySetSha256.Length);
    }

    [Fact]
    public void Current_operational_stay_is_not_due()
    {
        GuestRetentionPolicyFixture fixture =
            GuestRetentionTestData.CreatePolicy();
        GuestRetentionCandidateSnapshot snapshot =
            GuestRetentionTestData.Snapshot(
            fixture.Binding,
            GuestStayStatus.Confirmed,
            new DateOnly(2024, 1, 1));

        GuestRetentionEligibilityResult result =
            new GuestRetentionEligibilityEvaluator(fixture.Registry)
                .Evaluate(snapshot, Now);

        Assert.Equal(GuestRetentionEligibilityStatus.NotDue, result.Status);
        Assert.Equal(GuestRetentionEligibilityCode.OperationalStay, result.Code);
    }

    [Fact]
    public void Due_profile_with_an_active_hold_is_blocked()
    {
        GuestRetentionPolicyFixture fixture =
            GuestRetentionTestData.CreatePolicy();
        DateTimeOffset placedAtUtc = Now.AddDays(-7);
        GuestRetentionCandidateSnapshot source =
            GuestRetentionTestData.Snapshot(
            fixture.Binding,
            GuestStayStatus.CheckedOut,
            new DateOnly(2025, 1, 1));
        GuestRetentionCandidateSnapshot snapshot = source with
        {
            ActiveHolds =
            [
                new(source.OriginPropertyId, placedAtUtc)
            ]
        };

        GuestRetentionEligibilityResult result =
            new GuestRetentionEligibilityEvaluator(fixture.Registry)
                .Evaluate(snapshot, Now);

        Assert.Equal(GuestRetentionEligibilityStatus.Blocked, result.Status);
        Assert.Equal(GuestRetentionEligibilityCode.ActiveDataHold, result.Code);
        Assert.Equal(placedAtUtc, result.HoldReviewDueAtUtc);
    }

    [Fact]
    public void Suspended_retired_property_keeps_its_last_valid_policy_authority()
    {
        GuestRetentionPolicyFixture fixture =
            GuestRetentionTestData.CreatePolicy();
        GuestRetentionCandidateSnapshot source =
            GuestRetentionTestData.Snapshot(
            fixture.Binding,
            GuestStayStatus.Cancelled,
            new DateOnly(2025, 1, 1));
        GuestRetentionPropertySnapshot property = Assert.Single(source.Properties);
        GuestRetentionCandidateSnapshot snapshot = source with
        {
            Properties =
            [
                property with
                {
                    Status = PropertyStatus.Retired,
                    ProcessingStatus = PropertyProcessingStatus.Suspended
                }
            ]
        };

        GuestRetentionEligibilityResult result =
            new GuestRetentionEligibilityEvaluator(fixture.Registry)
                .Evaluate(snapshot, Now);

        Assert.Equal(GuestRetentionEligibilityStatus.Eligible, result.Status);
    }

    [Fact]
    public void Missing_policy_projection_fails_closed()
    {
        GuestRetentionPolicyFixture fixture =
            GuestRetentionTestData.CreatePolicy();
        GuestRetentionCandidateSnapshot source =
            GuestRetentionTestData.Snapshot(
            fixture.Binding,
            GuestStayStatus.CheckedOut,
            new DateOnly(2025, 1, 1));
        GuestRetentionPropertySnapshot property = Assert.Single(source.Properties);
        GuestRetentionCandidateSnapshot snapshot = source with
        {
            Properties =
            [
                property with { GovernancePolicy = null }
            ]
        };

        GuestRetentionEligibilityResult result =
            new GuestRetentionEligibilityEvaluator(fixture.Registry)
                .Evaluate(snapshot, Now);

        Assert.Equal(GuestRetentionEligibilityStatus.Failed, result.Status);
        Assert.Equal(
            GuestRetentionEligibilityCode.PolicyUnavailable,
            result.Code);
    }

}
