namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAnonymisationBarrierRepositoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Tombstone_and_exact_fingerprint_block_resurrection()
    {
        await using IngestionDbContext dbContext = CreateDbContext();
        IngestionAnonymisationTombstone tombstone =
            CreateTombstone();
        const int KeyVersion = 7;
        string digest = new(
            'c',
            IngestionAnonymisationFingerprint.Sha256Length);
        IngestionAnonymisationFingerprint fingerprint =
            IngestionAnonymisationFingerprint.Create(
                Guid.NewGuid(),
                "tenant-a",
                tombstone.Id,
                IngestionAnonymisationFingerprintPurpose.ObservationReceipt,
                KeyVersion,
                digest,
                Now)
            .Value;
        dbContext.AddRange(tombstone, fingerprint);
        await dbContext.SaveChangesAsync();
        IngestionAnonymisationBarrierRepository repository = new(dbContext);

        Assert.True(await repository.IsBlockedAsync(
            tombstone.Id,
            [],
            CancellationToken.None));
        Assert.True(await repository.IsBlockedAsync(
            Guid.NewGuid(),
            [
                new(
                    IngestionAnonymisationFingerprintPurpose
                        .ObservationReceipt,
                    KeyVersion,
                    digest)
            ],
            CancellationToken.None));
        Assert.False(await repository.IsBlockedAsync(
            Guid.NewGuid(),
            [
                new(
                    IngestionAnonymisationFingerprintPurpose
                        .ObservationReceipt,
                    KeyVersion + 1,
                    digest)
            ],
            CancellationToken.None));
    }

    private static IngestionAnonymisationTombstone CreateTombstone() =>
        IngestionAnonymisationTombstone.BeginRestore(
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            selectedSourceLinkVersion: 1,
            resultingSourceLinkVersion: 2,
            ownerReceiptContractVersion: 1,
            Guid.NewGuid(),
            new string('a', IngestionAnonymisationTombstone.Sha256Length),
            Now.AddDays(-1),
            Guid.NewGuid(),
            tenantSequence: 1,
            new string('b', IngestionAnonymisationTombstone.Sha256Length),
            graphRecordCount: 1,
            fingerprintCount: 1,
            rawPayloadCount: 0,
            Now)
        .Value;

    private static IngestionDbContext CreateDbContext()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(
                    $"ingestion-anonymisation-barrier-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
