namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using BunkFy.Modules.Ingestion.Tests.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionSourceGraphLocatorTests
{
    [Fact]
    public async Task Every_graph_entry_point_resolves_the_same_source_coordinate_untracked()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(
                    $"ingestion-source-locator-{Guid.NewGuid():N}")
                .Options;
        IngestionAnonymisationRestoreTests.TestScopeContext scope = new();
        await using IngestionDbContext dbContext = new(options, scope);
        IngestionAnonymisationRestoreTests.SeededGraph seeded =
            await IngestionAnonymisationRestoreTests.SeedAsync(dbContext);
        ChangeProposal proposal = ChangeProposal.Create(
            Guid.NewGuid(),
            seeded.Receipt.ScopeId,
            seeded.PropertyId,
            seeded.Receipt.ConnectionId,
            seeded.Receipt.Id,
            Guid.NewGuid(),
            seeded.Receipt.RawPayloadFileId,
            1,
            "staff-conflict",
            "{\"guest\":\"Maya Chen\"}",
            IngestionAnonymisationRestoreTests.Now).Value;
        Guid attemptId = Guid.NewGuid();
        ObservationReprocessingAttempt attempt =
            ObservationReprocessingAttempt.Create(
                attemptId,
                seeded.Receipt.ScopeId,
                seeded.PropertyId,
                seeded.Receipt.ConnectionId,
                seeded.Receipt.Id,
                attemptId,
                "reservation.v1",
                2,
                "user:operator",
                IngestionAnonymisationRestoreTests.Now,
                IngestionAnonymisationRestoreTests.Now.AddHours(1)).Value;
        Guid reservationId = Guid.NewGuid();
        ReservationDispatch acceptedCancellation =
            ReservationDispatch.Create(
                Guid.NewGuid(),
                seeded.Receipt.ScopeId,
                seeded.SourceLink.Id,
                ReservationDispatchTriggerKind.Observation,
                Guid.NewGuid(),
                seeded.Receipt.Id,
                seeded.Receipt.ConnectionId,
                seeded.PropertyId,
                reservationId,
                ReservationDispatchKind.Cancel,
                seeded.Receipt.SourceRevision,
                sourceSequence: 4,
                "{\"operation\":\"cancel\"}",
                expectedDetailsRevision: 4,
                IngestionAnonymisationRestoreTests.Now).Value;
        Assert.True(acceptedCancellation.Complete(
            ReservationDispatchState.Accepted,
            reservationId,
            detailsRevision: 4,
            reservationVersion: 5,
            errorCode: null,
            sensitiveDataRetainUntilUtc: null,
            IngestionAnonymisationRestoreTests.Now.AddMinutes(1)).IsSuccess);
        dbContext.AddRange(proposal, attempt, acceptedCancellation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        IngestionSourceGraphLocator locator = new(dbContext);

        IngestionSourceGraphCoordinate? receipt =
            await locator.FindReceiptAsync(
                seeded.Receipt.Id,
                CancellationToken.None);
        IngestionSourceGraphCoordinate? locatedProposal =
            await locator.FindProposalAsync(
                proposal.Id,
                CancellationToken.None);
        IngestionSourceGraphCoordinate? dispatch =
            await locator.FindDispatchAsync(
                seeded.Dispatch.Id,
                CancellationToken.None);
        IngestionSourceGraphCoordinate? locatedAttempt =
            await locator.FindReprocessingAttemptAsync(
                attempt.Id,
                CancellationToken.None);
        IngestionSourceGraphCoordinate? source =
            await locator.FindSourceLinkAsync(
                seeded.SourceLink.Id,
                CancellationToken.None);
        IngestionSourceGraphCoordinate? cancellation =
            await locator.FindAcceptedCancellationAsync(
                reservationId,
                CancellationToken.None);

        AssertCoordinate(receipt, seeded.Receipt.Id, seeded);
        AssertCoordinate(locatedProposal, proposal.Id, seeded);
        AssertCoordinate(dispatch, seeded.Dispatch.Id, seeded);
        AssertCoordinate(locatedAttempt, attempt.Id, seeded);
        AssertCoordinate(source, seeded.SourceLink.Id, seeded);
        AssertCoordinate(
            cancellation,
            acceptedCancellation.Id,
            seeded);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    private static void AssertCoordinate(
        IngestionSourceGraphCoordinate? coordinate,
        Guid recordId,
        IngestionAnonymisationRestoreTests.SeededGraph seeded)
    {
        Assert.NotNull(coordinate);
        Assert.Equal(recordId, coordinate.RecordId);
        Assert.Equal(
            seeded.Receipt.ConnectionId,
            coordinate.ConnectionId);
        Assert.Equal(seeded.SourceLink.Id, coordinate.SourceLinkId);
    }
}
