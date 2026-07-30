namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Application.Policies;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffRetentionEligibilityEvaluatorTests
{
    [Fact]
    public void Due_departed_profile_with_current_policy_is_eligible()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(policy.Governance);

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.Eligible,
            result.Status);
        Assert.Equal(
            snapshot.DepartedAtUtc,
            result.DepartedAtUtc);
        Assert.Equal(
            snapshot.DepartedAtUtc!.Value.AddDays(365),
            result.RetentionDeadlineUtc);
        Assert.Equal(64, result.PolicyEvidenceSha256!.Length);
    }

    [Fact]
    public void Future_deadline_is_not_due()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(
                policy.Governance,
                departedAtUtc:
                    StaffRetentionTestData.Now.AddDays(-30));

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.NotDue,
            result.Status);
        Assert.Equal(
            StaffRetentionEligibilityCode.PeriodNotElapsed,
            result.Code);
    }

    [Theory]
    [InlineData(StaffMemberState.Active)]
    [InlineData(StaffMemberState.Suspended)]
    public void Non_departed_profile_is_not_due(
        StaffMemberState status)
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(
                policy.Governance,
                status);

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.NotDue,
            result.Status);
        Assert.Equal(
            StaffRetentionEligibilityCode.NotDeparted,
            result.Code);
    }

    [Fact]
    public void Current_assignment_is_not_due()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(policy.Governance) with
            {
                HasCurrentAssignments = true
            };

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.NotDue,
            result.Status);
        Assert.Equal(
            StaffRetentionEligibilityCode.CurrentAssignment,
            result.Code);
    }

    [Fact]
    public void Active_hold_blocks_a_due_profile()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        DateTimeOffset placedAtUtc =
            StaffRetentionTestData.Now.AddDays(-2);
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(policy.Governance) with
            {
                ActiveHoldCount = 1,
                EarliestHoldPlacedAtUtc = placedAtUtc
            };

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.Blocked,
            result.Status);
        Assert.Equal(
            StaffRetentionEligibilityCode.ActiveDataHold,
            result.Code);
        Assert.Equal(placedAtUtc, result.HoldReviewDueAtUtc);
    }

    [Fact]
    public void Stale_governance_fails_closed()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(policy.Governance) with
            {
                Governance = policy.Governance with
                {
                    SelectedStaffVersion = 3
                }
            };

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.Failed,
            result.Status);
        Assert.Equal(
            StaffRetentionEligibilityCode.PolicyUnavailable,
            result.Code);
    }

    [Fact]
    public void Future_governance_configuration_fails_closed()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(policy.Governance) with
            {
                Governance = policy.Governance with
                {
                    ConfiguredAtUtc =
                        StaffRetentionTestData.Now.AddMinutes(1)
                }
            };

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.Failed,
            result.Status);
        Assert.Equal(
            StaffRetentionEligibilityCode.PolicyUnavailable,
            result.Code);
    }

    [Fact]
    public void Governance_policy_window_mismatch_fails_closed()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(policy.Governance) with
            {
                Governance = policy.Governance with
                {
                    PolicyExpiresAtUtc =
                        policy.Governance.PolicyExpiresAtUtc
                            .AddDays(1)
                }
            };

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.Failed,
            result.Status);
        Assert.Equal(
            StaffRetentionEligibilityCode.PolicyUnavailable,
            result.Code);
    }

    [Fact]
    public void Unknown_policy_digest_fails_closed()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(policy.Governance) with
            {
                Governance = policy.Governance with
                {
                    ContentSha256 = new string('b', 64)
                }
            };

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.Failed,
            result.Status);
        Assert.Equal(
            StaffRetentionEligibilityCode.PolicyUnavailable,
            result.Code);
    }

    [Fact]
    public void Restriction_state_does_not_defeat_retention()
    {
        StaffRetentionPolicyFixture policy =
            StaffRetentionTestData.CreatePolicy();
        StaffRetentionCandidateSnapshot snapshot =
            StaffRetentionTestData.Snapshot(policy.Governance) with
            {
                ProcessingRestrictionRevision = 7
            };

        StaffRetentionEligibilityResult result =
            new StaffRetentionEligibilityEvaluator(policy.Registry)
                .Evaluate(snapshot, StaffRetentionTestData.Now);

        Assert.Equal(
            StaffRetentionEligibilityStatus.Eligible,
            result.Status);
        Assert.Equal(
            StaffProcessingRestrictionContract.CurrentVersion,
            snapshot.ProcessingRestrictionContractVersion);
    }
}
