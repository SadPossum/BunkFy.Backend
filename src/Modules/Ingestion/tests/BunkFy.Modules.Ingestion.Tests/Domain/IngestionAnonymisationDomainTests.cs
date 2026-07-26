namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAnonymisationDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Tombstone_preserves_proof_and_has_one_way_lifecycle()
    {
        Guid sourceLinkId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        Guid ownerReceiptId = Guid.NewGuid();
        Guid ledgerEntryId = Guid.NewGuid();
        Result<IngestionAnonymisationTombstone> created =
            IngestionAnonymisationTombstone.BeginRestore(
                "tenant-a",
                sourceLinkId,
                propertyId,
                connectionId,
                selectedSourceLinkVersion: 4,
                resultingSourceLinkVersion: 5,
                ownerReceiptContractVersion: 1,
                ownerReceiptId,
                new string('a', 64),
                Now.AddMinutes(-10),
                ledgerEntryId,
                tenantSequence: 9,
                new string('b', 64),
                graphRecordCount: 3,
                fingerprintCount: 2,
                rawPayloadCount: 1,
                Now);

        Assert.True(created.IsSuccess);
        IngestionAnonymisationTombstone tombstone = created.Value;
        Assert.True(tombstone.MatchesRestore(
            propertyId,
            1,
            ownerReceiptId,
            new string('A', 64),
            Now.AddMinutes(-10),
            ledgerEntryId,
            9,
            new string('B', 64)));
        Assert.Equal(
            IngestionAnonymisationTombstoneState.Reducing,
            tombstone.State);

        DateTimeOffset completedAt = Now.AddMinutes(1);
        Assert.True(tombstone.CompleteRestore(completedAt).IsSuccess);
        Assert.Equal(
            IngestionAnonymisationTombstoneState.Completed,
            tombstone.State);
        Assert.Equal(2, tombstone.Revision);
        Assert.True(tombstone.CompleteRestore(completedAt).IsSuccess);
        Assert.True(
            tombstone.CompleteRestore(completedAt.AddSeconds(1)).IsFailure);
    }

    [Fact]
    public void Plan_encodes_reduction_and_raw_completion_versions()
    {
        Result<IngestionAnonymisationRecordPlanEntry> rawReceipt =
            IngestionAnonymisationRecordPlanEntry.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                IngestionAnonymisationRecordKind.ObservationReceipt,
                Guid.NewGuid(),
                selectedVersion: 7,
                reductionVersion: 8,
                resultingVersion: 9,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now);
        Result<IngestionAnonymisationRecordPlanEntry> attempt =
            IngestionAnonymisationRecordPlanEntry.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                IngestionAnonymisationRecordKind
                    .ObservationReprocessingAttempt,
                Guid.NewGuid(),
                selectedVersion: 3,
                reductionVersion: 3,
                resultingVersion: 3,
                rawPayloadFileId: null,
                rawPayloadConnectionId: null,
                Now);

        Assert.True(rawReceipt.IsSuccess);
        Assert.True(rawReceipt.Value.RequiresRawPayloadDeletion);
        Assert.True(rawReceipt.Value.MatchesReductionVersion(8));
        Assert.True(rawReceipt.Value.MatchesVersion(9));
        Assert.True(attempt.IsSuccess);
        Assert.False(attempt.Value.RequiresRawPayloadDeletion);
        Assert.True(attempt.Value.MatchesVersion(3));

        Assert.True(IngestionAnonymisationRecordPlanEntry.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            IngestionAnonymisationRecordKind.ObservationReceipt,
            Guid.NewGuid(),
            selectedVersion: 7,
            reductionVersion: 8,
            resultingVersion: 8,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now).IsFailure);
    }

    [Fact]
    public void Fingerprint_normalizes_and_rejects_invalid_digests()
    {
        Result<IngestionAnonymisationFingerprint> created =
            IngestionAnonymisationFingerprint.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                IngestionAnonymisationFingerprintPurpose.SourceLink,
                keyVersion: 3,
                new string('A', 64),
                Now);

        Assert.True(created.IsSuccess);
        Assert.Equal(new string('a', 64), created.Value.Sha256);
        Assert.True(created.Value.Matches(
            IngestionAnonymisationFingerprintPurpose.SourceLink,
            3,
            new string('A', 64)));
        Assert.True(IngestionAnonymisationFingerprint.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            IngestionAnonymisationFingerprintPurpose.SourceLink,
            keyVersion: 3,
            "plaintext-provider-id",
            Now).IsFailure);
    }
}
