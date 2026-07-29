namespace BunkFy.Modules.Staff.Tests.Domain;

using BunkFy.Modules.Staff.Domain.Governance;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffEmploymentGovernanceTests
{
    private static readonly DateTimeOffset EvaluatedAt =
        new(2026, 7, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Configure_sorts_evidence_and_replace_is_versioned()
    {
        StaffEmploymentGovernanceAcknowledgement second =
            Acknowledgement("notice-b", 2);
        StaffEmploymentGovernanceAcknowledgement first =
            Acknowledgement("notice-a", 1);
        StaffEmploymentGovernance governance =
            StaffEmploymentGovernance.Configure(
                "tenant-a",
                Guid.NewGuid(),
                selectedStaffVersion: 7,
                Binding(),
                [second, first],
                "user:privacy",
                EvaluatedAt.AddMinutes(1)).Value;

        Assert.Equal(1, governance.Version);
        Assert.Equal(
            ["notice-a", "notice-b"],
            governance.AcceptedAcknowledgements
                .Select(item => item.AcknowledgementId));

        Result stale = governance.Replace(
            expectedGovernanceVersion: 2,
            selectedStaffVersion: 8,
            Binding(),
            [first],
            "user:privacy",
            EvaluatedAt.AddMinutes(2));
        Assert.Equal(
            "Staff.EmploymentGovernanceVersionConflict",
            stale.Error.Code);
        Assert.Equal(1, governance.Version);
        Assert.Equal(7, governance.SelectedStaffVersion);

        Assert.True(governance.Replace(
            expectedGovernanceVersion: 1,
            selectedStaffVersion: 8,
            Binding(),
            [first],
            "user:privacy",
            EvaluatedAt.AddMinutes(2)).IsSuccess);
        Assert.Equal(2, governance.Version);
        Assert.Equal(8, governance.SelectedStaffVersion);
    }

    [Fact]
    public void Receipt_binds_the_exact_change_and_replay_fingerprint()
    {
        Guid staffMemberId = Guid.NewGuid();
        StaffEmploymentGovernance governance =
            StaffEmploymentGovernance.Configure(
                "tenant-a",
                staffMemberId,
                selectedStaffVersion: 4,
                Binding(),
                [Acknowledgement("notice-a", 1)],
                "user:privacy",
                EvaluatedAt.AddMinutes(1)).Value;
        string requestSha256 = new('b', 64);

        StaffEmploymentGovernanceChangeReceipt receipt =
            StaffEmploymentGovernanceChangeReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                governance,
                previousGovernanceVersion: 0,
                requestSha256,
                "user:privacy",
                EvaluatedAt.AddMinutes(1)).Value;

        Assert.Equal(64, receipt.AcknowledgementsSha256.Length);
        Assert.Equal(64, receipt.ReceiptSha256.Length);
        Assert.True(receipt.MatchesReplay(
            staffMemberId,
            expectedStaffVersion: 4,
            expectedGovernanceVersion: 0,
            requestSha256));
        Assert.False(receipt.MatchesReplay(
            staffMemberId,
            expectedStaffVersion: 5,
            expectedGovernanceVersion: 0,
            requestSha256));
    }

    [Fact]
    public void Replacement_rejects_regressed_staff_version_or_time()
    {
        StaffEmploymentGovernanceAcknowledgement acknowledgement =
            Acknowledgement("notice-a", 1);
        StaffEmploymentGovernance governance =
            StaffEmploymentGovernance.Configure(
                "tenant-a",
                Guid.NewGuid(),
                selectedStaffVersion: 7,
                Binding(),
                [acknowledgement],
                "user:privacy",
                EvaluatedAt.AddMinutes(2)).Value;

        Result versionRegression = governance.Replace(
            expectedGovernanceVersion: 1,
            selectedStaffVersion: 6,
            Binding(),
            [acknowledgement],
            "user:privacy",
            EvaluatedAt.AddMinutes(3));
        Result timeRegression = governance.Replace(
            expectedGovernanceVersion: 1,
            selectedStaffVersion: 7,
            Binding(),
            [acknowledgement],
            "user:privacy",
            EvaluatedAt.AddMinutes(1));

        Assert.Equal(
            "Staff.EmploymentGovernanceLifecycleInvalid",
            versionRegression.Error.Code);
        Assert.Equal(
            "Staff.EmploymentGovernanceLifecycleInvalid",
            timeRegression.Error.Code);
        Assert.Equal(1, governance.Version);
    }

    [Fact]
    public void Duplicate_acknowledgements_and_stale_policy_evidence_are_rejected()
    {
        StaffEmploymentGovernanceAcknowledgement acknowledgement =
            Acknowledgement("notice-a", 1);
        Result<StaffEmploymentGovernance> duplicate =
            StaffEmploymentGovernance.Configure(
                "tenant-a",
                Guid.NewGuid(),
                selectedStaffVersion: 1,
                Binding(),
                [acknowledgement, acknowledgement],
                "user:privacy",
                EvaluatedAt.AddMinutes(1));
        Result<StaffEmploymentGovernance> stale =
            StaffEmploymentGovernance.Configure(
                "tenant-a",
                Guid.NewGuid(),
                selectedStaffVersion: 1,
                Binding(),
                [acknowledgement],
                "user:privacy",
                EvaluatedAt.AddMinutes(-1));

        Assert.Equal(
            "Staff.EmploymentGovernanceAcknowledgementsInvalid",
            duplicate.Error.Code);
        Assert.Equal(
            "Staff.EmploymentGovernanceLifecycleInvalid",
            stale.Error.Code);
    }

    private static StaffEmploymentGovernanceBinding Binding() =>
        StaffEmploymentGovernanceBinding.Create(
            "GB",
            "staff-test",
            1,
            "eu-west-2",
            "uk-no-transfer",
            "staff-employment",
            1,
            new string('a', 64),
            new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero),
            new DateTimeOffset(
                2027,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero),
            EvaluatedAt).Value;

    private static StaffEmploymentGovernanceAcknowledgement Acknowledgement(
        string id,
        int version) =>
        StaffEmploymentGovernanceAcknowledgement.Create(id, version).Value;
}
