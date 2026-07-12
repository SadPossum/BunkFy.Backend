namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Text;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GetObservationRawPayloadQueryHandlerTests
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("{\"reservation\":42}");
    private static readonly string ContentHash = AdapterPayloadHash.ComputeSha256(Content);

    [Fact]
    public async Task Property_scoped_receipt_returns_hash_verified_payload()
    {
        Guid propertyId = Guid.NewGuid();
        ObservationReceiptDto receipt = CreateReceipt(propertyId, ContentHash);
        FakeRawPayloadStore payloads = new(new RawPayloadRead("application/json", Content, ContentHash));
        GetObservationRawPayloadQueryHandler handler = new(
            new FakeOperationsReader(receipt), payloads, new TestScope());

        Result<ObservationRawPayload> result = await handler.HandleAsync(
            new GetObservationRawPayloadQuery(propertyId, receipt.ReceiptId),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(Content, result.Value.Content.ToArray());
        Assert.Equal("application/json", result.Value.ContentType);
        Assert.Equal(receipt.RawPayloadFileId, payloads.PayloadId);
        Assert.Equal(receipt.ConnectionId, payloads.ConnectionId);
        Assert.Equal("tenant-a", payloads.ScopeId);
    }

    [Fact]
    public async Task Cross_property_receipt_is_not_resolved_and_payload_store_is_not_called()
    {
        ObservationReceiptDto receipt = CreateReceipt(Guid.NewGuid(), ContentHash);
        FakeRawPayloadStore payloads = new(new RawPayloadRead("application/json", Content, ContentHash));
        GetObservationRawPayloadQueryHandler handler = new(
            new FakeOperationsReader(receipt), payloads, new TestScope());

        Result<ObservationRawPayload> result = await handler.HandleAsync(
            new GetObservationRawPayloadQuery(Guid.NewGuid(), receipt.ReceiptId),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.ReceiptNotFound, result.Error);
        Assert.Equal(0, payloads.ReadCount);
    }

    [Fact]
    public async Task Stored_payload_hash_must_match_the_receipt()
    {
        Guid propertyId = Guid.NewGuid();
        ObservationReceiptDto receipt = CreateReceipt(propertyId, ContentHash);
        FakeRawPayloadStore payloads = new(new RawPayloadRead(
            "application/json",
            "different"u8.ToArray(),
            AdapterPayloadHash.ComputeSha256("different"u8)));
        GetObservationRawPayloadQueryHandler handler = new(
            new FakeOperationsReader(receipt), payloads, new TestScope());

        Result<ObservationRawPayload> result = await handler.HandleAsync(
            new GetObservationRawPayloadQuery(propertyId, receipt.ReceiptId),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.RawPayloadInvalid, result.Error);
    }

    [Fact]
    public async Task Purged_payload_is_reported_without_reading_object_storage()
    {
        Guid propertyId = Guid.NewGuid();
        ObservationReceiptDto receipt = CreateReceipt(propertyId, ContentHash) with
        {
            RawPayloadStatus = RawPayloadRetentionStatus.Purged,
            RawPayloadPurgedAtUtc = DateTimeOffset.UtcNow
        };
        FakeRawPayloadStore payloads = new(new RawPayloadRead("application/json", Content, ContentHash));
        GetObservationRawPayloadQueryHandler handler = new(
            new FakeOperationsReader(receipt), payloads, new TestScope());

        Result<ObservationRawPayload> result = await handler.HandleAsync(
            new GetObservationRawPayloadQuery(propertyId, receipt.ReceiptId),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.RawPayloadUnavailable, result.Error);
        Assert.Equal(0, payloads.ReadCount);
    }

    private static ObservationReceiptDto CreateReceipt(Guid propertyId, string contentHash) => new(
        Guid.NewGuid(),
        propertyId,
        Guid.NewGuid(),
        RunId: null,
        Guid.NewGuid(),
        "reservation.v1",
        "booking-42",
        "1",
        contentHash,
        Guid.NewGuid(),
        RawPayloadRetentionStatus.Available,
        DateTimeOffset.UtcNow.AddDays(30),
        RawPayloadPurgedAtUtc: null,
        ActiveReprocessingAttemptId: null,
        ReprocessingReservationExpiresAtUtc: null,
        SourceReceiptId: null,
        ReprocessingAttemptId: null,
        ParserType: null,
        ParserVersion: null,
        ParserOutputIndex: null,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        ObservationReceiptStatus.Pending,
        RejectionReason: null,
        DateTimeOffset.UtcNow,
        ProcessedAtUtc: null);

    private sealed class FakeOperationsReader(ObservationReceiptDto receipt) : IIngestionOperationsReader
    {
        public Task<ObservationReceiptDto?> GetReceiptAsync(
            Guid propertyId,
            Guid receiptId,
            CancellationToken cancellationToken) => Task.FromResult<ObservationReceiptDto?>(
            receipt.PropertyId == propertyId && receipt.ReceiptId == receiptId ? receipt : null);

        public Task<AdapterConnectionDto?> GetConnectionAsync(
            Guid propertyId, Guid connectionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AdapterConnectionHealthDto?> GetConnectionHealthAsync(
            Guid propertyId,
            Guid connectionId,
            DateTimeOffset evaluatedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AdapterConnectionListResponse> ListConnectionsAsync(
            Guid propertyId, AdapterConnectionStatus? status, PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IngestionRunDto?> GetRunAsync(
            Guid propertyId, Guid runId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IngestionRunListResponse> ListRunsAsync(
            Guid propertyId, Guid? connectionId, IngestionRunStatus? status, PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ObservationReceiptListResponse> ListReceiptsAsync(
            Guid propertyId, Guid? connectionId, Guid? runId, ObservationReceiptStatus? status,
            PageRequest pageRequest, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ObservationReprocessingAttemptDto?> GetReprocessingAttemptAsync(
            Guid propertyId, Guid attemptId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<ObservationReprocessingOutputDto>> ListReprocessingOutputsAsync(
            Guid attemptId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ObservationReprocessingAttemptListResponse> ListReprocessingAttemptsAsync(
            Guid propertyId,
            Guid? sourceReceiptId,
            ObservationReprocessingStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRawPayloadStore(RawPayloadRead? payload) : IRawPayloadStore
    {
        public int ReadCount { get; private set; }
        public Guid PayloadId { get; private set; }
        public string? ScopeId { get; private set; }
        public Guid ConnectionId { get; private set; }

        public Task StoreAsync(RawPayloadWrite write, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            this.ReadCount++;
            this.PayloadId = payloadId;
            this.ScopeId = scopeId;
            this.ConnectionId = connectionId;
            return Task.FromResult(payload);
        }

        public Task<bool> DeleteAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestScope : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
