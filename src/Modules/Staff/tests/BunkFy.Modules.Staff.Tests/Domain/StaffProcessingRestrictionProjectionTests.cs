namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffProcessingRestrictionProjectionTests
{
    [Fact]
    public void Multiple_restrictions_remain_effective_until_every_reference_is_released()
    {
        DateTimeOffset initializedAtUtc =
            new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        StaffProcessingRestrictionProjection projection =
            StaffProcessingRestrictionProjection.Create(
                "tenant-a",
                Guid.NewGuid(),
                contractVersion: 1,
                initializedAtUtc).Value;

        Assert.True(
            projection.Apply(
                0,
                1,
                initializedAtUtc.AddMinutes(1)).IsSuccess);
        Assert.True(
            projection.Apply(
                1,
                1,
                initializedAtUtc.AddMinutes(2)).IsSuccess);
        Assert.True(
            projection.Release(
                2,
                1,
                initializedAtUtc.AddMinutes(3)).IsSuccess);

        Assert.True(projection.IsRestricted);
        Assert.Equal(1, projection.ActiveRestrictionCount);
        Assert.Equal(3, projection.Revision);

        Assert.True(
            projection.Release(
                3,
                1,
                initializedAtUtc.AddMinutes(4)).IsSuccess);
        Assert.False(projection.IsRestricted);
        Assert.Equal(0, projection.ActiveRestrictionCount);
        Assert.Equal(4, projection.Revision);
    }

    [Fact]
    public void Unsupported_stale_and_invalid_release_transitions_fail_closed()
    {
        DateTimeOffset initializedAtUtc =
            new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        StaffProcessingRestrictionProjection projection =
            StaffProcessingRestrictionProjection.Create(
                "tenant-a",
                Guid.NewGuid(),
                contractVersion: 1,
                initializedAtUtc).Value;

        Result unsupported = projection.Apply(
            0,
            2,
            initializedAtUtc.AddMinutes(1));
        Result stale = projection.Apply(
            1,
            1,
            initializedAtUtc.AddMinutes(1));
        Result releaseWithoutReference = projection.Release(
            0,
            1,
            initializedAtUtc.AddMinutes(1));

        Assert.Equal(
            "Staff.RestrictionProjectionContractUnsupported",
            unsupported.Error.Code);
        Assert.Equal(
            "Staff.RestrictionProjectionVersionConflict",
            stale.Error.Code);
        Assert.Equal(
            "Staff.RestrictionProjectionStateInvalid",
            releaseWithoutReference.Error.Code);
        Assert.False(projection.IsRestricted);
        Assert.Equal(0, projection.Revision);
    }
}
