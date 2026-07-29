namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Events;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffProcessingRestrictionTests
{
    [Fact]
    public void Release_records_a_distinct_approved_decision_and_advances_version()
    {
        DateTimeOffset appliedAtUtc =
            new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        StaffProcessingRestriction restriction =
            StaffProcessingRestriction.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                applyApprovalRevision: 4,
                applySelectedStaffVersion: 7,
                "staff:privacy",
                appliedAtUtc).Value;
        Guid releaseCaseId = Guid.NewGuid();

        Result released = restriction.Release(
            releaseCaseId,
            releaseApprovalRevision: 9,
            releaseSelectedStaffVersion: 8,
            expectedVersion: 1,
            "staff:decision-maker",
            appliedAtUtc.AddMinutes(5));

        Assert.True(released.IsSuccess);
        Assert.Equal(
            StaffProcessingRestrictionState.Released,
            restriction.Status);
        Assert.Equal(2, restriction.Version);
        Assert.Equal(releaseCaseId, restriction.ReleaseCaseId);
        Assert.Equal(9, restriction.ReleaseApprovalRevision);
        Assert.Equal(8, restriction.ReleaseSelectedStaffVersion);
        Assert.Equal("staff:decision-maker", restriction.ReleasedBy);
    }

    [Fact]
    public void Stale_or_repeated_release_does_not_change_state()
    {
        DateTimeOffset appliedAtUtc =
            new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        StaffProcessingRestriction restriction =
            StaffProcessingRestriction.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                4,
                7,
                "staff:privacy",
                appliedAtUtc).Value;

        Result stale = restriction.Release(
            Guid.NewGuid(),
            5,
            7,
            expectedVersion: 2,
            "staff:privacy",
            appliedAtUtc.AddMinutes(1));
        Assert.Equal("Staff.RestrictionVersionConflict", stale.Error.Code);
        Assert.Equal(
            StaffProcessingRestrictionState.Active,
            restriction.Status);

        Assert.True(restriction.Release(
            Guid.NewGuid(),
            5,
            7,
            expectedVersion: 1,
            "staff:privacy",
            appliedAtUtc.AddMinutes(1)).IsSuccess);
        Result repeated = restriction.Release(
            Guid.NewGuid(),
            6,
            7,
            expectedVersion: 2,
            "staff:privacy",
            appliedAtUtc.AddMinutes(2));
        Assert.Equal("Staff.RestrictionAlreadyReleased", repeated.Error.Code);
        Assert.Equal(2, restriction.Version);
    }

    [Fact]
    public void Receipt_binds_action_versions_actor_and_effective_outcome()
    {
        Result<StaffProcessingRestrictionReceipt> invalidApply =
            StaffProcessingRestrictionReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                StaffProcessingRestrictionAction.Apply,
                Guid.NewGuid(),
                Guid.NewGuid(),
                3,
                5,
                1,
                1,
                8,
                effectiveRestricted: false,
                "staff:privacy",
                Guid.NewGuid(),
                new DateTimeOffset(
                    2026,
                    7,
                    29,
                    12,
                    0,
                    0,
                    TimeSpan.Zero));

        Assert.Equal(
            "Staff.RestrictionReceiptTransitionInvalid",
            invalidApply.Error.Code);

        StaffProcessingRestrictionReceipt release =
            StaffProcessingRestrictionReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                StaffProcessingRestrictionAction.Release,
                Guid.NewGuid(),
                Guid.NewGuid(),
                3,
                5,
                1,
                2,
                8,
                effectiveRestricted: false,
                "staff:privacy",
                Guid.NewGuid(),
                new DateTimeOffset(
                    2026,
                    7,
                    29,
                    12,
                    0,
                    0,
                    TimeSpan.Zero)).Value;
        Assert.Equal(
            StaffProcessingRestrictionAction.Release,
            release.Action);
        Assert.False(release.EffectiveRestricted);
        Assert.Equal(2, release.ResultingRestrictionVersion);
        Assert.Equal(8, release.ResultingProjectionRevision);
        StaffProcessingRestrictionChangedDomainEvent domainEvent =
            Assert.IsType<StaffProcessingRestrictionChangedDomainEvent>(
                Assert.Single(release.DomainEvents));
        Assert.Equal(release.StaffMemberId, domainEvent.StaffMemberId);
        Assert.Equal(1, domainEvent.ContractVersion);
        Assert.Equal(8, domainEvent.ProjectionRevision);
        Assert.False(domainEvent.IsRestricted);
    }
}
