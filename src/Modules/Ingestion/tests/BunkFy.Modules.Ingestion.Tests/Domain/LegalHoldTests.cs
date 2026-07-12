namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Modules.Ingestion.Domain.Errors;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using Xunit;

[Trait("Category", "Unit")]
public sealed class LegalHoldTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Place_and_release_preserve_complete_audit_history()
    {
        LegalHold legalHold = LegalHold.Place(
            Guid.NewGuid(),
            " tenant-a ",
            Guid.NewGuid(),
            " Litigation 2026-14 ",
            " user:compliance ",
            Now).Value;

        var released = legalHold.Release(
            legalHold.Version,
            " user:legal-lead ",
            " Counsel approved release ",
            Now.AddDays(10));

        Assert.True(released.IsSuccess);
        Assert.Equal("tenant-a", legalHold.ScopeId);
        Assert.Equal("Litigation 2026-14", legalHold.Reason);
        Assert.Equal(LegalHoldState.Released, legalHold.State);
        Assert.Equal("user:compliance", legalHold.PlacedBy);
        Assert.Equal("user:legal-lead", legalHold.ReleasedBy);
        Assert.Equal("Counsel approved release", legalHold.ReleaseReason);
        Assert.Equal(Now.AddDays(10), legalHold.ReleasedAtUtc);
        Assert.Equal(2, legalHold.Version);
    }

    [Fact]
    public void Release_rejects_stale_version_repeat_and_invalid_lifecycle()
    {
        LegalHold legalHold = LegalHold.Place(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "Investigation", "user:legal", Now).Value;

        Assert.Equal(
            IngestionDomainErrors.VersionConflict,
            legalHold.Release(2, "user:legal", "Wrong version", Now.AddDays(1)).Error);
        Assert.Equal(
            IngestionDomainErrors.LegalHoldLifecycleInvalid,
            legalHold.Release(1, "user:legal", "Clock moved backwards", Now.AddTicks(-1)).Error);
        Assert.True(legalHold.Release(1, "user:legal", "Closed", Now.AddDays(1)).IsSuccess);
        Assert.Equal(
            IngestionDomainErrors.LegalHoldAlreadyReleased,
            legalHold.Release(2, "user:legal", "Again", Now.AddDays(2)).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Place_rejects_blank_reason_or_actor(string invalid)
    {
        Assert.Equal(
            IngestionDomainErrors.LegalHoldReasonInvalid,
            LegalHold.Place(
                Guid.NewGuid(), "tenant-a", Guid.NewGuid(), invalid, "user:legal", Now).Error);
        Assert.Equal(
            IngestionDomainErrors.LegalHoldActorInvalid,
            LegalHold.Place(
                Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "Reason", invalid, Now).Error);
    }
}
