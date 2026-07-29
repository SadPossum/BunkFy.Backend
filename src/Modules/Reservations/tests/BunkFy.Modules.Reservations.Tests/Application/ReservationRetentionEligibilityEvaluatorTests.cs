namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationRetentionEligibilityEvaluatorTests
{
    private static readonly DateTimeOffset Now =
        ReservationRetentionTestData.Now;

    [Fact]
    public void Terminal_reservation_past_the_exact_period_is_eligible()
    {
        ReservationRetentionPolicyFixture fixture =
            ReservationRetentionTestData.CreatePolicy();
        DateTimeOffset terminalAtUtc = Now.AddDays(-366);

        ReservationRetentionEligibilityResult result =
            new ReservationRetentionEligibilityEvaluator(fixture.Registry)
                .Evaluate(
                    ReservationRetentionTestData.Snapshot(
                        fixture.Binding,
                        terminalAtUtc: terminalAtUtc),
                    Now);

        Assert.Equal(
            ReservationRetentionEligibilityStatus.Eligible,
            result.Status);
        Assert.Equal(
            terminalAtUtc.AddDays(365),
            result.RetentionDeadlineUtc);
        Assert.Equal(64, result.PolicyEvidenceSha256?.Length);
    }

    [Fact]
    public void Current_reservation_and_unelapsed_period_are_not_due()
    {
        ReservationRetentionPolicyFixture fixture =
            ReservationRetentionTestData.CreatePolicy();
        ReservationRetentionEligibilityEvaluator evaluator =
            new(fixture.Registry);

        ReservationRetentionEligibilityResult current =
            evaluator.Evaluate(
                ReservationRetentionTestData.Snapshot(
                    fixture.Binding,
                    ReservationState.Confirmed),
                Now);
        ReservationRetentionEligibilityResult recent =
            evaluator.Evaluate(
                ReservationRetentionTestData.Snapshot(
                    fixture.Binding,
                    terminalAtUtc: Now.AddDays(-30)),
                Now);

        Assert.Equal(
            ReservationRetentionEligibilityCode.NotTerminal,
            current.Code);
        Assert.Equal(
            ReservationRetentionEligibilityCode.PeriodNotElapsed,
            recent.Code);
    }

    [Fact]
    public void Due_reservation_with_an_active_hold_is_blocked()
    {
        ReservationRetentionPolicyFixture fixture =
            ReservationRetentionTestData.CreatePolicy();
        DateTimeOffset placedAtUtc = Now.AddDays(-7);
        ReservationRetentionCandidateSnapshot snapshot =
            ReservationRetentionTestData.Snapshot(fixture.Binding) with
            {
                ActiveHoldCount = 1,
                EarliestHoldPlacedAtUtc = placedAtUtc
            };

        ReservationRetentionEligibilityResult result =
            new ReservationRetentionEligibilityEvaluator(fixture.Registry)
                .Evaluate(snapshot, Now);

        Assert.Equal(
            ReservationRetentionEligibilityStatus.Blocked,
            result.Status);
        Assert.Equal(
            ReservationRetentionEligibilityCode.ActiveDataHold,
            result.Code);
        Assert.Equal(placedAtUtc, result.HoldReviewDueAtUtc);
    }

    [Fact]
    public void Suspended_retired_property_keeps_last_valid_policy_authority()
    {
        ReservationRetentionPolicyFixture fixture =
            ReservationRetentionTestData.CreatePolicy();
        ReservationRetentionCandidateSnapshot source =
            ReservationRetentionTestData.Snapshot(fixture.Binding);
        ReservationRetentionCandidateSnapshot snapshot = source with
        {
            Property = source.Property! with
            {
                IsActive = false,
                ProcessingStatus =
                    PropertyProcessingStatus.Suspended
            }
        };

        ReservationRetentionEligibilityResult result =
            new ReservationRetentionEligibilityEvaluator(fixture.Registry)
                .Evaluate(snapshot, Now);

        Assert.Equal(
            ReservationRetentionEligibilityStatus.Eligible,
            result.Status);
    }

    [Fact]
    public void Stale_processing_projection_and_missing_policy_fail_closed()
    {
        ReservationRetentionPolicyFixture fixture =
            ReservationRetentionTestData.CreatePolicy();
        ReservationRetentionCandidateSnapshot source =
            ReservationRetentionTestData.Snapshot(fixture.Binding);
        ReservationRetentionEligibilityEvaluator evaluator =
            new(fixture.Registry);

        ReservationRetentionEligibilityResult stale =
            evaluator.Evaluate(
                source with
                {
                    ProcessingRestrictionContractVersion = null
                },
                Now);
        ReservationRetentionEligibilityResult missingPolicy =
            evaluator.Evaluate(
                source with
                {
                    Property = source.Property! with
                    {
                        GovernancePolicy = null
                    }
                },
                Now);

        Assert.Equal(
            ReservationRetentionEligibilityCode.ProjectionUnavailable,
            stale.Code);
        Assert.Equal(
            ReservationRetentionEligibilityCode.PolicyUnavailable,
            missingPolicy.Code);
    }
}
