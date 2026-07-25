namespace BunkFy.Modules.Reservations.Tests.Domain;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Events;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationProcessingRestrictionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Restriction_release_requires_exact_version_and_records_lifecycle()
    {
        Guid releaseCaseId = Guid.NewGuid();
        ReservationProcessingRestriction restriction = CreateRestriction();

        Result stale = restriction.Release(
            releaseCaseId,
            releaseApprovalRevision: 2,
            releaseSelectedReservationVersion: 7,
            expectedVersion: 2,
            actorId: "user:privacy",
            releasedAtUtc: Now.AddMinutes(1));
        Result released = restriction.Release(
            releaseCaseId,
            releaseApprovalRevision: 2,
            releaseSelectedReservationVersion: 7,
            expectedVersion: 1,
            actorId: "user:privacy",
            releasedAtUtc: Now.AddMinutes(1));

        Assert.True(stale.IsFailure);
        Assert.True(released.IsSuccess, released.Error.Code);
        Assert.Equal(ReservationProcessingRestrictionStatus.Released, restriction.Status);
        Assert.Equal(2, restriction.Version);
        Assert.Equal(releaseCaseId, restriction.ReleaseCaseId);
        Assert.Equal("user:privacy", restriction.ReleasedBy);
    }

    [Fact]
    public void Effective_state_composes_overlapping_cases_and_rebuilds_exactly()
    {
        ReservationProcessingRestrictionProjection projection =
            ReservationProcessingRestrictionProjection.Create(
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                ReservationProcessingRestrictionContract.CurrentVersion,
                Now).Value;

        Assert.True(projection.Apply(0, 1, Now).IsSuccess);
        Assert.True(projection.Apply(1, 1, Now.AddSeconds(1)).IsSuccess);
        Assert.True(projection.Release(2, 1, Now.AddSeconds(2)).IsSuccess);
        Assert.True(projection.IsRestricted);
        Assert.Equal(1, projection.ActiveRestrictionCount);
        Assert.Equal(3, projection.Revision);

        Assert.True(projection.ReplaceForRebuild(
            contractVersion: 1,
            revision: 4,
            activeRestrictionCount: 0,
            lastTransitionAtUtc: Now.AddSeconds(3)).IsSuccess);
        Assert.False(projection.IsRestricted);
        Assert.Equal(4, projection.Revision);
    }

    [Fact]
    public void Receipt_raises_constant_size_pii_free_event()
    {
        ReservationProcessingRestrictionReceipt receipt =
            ReservationProcessingRestrictionReceipt.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                ReservationProcessingRestrictionAction.Apply,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 3,
                selectedReservationVersion: 7,
                contractVersion: 1,
                resultingRestrictionVersion: 1,
                resultingProjectionRevision: 1,
                effectiveRestricted: true,
                Guid.NewGuid(),
                Now).Value;

        ReservationProcessingRestrictionChangedDomainEvent domainEvent =
            Assert.IsType<ReservationProcessingRestrictionChangedDomainEvent>(
                Assert.Single(receipt.DomainEvents));
        Assert.True(domainEvent.IsRestricted);
        Assert.DoesNotContain(
            receipt.GetType().GetProperties(),
            property => property.Name is
                "ActorId" or
                "PrimaryGuestName" or
                "Email" or
                "Phone" or
                "Notes");
        Assert.DoesNotContain(
            domainEvent.GetType().GetProperties(),
            property => property.Name is
                "ActorId" or
                "PrimaryGuestName" or
                "Email" or
                "Phone" or
                "Notes");
    }

    private static ReservationProcessingRestriction CreateRestriction() =>
        ReservationProcessingRestriction.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            applyApprovalRevision: 1,
            applySelectedReservationVersion: 7,
            actorId: "user:privacy",
            appliedAtUtc: Now).Value;
}
