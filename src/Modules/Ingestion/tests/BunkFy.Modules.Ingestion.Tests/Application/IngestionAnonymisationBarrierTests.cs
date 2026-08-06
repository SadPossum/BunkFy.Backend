namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.DataRights;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAnonymisationBarrierTests
{
    [Fact]
    public async Task Barrier_locks_deterministic_source_before_rejecting()
    {
        const string TenantId = "tenant-a";
        const string ExternalId = "booking-42";
        Guid connectionId = Guid.NewGuid();
        RecordingOperationLock operationLock = new();
        BlockingRepository repository = new(operationLock);
        IngestionAnonymisationBarrier barrier = new(
            operationLock,
            new FingerprintService(),
            repository);

        Result<Guid> result = await barrier.AcquireAndCheckAsync(
            TenantId,
            connectionId,
            "reservation.v1",
            ExternalId,
            CancellationToken.None);

        Guid expectedSourceLinkId =
            ReservationOperationIdentity.CreateSourceLinkId(
                TenantId,
                connectionId,
                ExternalId);
        Assert.Equal(
            IngestionApplicationErrors.AnonymisationBarrierActive,
            result.Error);
        Assert.Equal(expectedSourceLinkId, operationLock.SourceLinkId);
        Assert.Equal(TenantId, operationLock.TenantId);
        Assert.True(repository.ObservedLock);
    }

    [Fact]
    public async Task Check_under_existing_lock_does_not_reacquire_it()
    {
        const string TenantId = "tenant-a";
        const string ExternalId = "booking-43";
        Guid connectionId = Guid.NewGuid();
        RecordingOperationLock operationLock = new();
        AllowingRepository repository = new();
        IngestionAnonymisationBarrier barrier = new(
            operationLock,
            new FingerprintService(),
            repository);

        Result<Guid> result = await barrier.CheckUnderLockAsync(
            TenantId,
            connectionId,
            "reservation.v1",
            ExternalId,
            CancellationToken.None);

        Guid expectedSourceLinkId =
            ReservationOperationIdentity.CreateSourceLinkId(
                TenantId,
                connectionId,
                ExternalId);
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedSourceLinkId, result.Value);
        Assert.Equal(expectedSourceLinkId, repository.SourceLinkId);
        Assert.False(operationLock.Acquired);
    }

    private sealed class RecordingOperationLock
        : IIngestionSourceOperationLock
    {
        public string? TenantId { get; private set; }
        public Guid SourceLinkId { get; private set; }
        public bool Acquired { get; private set; }

        public Task AcquireAsync(
            string tenantId,
            Guid sourceLinkId,
            CancellationToken cancellationToken)
        {
            this.TenantId = tenantId;
            this.SourceLinkId = sourceLinkId;
            this.Acquired = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FingerprintService
        : IIngestionAnonymisationFingerprintService
    {
        private static readonly IngestionAnonymisationFingerprintValue Value =
            new(
                IngestionAnonymisationFingerprintPurpose.ObservationReceipt,
                7,
                new string(
                    'a',
                    IngestionAnonymisationFingerprint.Sha256Length));

        public Result<IngestionAnonymisationFingerprintValue> CreateActive(
            string tenantId,
            IngestionAnonymisationFingerprintPurpose purpose,
            Guid connectionId,
            string recordType,
            string externalId) =>
            Result.Success(Value);

        public Result<
            IReadOnlyList<IngestionAnonymisationFingerprintValue>>
            CreateCandidates(
                string tenantId,
                IngestionAnonymisationFingerprintPurpose purpose,
                Guid connectionId,
                string recordType,
                string externalId) =>
            Result.Success<
                IReadOnlyList<IngestionAnonymisationFingerprintValue>>(
                [Value]);
    }

    private sealed class BlockingRepository(
        RecordingOperationLock operationLock)
        : IIngestionAnonymisationBarrierRepository
    {
        public bool ObservedLock { get; private set; }

        public Task<bool> IsBlockedAsync(
            Guid sourceLinkId,
            IReadOnlyCollection<
                IngestionAnonymisationFingerprintValue> fingerprints,
            CancellationToken cancellationToken)
        {
            this.ObservedLock =
                operationLock.Acquired &&
                operationLock.SourceLinkId == sourceLinkId;
            return Task.FromResult(true);
        }
    }

    private sealed class AllowingRepository
        : IIngestionAnonymisationBarrierRepository
    {
        public Guid SourceLinkId { get; private set; }

        public Task<bool> IsBlockedAsync(
            Guid sourceLinkId,
            IReadOnlyCollection<
                IngestionAnonymisationFingerprintValue> fingerprints,
            CancellationToken cancellationToken)
        {
            this.SourceLinkId = sourceLinkId;
            return Task.FromResult(false);
        }
    }
}
