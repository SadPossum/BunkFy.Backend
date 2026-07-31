namespace BunkFy.Modules.Ingestion.Tests;

using System.Security.Cryptography;
using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionDataRightsContributorTests
{
    private const string ScopeId = "tenant-a";
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Staff_scope_is_not_owned_or_exported_by_ingestion()
    {
        TestScopeContext scope = new(ScopeId);
        await using IngestionDbContext dbContext = CreateDbContext(scope);
        IngestionDataRightsDiscoveryContributor discovery = new(dbContext, scope);
        IngestionDataRightsExportContributor export = new(
            dbContext,
            new IngestionDataRightsEvidenceGraphLoader(dbContext),
            new TestRawPayloadStore(),
            scope);
        CollectingSink sink = new();

        DataRightsSubjectDiscoveryResult discovered = await discovery.DiscoverAsync(
            new DataRightsSubjectDiscoveryRequest(
                ScopeId,
                DataRightsCaseType.StaffRights,
                PropertyId: null,
                new DataRightsSubjectLookup(
                    RecordId: null,
                    Email: null,
                    Phone: null,
                    Name: null,
                    DateOfBirth: null,
                    AccountSubjectId: "account-subject-123"),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);
        DataRightsSubjectExportResult exported = await export.ExportAsync(
            new DataRightsSubjectExportRequest(
                ScopeId,
                DataRightsCaseType.StaffRights,
                PropertyId: null,
                new DataRightsSubjectCoordinate(
                    IngestionDataRightsDiscoveryContributor.Owner,
                    IngestionDataRightsDiscoveryContributor.SourceLinkRecordType,
                    Guid.NewGuid(),
                    1)),
            sink,
            CancellationToken.None);

        Assert.Equal([DataRightsCaseType.GuestRights], discovery.SupportedCaseTypes);
        Assert.Equal([DataRightsCaseType.GuestRights], export.SupportedCaseTypes);
        Assert.Equal(DataRightsSubjectDiscoveryStatus.ScopeUnavailable, discovered.Status);
        Assert.Equal(DataRightsSubjectExportStatus.ScopeUnavailable, exported.Status);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task Discovery_requires_an_exact_reservation_and_returns_ordered_source_links()
    {
        TestScopeContext scope = new(ScopeId);
        await using IngestionDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        SeedKnownProperty(dbContext, propertyId);

        ReservationSourceLink second = CreateLinkedSource(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            propertyId,
            reservationId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hostelworld",
            "hw-42");
        ReservationSourceLink first = CreateLinkedSource(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            propertyId,
            reservationId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "booking-com",
            "bk-42");
        dbContext.ReservationSourceLinks.AddRange(second, first);
        await dbContext.SaveChangesAsync();

        IngestionDataRightsDiscoveryContributor contributor = new(dbContext, scope);
        DataRightsSubjectDiscoveryResult discovered = await contributor.DiscoverAsync(
            new DataRightsSubjectDiscoveryRequest(
                ScopeId,
                DataRightsCaseType.GuestRights,
                propertyId,
                new DataRightsSubjectLookup(
                    reservationId,
                    Email: null,
                    Phone: null,
                    Name: null,
                    DateOfBirth: null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectDiscoveryStatus.Succeeded, discovered.Status);
        Assert.Equal([first.Id, second.Id], discovered.Candidates
            .Select(candidate => candidate.Coordinate.RecordId));
        Assert.All(discovered.Candidates, candidate =>
        {
            Assert.Equal(IngestionDataRightsDiscoveryContributor.Owner, candidate.Coordinate.OwnerKey);
            Assert.Equal(
                IngestionDataRightsDiscoveryContributor.SourceLinkRecordType,
                candidate.Coordinate.RecordType);
            Assert.Null(candidate.EmailHint);
            Assert.Null(candidate.PhoneHint);
            Assert.DoesNotContain("42", candidate.DisplayName, StringComparison.Ordinal);
        });

        DataRightsSubjectDiscoveryResult unsupported = await contributor.DiscoverAsync(
            new DataRightsSubjectDiscoveryRequest(
                ScopeId,
                DataRightsCaseType.GuestRights,
                propertyId,
                new DataRightsSubjectLookup(
                    RecordId: null,
                    Email: "guest@example.test",
                    Phone: null,
                    Name: null,
                    DateOfBirth: null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);
        Assert.Equal(DataRightsSubjectDiscoveryStatus.ScopeUnavailable, unsupported.Status);

        DataRightsSubjectSelectionValidation stale = await contributor.ValidateSelectionAsync(
            new DataRightsSubjectSelectionRequest(
                ScopeId,
                DataRightsCaseType.GuestRights,
                propertyId,
                new DataRightsSubjectCoordinate(
                    IngestionDataRightsDiscoveryContributor.Owner,
                    IngestionDataRightsDiscoveryContributor.SourceLinkRecordType,
                    first.Id,
                    first.Version - 1)),
            CancellationToken.None);
        Assert.Equal(DataRightsSubjectSelectionValidationStatus.Stale, stale.Status);
    }

    [Fact]
    public async Task Export_streams_only_the_selected_graph_and_reconstructable_raw_chunks()
    {
        TestScopeContext scope = new(ScopeId);
        await using IngestionDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        SeedKnownProperty(dbContext, propertyId);

        byte[] payload = Enumerable.Range(0, 25_123)
            .Select(index => (byte)(index % 251))
            .ToArray();
        string hash = Sha256(payload);
        ObservationReceipt receipt = CreateReceipt(
            receiptId,
            propertyId,
            connectionId,
            "provider-42",
            hash,
            Guid.NewGuid());
        ReservationSourceLink sourceLink = CreateLinkedSource(
            Guid.NewGuid(),
            propertyId,
            reservationId,
            connectionId,
            receiptId,
            "booking-com",
            "provider-42",
            hash);
        ObservationReceipt unrelated = CreateReceipt(
            Guid.NewGuid(),
            propertyId,
            connectionId,
            "provider-other",
            Sha256([1, 2, 3]),
            Guid.NewGuid());
        dbContext.ObservationReceipts.AddRange(receipt, unrelated);
        dbContext.ReservationSourceLinks.Add(sourceLink);
        await dbContext.SaveChangesAsync();

        TestRawPayloadStore rawStore = new();
        rawStore.Add(
            receipt.RawPayloadFileId,
            connectionId,
            new RawPayloadRead("application/json", payload, hash));
        CollectingSink sink = new();
        IngestionDataRightsExportContributor contributor = new(
            dbContext,
            new IngestionDataRightsEvidenceGraphLoader(dbContext),
            rawStore,
            scope);

        DataRightsSubjectExportResult result = await contributor.ExportAsync(
            RequestFor(propertyId, sourceLink),
            sink,
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectExportStatus.Succeeded, result.Status);
        Assert.Equal(result.RecordCount, sink.Records.Count);
        Assert.DoesNotContain(sink.Records, record => record.RecordId == unrelated.Id);
        Assert.Equal(2, sink.Records.Count(record =>
            record.RecordType != IngestionDataRightsExportContributor.RawPayloadChunkRecordType));

        DataRightsExportRecord[] chunks = sink.Records
            .Where(record =>
                record.RecordType ==
                IngestionDataRightsExportContributor.RawPayloadChunkRecordType)
            .OrderBy(record => Field(record, "ingestion.operations.raw-payload-chunk-index")
                .GetInt32())
            .ToArray();
        Assert.Equal(3, chunks.Length);
        byte[] reconstructed = chunks
            .SelectMany(record => Field(record, "ingestion.raw-source.content")
                .GetBytesFromBase64())
            .ToArray();
        Assert.Equal(payload, reconstructed);
        Assert.All(chunks, record =>
        {
            Assert.Equal(
                chunks.Length,
                Field(record, "ingestion.operations.raw-payload-chunk-count").GetInt32());
            Assert.Equal(
                payload.Length,
                Field(record, "ingestion.operations.raw-payload-total-bytes").GetInt32());
        });
        Assert.Equal(7, contributor.Descriptor.CatalogVersion);
        Assert.Equal(
            IngestionDataRightsExportSchema.ExportSchemaVersion,
            contributor.Descriptor.ExportSchemaVersion);
    }

    [Fact]
    public async Task Export_omits_purged_content_and_fails_when_available_content_is_missing()
    {
        TestScopeContext scope = new(ScopeId);
        await using IngestionDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        SeedKnownProperty(dbContext, propertyId);

        byte[] payload = [1, 2, 3, 4];
        string hash = Sha256(payload);
        ObservationReceipt receipt = CreateReceipt(
            receiptId,
            propertyId,
            connectionId,
            "provider-42",
            hash,
            Guid.NewGuid());
        ReservationSourceLink sourceLink = CreateLinkedSource(
            Guid.NewGuid(),
            propertyId,
            reservationId,
            connectionId,
            receiptId,
            "booking-com",
            "provider-42",
            hash);
        dbContext.ObservationReceipts.Add(receipt);
        dbContext.ReservationSourceLinks.Add(sourceLink);
        await dbContext.SaveChangesAsync();

        IngestionDataRightsExportContributor contributor = new(
            dbContext,
            new IngestionDataRightsEvidenceGraphLoader(dbContext),
            new TestRawPayloadStore(),
            scope);
        CollectingSink missingSink = new();
        DataRightsSubjectExportResult missing = await contributor.ExportAsync(
            RequestFor(propertyId, sourceLink),
            missingSink,
            CancellationToken.None);
        Assert.Equal(DataRightsSubjectExportStatus.ScopeUnavailable, missing.Status);
        Assert.NotEmpty(missingSink.Records);

        Guid claimId = Guid.NewGuid();
        DateTimeOffset purgeAt = Now.AddDays(31);
        Assert.True(receipt.BeginRawPayloadPurge(claimId, purgeAt, purgeAt.AddHours(-1)).IsSuccess);
        Assert.True(receipt.CompleteRawPayloadPurge(claimId, purgeAt.AddMinutes(1)).IsSuccess);
        await dbContext.SaveChangesAsync();

        CollectingSink purgedSink = new();
        DataRightsSubjectExportResult purged = await contributor.ExportAsync(
            RequestFor(propertyId, sourceLink),
            purgedSink,
            CancellationToken.None);
        Assert.Equal(DataRightsSubjectExportStatus.Succeeded, purged.Status);
        Assert.DoesNotContain(purgedSink.Records, record =>
            record.RecordType ==
            IngestionDataRightsExportContributor.RawPayloadChunkRecordType);
    }

    [Fact]
    public async Task Eligibility_loads_complete_maximum_graph_and_current_restrictions()
    {
        TestScopeContext scope = new(ScopeId);
        await using IngestionDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        Guid firstReceiptId = Guid.NewGuid();

        IngestionPropertyProjection property =
            IngestionPropertyProjection.Create(propertyId, ScopeId);
        property.ApplySnapshot(
            "Hostel",
            "HST",
            isActive: true,
            PropertyProcessingStatus.Enabled,
            PolicyBinding(),
            sourceVersion: 3);
        property.AdvanceRetentionFence();
        property.AdvanceRetentionFence();
        AdapterConnection connection = AdapterConnection.Create(
            connectionId,
            ScopeId,
            propertyId,
            "fake.http",
            AdapterExecutionMode.Push,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://data-rights",
            secretReference: null,
            Now).Value;
        ReservationSourceLink sourceLink = CreateLinkedSource(
            Guid.NewGuid(),
            propertyId,
            reservationId,
            connectionId,
            firstReceiptId,
            "booking-com",
            "provider-42");
        ObservationReceipt[] receipts = Enumerable.Range(
                0,
                IngestionDataRightsEvidenceGraphLoader.MaximumGraphRecords - 1)
            .Select(index => CreateReceipt(
                index == 0 ? firstReceiptId : Guid.NewGuid(),
                propertyId,
                connectionId,
                "provider-42",
                new string('a', 64),
                Guid.NewGuid()))
            .ToArray();
        LegalHold hold = LegalHold.Place(
            Guid.NewGuid(),
            ScopeId,
            propertyId,
            "Preserve provider evidence",
            "user:privacy-operator",
            Now).Value;
        dbContext.PropertyProjections.Add(property);
        dbContext.AdapterConnections.Add(connection);
        dbContext.ReservationSourceLinks.Add(sourceLink);
        dbContext.ObservationReceipts.AddRange(receipts);
        dbContext.LegalHolds.Add(hold);
        await dbContext.SaveChangesAsync();

        IngestionAnonymisationEligibilityRepository repository = new(
            dbContext,
            new IngestionDataRightsEvidenceGraphLoader(dbContext));
        IngestionAnonymisationEligibilityLoadResult loaded =
            await repository.LoadAsync(
                propertyId,
                sourceLink.Id,
                CancellationToken.None);

        Assert.Equal(
            IngestionAnonymisationEligibilityLoadStatus.Found,
            loaded.Status);
        IngestionAnonymisationEligibilitySnapshot snapshot =
            Assert.IsType<IngestionAnonymisationEligibilitySnapshot>(
                loaded.Snapshot);
        Assert.Equal(
            IngestionDataRightsEvidenceGraphLoader.MaximumGraphRecords,
            snapshot.GraphRecordCount);
        Assert.Equal(receipts.Length, snapshot.Receipts.Count);
        Assert.Equal(1, snapshot.ActiveLegalHoldCount);
        Assert.Equal(2, snapshot.Property?.RetentionFenceVersion);
        Assert.Equal(
            PropertyProcessingStatus.Enabled,
            snapshot.Property?.ProcessingStatus);
        Assert.Equal(
            connection.Version,
            snapshot.Connection.Version);
    }

    [Fact]
    public async Task Evidence_graph_fails_closed_when_reprocessing_lineage_is_incomplete()
    {
        TestScopeContext scope = new(ScopeId);
        await using IngestionDbContext dbContext = CreateDbContext(scope);
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        ObservationReceipt sourceReceipt = CreateReceipt(
            Guid.NewGuid(),
            propertyId,
            connectionId,
            "provider-42",
            new string('a', 64),
            Guid.NewGuid());
        ObservationReceipt descendant = CreateReceipt(
            Guid.NewGuid(),
            propertyId,
            connectionId,
            "provider-42",
            new string('b', 64),
            Guid.NewGuid(),
            sourceReceipt.Id,
            Guid.NewGuid());
        ReservationSourceLink sourceLink = CreateLinkedSource(
            Guid.NewGuid(),
            propertyId,
            reservationId,
            connectionId,
            sourceReceipt.Id,
            "booking-com",
            "provider-42");
        dbContext.ObservationReceipts.AddRange(sourceReceipt, descendant);
        dbContext.ReservationSourceLinks.Add(sourceLink);
        await dbContext.SaveChangesAsync();

        IngestionDataRightsEvidenceGraphLoadResult loaded =
            await new IngestionDataRightsEvidenceGraphLoader(dbContext)
                .LoadAsync(sourceLink, CancellationToken.None);

        Assert.Equal(
            IngestionDataRightsEvidenceGraphLoadStatus.Incomplete,
            loaded.Status);
        Assert.Null(loaded.Graph);
    }

    private static DataRightsSubjectExportRequest RequestFor(
        Guid propertyId,
        ReservationSourceLink sourceLink) =>
        new(
            ScopeId,
            DataRightsCaseType.GuestRights,
            propertyId,
            new DataRightsSubjectCoordinate(
                IngestionDataRightsDiscoveryContributor.Owner,
                IngestionDataRightsDiscoveryContributor.SourceLinkRecordType,
                sourceLink.Id,
                sourceLink.Version));

    private static IngestionDbContext CreateDbContext(IScopeContext scope)
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase($"ingestion-data-rights-{Guid.NewGuid():N}")
                .Options;
        return new IngestionDbContext(options, scope);
    }

    private static void SeedKnownProperty(
        IngestionDbContext dbContext,
        Guid propertyId)
    {
        IngestionPropertyProjection property =
            IngestionPropertyProjection.Create(propertyId, ScopeId);
        property.ApplyTopology("Hostel", "HST", isActive: true, sourceVersion: 1);
        dbContext.PropertyProjections.Add(property);
    }

    private static ReservationSourceLink CreateLinkedSource(
        Guid sourceLinkId,
        Guid propertyId,
        Guid reservationId,
        Guid connectionId,
        Guid receiptId,
        string sourceSystem,
        string sourceReference,
        string? contentHash = null)
    {
        string hash = contentHash ?? new string('a', 64);
        ReservationSourceLink sourceLink = ReservationSourceLink.Create(
            sourceLinkId,
            ScopeId,
            propertyId,
            connectionId,
            sourceSystem,
            sourceReference,
            Now).Value;
        Assert.True(sourceLink.Observe(
            receiptId,
            "revision-1",
            sourceSequence: 1,
            Now,
            hash,
            Now).IsSuccess);
        Guid operationId = Guid.NewGuid();
        Assert.True(sourceLink.BeginDispatch(operationId, Now).IsSuccess);
        Assert.True(sourceLink.CompleteDispatch(
            operationId,
            receiptId,
            "revision-1",
            sourceSequence: 1,
            "{\"guest\":\"Maya\"}",
            reservationId,
            detailsRevision: 1,
            keepActive: false,
            applied: true,
            cancellationPending: false,
            cancelled: false,
            Now).IsSuccess);
        return sourceLink;
    }

    private static ObservationReceipt CreateReceipt(
        Guid receiptId,
        Guid propertyId,
        Guid connectionId,
        string externalId,
        string contentHash,
        Guid payloadFileId,
        Guid? sourceReceiptId = null,
        Guid? reprocessingAttemptId = null)
    {
        ObservationCountryPolicyEvidence evidence =
            ObservationCountryPolicyEvidence.Create(
                "GB",
                "policy",
                1,
                "eu",
                "eu-only",
                "retention",
                1,
                new string('b', 64),
                "reservation-import",
                "adapter-ingress",
                "property-policy",
                Now.AddDays(-1),
                Now.AddDays(30),
                Now).Value;
        ObservationReceipt receipt = ObservationReceipt.Create(
            receiptId,
            ScopeId,
            propertyId,
            connectionId,
            runId: null,
            Guid.NewGuid(),
            "reservation",
            externalId,
            "revision-1",
            $"reservation:{externalId}:revision-1",
            contentHash,
            evidence,
            payloadFileId,
            Now.AddDays(30),
            Now,
            Now,
            Now,
            sourceReceiptId,
            reprocessingAttemptId,
            parserType: sourceReceiptId.HasValue ? "reservation-mail" : null,
            parserVersion: sourceReceiptId.HasValue ? 1 : null,
            parserOutputIndex: sourceReceiptId.HasValue ? 0 : null).Value;
        Assert.True(receipt.MarkProcessed(Now.AddMinutes(1)).IsSuccess);
        return receipt;
    }

    private static System.Text.Json.JsonElement Field(
        DataRightsExportRecord record,
        string fieldId) =>
        Assert.Single(record.Fields, field => field.FieldId == fieldId).Value;

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static PropertyGovernancePolicyBinding PolicyBinding() =>
        new(
            "GB",
            "gb-hostel",
            1,
            "eu-west-2",
            "uk-no-transfer",
            "ingestion-operational",
            1,
            new string('b', 64),
            Now.AddDays(-1),
            Now.AddDays(30),
            Now.AddHours(-1),
            []);

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }

    private sealed class CollectingSink : IDataRightsExportSink
    {
        public List<DataRightsExportRecord> Records { get; } = [];

        public ValueTask WriteAsync(
            DataRightsExportRecord record,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestRawPayloadStore : IRawPayloadStore
    {
        private readonly Dictionary<(Guid PayloadId, Guid ConnectionId), RawPayloadRead>
            payloads = [];

        public void Add(
            Guid payloadId,
            Guid connectionId,
            RawPayloadRead payload) =>
            this.payloads.Add((payloadId, connectionId), payload);

        public Task StoreAsync(
            RawPayloadWrite write,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(ScopeId, scopeId);
            this.payloads.TryGetValue((payloadId, connectionId), out RawPayloadRead? payload);
            return Task.FromResult(payload);
        }

        public Task<bool> DeleteAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
