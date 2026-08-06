namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Text;
using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAnonymisationRestoreTests
{
    internal const string TenantId = "tenant-a";
    internal static readonly DateTimeOffset Now =
        new(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Restore_is_staged_retry_safe_and_conflict_checked()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(
                    $"ingestion-anonymisation-restore-{Guid.NewGuid():N}")
                .Options;
        TestScopeContext scope = new();
        await using IngestionDbContext dbContext = new(options, scope);
        SeededGraph seeded = await SeedAsync(dbContext);
        TestClock clock = new(Now.AddHours(1));
        TestRawPayloadStore rawPayloads = new();
        rawPayloads.Add(
            seeded.Receipt.RawPayloadFileId,
            seeded.Receipt.ConnectionId,
            Encoding.UTF8.GetBytes("""{"guest":"Maya Chen"}"""));
        IngestionAnonymisationRestoreRepository repository = new(
            dbContext,
            new IngestionDataRightsEvidenceGraphLoader(dbContext));
        IngestionSourceMutationCoordinator sourceMutations =
            TestIngestionSource.Create(dbContext, scope);
        HmacIngestionAnonymisationFingerprintService fingerprintService =
            new(Options.Create(FingerprintOptions()));
        DataRightsAnonymisationRestoreRequest request =
            CreateRequest(seeded);
        BeginIngestionAnonymisationRestoreCommandHandler begin = new(
            repository,
            sourceMutations,
            fingerprintService,
            scope,
            clock,
            new TestIds());
        CompleteIngestionAnonymisationRestoreCommandHandler complete = new(
            repository,
            sourceMutations,
            rawPayloads,
            scope,
            clock);

        Result<IngestionAnonymisationRestoreStage> started =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationRestoreCommand(request),
                CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Single(started.Value.RawPayloads);
        await dbContext.SaveChangesAsync();
        Assert.Equal(
            ReservationSourceLinkState.Anonymised,
            seeded.SourceLink.State);
        Assert.Equal(
            RawPayloadRetentionState.Purging,
            seeded.Receipt.RawPayloadRetentionState);
        Assert.Equal(request.LedgerEntryId, seeded.Receipt.RawPayloadPurgeClaimId);
        Assert.Equal(
            $"anonymised:{seeded.SourceLink.Id:N}",
            seeded.SourceLink.SourceReference);
        Assert.Equal(
            $"anonymised:{seeded.Receipt.Id:N}",
            seeded.Receipt.ExternalId);
        Assert.Equal(
            2,
            await dbContext.AnonymisationFingerprints.CountAsync());
        Assert.Equal(
            3,
            await dbContext.AnonymisationRecordPlan.CountAsync());

        Result<DataRightsAnonymisationRestoreProof> premature =
            await complete.HandleAsync(
                new CompleteIngestionAnonymisationRestoreCommand(request),
                CancellationToken.None);

        Assert.True(premature.IsFailure);
        Assert.Equal(
            IngestionApplicationErrors
                .AnonymisationRawPayloadDeletionIncomplete,
            premature.Error);
        Assert.Equal(
            IngestionAnonymisationTombstoneState.Reducing,
            (await dbContext.AnonymisationTombstones.SingleAsync()).State);

        Assert.True(await rawPayloads.DeleteAsync(
            seeded.Receipt.RawPayloadFileId,
            TenantId,
            seeded.Receipt.ConnectionId,
            CancellationToken.None));
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Result<DataRightsAnonymisationRestoreProof> completed =
            await complete.HandleAsync(
                new CompleteIngestionAnonymisationRestoreCommand(request),
                CancellationToken.None);

        Assert.True(completed.IsSuccess);
        await dbContext.SaveChangesAsync();
        Assert.Equal(
            request.ResultingRecordVersion,
            completed.Value.ResultingRecordVersion);
        Assert.Equal(
            RawPayloadRetentionState.Purged,
            seeded.Receipt.RawPayloadRetentionState);
        Assert.Equal(
            IngestionAnonymisationTombstoneState.Completed,
            (await dbContext.AnonymisationTombstones.SingleAsync()).State);

        Result<IngestionAnonymisationRestoreStage> replay =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationRestoreCommand(request),
                CancellationToken.None);
        Result<DataRightsAnonymisationRestoreProof> replayed =
            await complete.HandleAsync(
                new CompleteIngestionAnonymisationRestoreCommand(request),
                CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.True(replayed.IsSuccess);
        Assert.Equal(completed.Value, replayed.Value);

        DataRightsAnonymisationRestoreRequest conflicting = request with
        {
            LedgerEntryId = Guid.NewGuid()
        };
        Result<IngestionAnonymisationRestoreStage> conflict =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationRestoreCommand(conflicting),
                CancellationToken.None);

        Assert.True(conflict.IsFailure);
        Assert.Equal(
            IngestionApplicationErrors.AnonymisationRestoreProofConflict,
            conflict.Error);
    }

    [Fact]
    public async Task Stale_restore_version_fails_without_reducing_owner_state()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(
                    $"ingestion-anonymisation-stale-{Guid.NewGuid():N}")
                .Options;
        TestScopeContext scope = new();
        await using IngestionDbContext dbContext = new(options, scope);
        SeededGraph seeded = await SeedAsync(dbContext);
        string sourceReference = seeded.SourceLink.SourceReference;
        string externalId = seeded.Receipt.ExternalId;
        BeginIngestionAnonymisationRestoreCommandHandler begin =
            CreateBeginHandler(dbContext, scope);
        DataRightsAnonymisationRestoreRequest request =
            CreateRequest(seeded) with
            {
                ResultingRecordVersion = seeded.SourceLink.Version + 2
            };

        Result<IngestionAnonymisationRestoreStage> result =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationRestoreCommand(request),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            IngestionApplicationErrors.AnonymisationRestoreProofConflict,
            result.Error);
        Assert.Equal(sourceReference, seeded.SourceLink.SourceReference);
        Assert.Equal(externalId, seeded.Receipt.ExternalId);
        Assert.Null(seeded.SourceLink.AnonymisedAtUtc);
        Assert.Null(seeded.Receipt.AnonymisedAtUtc);
        Assert.Empty(dbContext.AnonymisationTombstones);
        Assert.Empty(dbContext.AnonymisationFingerprints);
        Assert.Empty(dbContext.AnonymisationRecordPlan);
    }

    [Fact]
    public async Task Incomplete_restore_graph_fails_without_reducing_source_link()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(
                    $"ingestion-anonymisation-incomplete-{Guid.NewGuid():N}")
                .Options;
        TestScopeContext scope = new();
        await using IngestionDbContext dbContext = new(options, scope);
        SeededGraph seeded = await SeedAsync(dbContext);
        string sourceReference = seeded.SourceLink.SourceReference;
        dbContext.ReservationDispatches.Remove(seeded.Dispatch);
        dbContext.ObservationReceipts.Remove(seeded.Receipt);
        await dbContext.SaveChangesAsync();
        BeginIngestionAnonymisationRestoreCommandHandler begin =
            CreateBeginHandler(dbContext, scope);

        Result<IngestionAnonymisationRestoreStage> result =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationRestoreCommand(
                    CreateRequest(seeded)),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            IngestionApplicationErrors.AnonymisationRestoreStateUnavailable,
            result.Error);
        Assert.Equal(sourceReference, seeded.SourceLink.SourceReference);
        Assert.Null(seeded.SourceLink.AnonymisedAtUtc);
        Assert.Empty(dbContext.AnonymisationTombstones);
        Assert.Empty(dbContext.AnonymisationFingerprints);
        Assert.Empty(dbContext.AnonymisationRecordPlan);
    }

    private static BeginIngestionAnonymisationRestoreCommandHandler
        CreateBeginHandler(
            IngestionDbContext dbContext,
            IScopeContext scope)
    {
        IngestionAnonymisationRestoreRepository repository = new(
            dbContext,
            new IngestionDataRightsEvidenceGraphLoader(dbContext));
        return new(
            repository,
            TestIngestionSource.Create(dbContext, scope),
            new HmacIngestionAnonymisationFingerprintService(
                Options.Create(FingerprintOptions())),
            scope,
            new TestClock(Now.AddHours(1)),
            new TestIds());
    }

    internal static async Task<SeededGraph> SeedAsync(
        IngestionDbContext dbContext)
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(),
            TenantId,
            propertyId,
            "booking.com",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged,
            "configuration://booking",
            secretReference: null,
            Now).Value;
        Guid reservationId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        ObservationReceipt receipt = ObservationReceipt.Create(
            receiptId,
            TenantId,
            propertyId,
            connection.Id,
            runId: null,
            Guid.NewGuid(),
            "reservation.v1",
            "booking-restore-42",
            "source-revision-3",
            "reservation.v1|booking-restore-42|source-revision-3",
            new string('a', 64),
            TestObservationCountryPolicyEvidence.Create(Now),
            receiptId,
            Now.AddDays(30),
            Now,
            Now,
            Now).Value;
        Assert.True(receipt.MarkProcessed(Now.AddMinutes(1)).IsSuccess);

        ReservationSourceLink sourceLink = ReservationSourceLink.Create(
            ReservationOperationIdentity.CreateSourceLinkId(
                TenantId,
                connection.Id,
                "booking-restore-42"),
            TenantId,
            propertyId,
            connection.Id,
            "booking.com",
            "booking-restore-42",
            Now).Value;
        Assert.True(sourceLink.Observe(
            receipt.Id,
            receipt.SourceRevision,
            sourceSequence: 3,
            receipt.SourceUpdatedAtUtc,
            receipt.ContentHash,
            Now.AddMinutes(1)).IsSuccess);
        Guid sourceOperationId = Guid.NewGuid();
        Assert.True(sourceLink.BeginDispatch(
            sourceOperationId,
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(sourceLink.CompleteDispatch(
            sourceOperationId,
            receipt.Id,
            receipt.SourceRevision,
            sourceSequence: 3,
            operationalBaseline: null,
            reservationId,
            detailsRevision: 4,
            keepActive: false,
            applied: true,
            cancellationPending: false,
            cancelled: true,
            Now.AddMinutes(3)).IsSuccess);

        ReservationDispatch dispatch = ReservationDispatch.Create(
            Guid.NewGuid(),
            TenantId,
            sourceLink.Id,
            ReservationDispatchTriggerKind.Observation,
            receipt.Id,
            receipt.Id,
            connection.Id,
            propertyId,
            reservationId,
            ReservationDispatchKind.Cancel,
            receipt.SourceRevision,
            sourceSequence: 3,
            """{"operation":"cancel"}""",
            expectedDetailsRevision: 3,
            Now.AddMinutes(2)).Value;
        Assert.True(dispatch.Complete(
            ReservationDispatchState.Applied,
            reservationId,
            detailsRevision: 4,
            reservationVersion: 5,
            errorCode: null,
            Now.AddDays(90),
            Now.AddMinutes(3)).IsSuccess);

        dbContext.AdapterConnections.Add(connection);
        dbContext.ObservationReceipts.Add(receipt);
        dbContext.ReservationSourceLinks.Add(sourceLink);
        dbContext.ReservationDispatches.Add(dispatch);
        await dbContext.SaveChangesAsync();
        return new(sourceLink, receipt, dispatch, propertyId);
    }

    private static DataRightsAnonymisationRestoreRequest CreateRequest(
        SeededGraph seeded) =>
        new(
            DataRightsAnonymisationRestoreContract.CurrentVersion,
            TenantId,
            Guid.NewGuid(),
            TenantSequence: 11,
            new string('b', 64),
            seeded.PropertyId,
            IngestionDataRightsCoordinates.Owner,
            IngestionDataRightsCoordinates.ReservationSourceLinkRecordType,
            seeded.SourceLink.Id,
            OwnerReceiptContractVersion: 1,
            Guid.NewGuid(),
            new string('c', 64),
            seeded.SourceLink.Version + 1,
            Now.AddMinutes(30));

    internal static IngestionAnonymisationFingerprintOptions
        FingerprintOptions() =>
        new()
        {
            ActiveKeyVersion = 7,
            Keys =
            {
                [7] = Convert.ToBase64String(
                    Enumerable.Range(1, 32)
                        .Select(value => (byte)value)
                        .ToArray())
            }
        };

    internal sealed record SeededGraph(
        ReservationSourceLink SourceLink,
        ObservationReceipt Receipt,
        ReservationDispatch Dispatch,
        Guid PropertyId);

    internal sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    internal sealed class TestClock(DateTimeOffset utcNow)
        : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    internal sealed class TestIds : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    internal sealed class TestRawPayloadStore : IRawPayloadStore
    {
        private readonly Dictionary<
            (Guid FileId, Guid ConnectionId),
            RawPayloadRead> payloads = [];

        public void Add(
            Guid fileId,
            Guid connectionId,
            byte[] content) =>
            this.payloads.Add(
                (fileId, connectionId),
                new(
                    "application/json",
                    content,
                    AdapterPayloadHash.ComputeSha256(content)));

        public Task StoreAsync(
            RawPayloadWrite write,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.payloads.GetValueOrDefault(
                    (payloadId, connectionId)));

        public Task<bool> DeleteAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.payloads.Remove((payloadId, connectionId)));
    }
}
