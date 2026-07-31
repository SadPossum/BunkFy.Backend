namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class IngestionNotificationSourceLinkResolverTests
{
    private const string ScopeId =
        "00000000-0000-0000-0000-000000000111";

    [Fact]
    public async Task Resolves_only_the_exact_dispatch_correlation()
    {
        await using IngestionDbContext dbContext = CreateContext();
        SeededCorrelation seeded = await SeedAsync(
            dbContext,
            anonymise: false);
        IngestionNotificationSourceLinkResolver resolver =
            new(dbContext);

        IngestionNotificationSourceLink? result =
            await resolver.ResolveAsync(
                ScopeId,
                seeded.PropertyId,
                seeded.ConnectionId,
                seeded.OperationId,
                seeded.ReceiptId,
                CancellationToken.None);

        Assert.Equal(
            seeded.SourceLinkId,
            Assert.IsType<IngestionNotificationSourceLink>(result)
                .SourceLinkId);
    }

    [Fact]
    public async Task Rejects_mismatched_dispatch_coordinates()
    {
        await using IngestionDbContext dbContext = CreateContext();
        SeededCorrelation seeded = await SeedAsync(
            dbContext,
            anonymise: false);
        IngestionNotificationSourceLinkResolver resolver =
            new(dbContext);

        Assert.Null(await resolver.ResolveAsync(
            "00000000-0000-0000-0000-000000000222",
            seeded.PropertyId,
            seeded.ConnectionId,
            seeded.OperationId,
            seeded.ReceiptId,
            CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync(
            ScopeId,
            Guid.NewGuid(),
            seeded.ConnectionId,
            seeded.OperationId,
            seeded.ReceiptId,
            CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync(
            ScopeId,
            seeded.PropertyId,
            Guid.NewGuid(),
            seeded.OperationId,
            seeded.ReceiptId,
            CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync(
            ScopeId,
            seeded.PropertyId,
            seeded.ConnectionId,
            Guid.NewGuid(),
            seeded.ReceiptId,
            CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync(
            ScopeId,
            seeded.PropertyId,
            seeded.ConnectionId,
            seeded.OperationId,
            Guid.NewGuid(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Resolves_anonymised_source_link_for_late_replay_suppression()
    {
        await using IngestionDbContext dbContext = CreateContext();
        SeededCorrelation seeded = await SeedAsync(
            dbContext,
            anonymise: true);
        IngestionNotificationSourceLinkResolver resolver =
            new(dbContext);

        IngestionNotificationSourceLink? result =
            await resolver.ResolveAsync(
                ScopeId,
                seeded.PropertyId,
                seeded.ConnectionId,
                seeded.OperationId,
                seeded.ReceiptId,
                CancellationToken.None);

        Assert.Equal(
            seeded.SourceLinkId,
            Assert.IsType<IngestionNotificationSourceLink>(result)
                .SourceLinkId);
    }

    private static async Task<SeededCorrelation> SeedAsync(
        IngestionDbContext dbContext,
        bool anonymise)
    {
        Guid sourceLinkId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        DateTimeOffset nowUtc =
            new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);

        ReservationSourceLink sourceLink =
            ReservationSourceLink.Create(
                    sourceLinkId,
                    ScopeId,
                    propertyId,
                    connectionId,
                    "test-adapter",
                    "external-reservation",
                    nowUtc)
                .Value;
        _ = sourceLink.Observe(
            receiptId,
            "revision-1",
            1,
            nowUtc,
            new string('a', ReservationSourceLink.ContentHashLength),
            nowUtc);
        _ = sourceLink.BeginDispatch(operationId, nowUtc);
        _ = sourceLink.CompleteDispatch(
            operationId,
            receiptId,
            "revision-1",
            1,
            operationalBaseline: null,
            reservationId,
            detailsRevision: 1,
            keepActive: false,
            applied: true,
            cancellationPending: false,
            cancelled: true,
            nowUtc);

        ReservationDispatch dispatch =
            ReservationDispatch.Create(
                    operationId,
                    ScopeId,
                    sourceLinkId,
                    ReservationDispatchTriggerKind.Observation,
                    receiptId,
                    receiptId,
                    connectionId,
                    propertyId,
                    reservationId,
                    ReservationDispatchKind.Cancel,
                    "revision-1",
                    1,
                    "{}",
                    expectedDetailsRevision: 1,
                    nowUtc)
                .Value;
        _ = dispatch.Complete(
            ReservationDispatchState.Applied,
            reservationId,
            detailsRevision: 1,
            reservationVersion: 1,
            errorCode: null,
            sensitiveDataRetainUntilUtc: nowUtc.AddDays(1),
            nowUtc);

        if (anonymise)
        {
            _ = sourceLink.Anonymise(
                sourceLink.Version,
                nowUtc.AddMinutes(1));
            _ = dispatch.Anonymise(nowUtc.AddMinutes(1));
        }

        dbContext.ReservationSourceLinks.Add(sourceLink);
        dbContext.ReservationDispatches.Add(dispatch);
        await dbContext.SaveChangesAsync();
        return new(
            sourceLinkId,
            propertyId,
            connectionId,
            operationId,
            receiptId);
    }

    private static IngestionDbContext CreateContext()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(
                    $"ingestion-notification-source-link-{Guid.NewGuid():N}")
                .Options;
        return new IngestionDbContext(
            options,
            new TestScopeContext(ScopeId));
    }

    private sealed record SeededCorrelation(
        Guid SourceLinkId,
        Guid PropertyId,
        Guid ConnectionId,
        Guid OperationId,
        Guid ReceiptId);

    private sealed class TestScopeContext(string scopeId)
        : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
