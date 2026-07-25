namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsRestoreCheckpointTests
{
    private static readonly string FirstEntrySha256 = new('a', 64);
    private static readonly string FirstStorageMacSha256 = new('b', 64);

    [Fact]
    public void Checkpoint_advances_and_confirms_only_from_its_exact_cursor()
    {
        DataRightsRestoreCheckpoint checkpoint =
            DataRightsRestoreCheckpoint.Create(
                Guid.NewGuid(),
                "tenant-a").Value;

        Assert.Equal(DataRightsRestoreCursor.Genesis, checkpoint.Cursor);
        Assert.True(checkpoint.Advance(
            expectedVersion: 0,
            DataRightsRestoreCursor.Genesis,
            new DataRightsRestoreCursor(1, FirstEntrySha256),
            FirstStorageMacSha256).IsSuccess);
        Assert.Equal(1, checkpoint.Version);
        Assert.Equal(1, checkpoint.TenantSequence);

        DateTimeOffset reconciledAtUtc =
            new(2026, 7, 25, 15, 0, 0, TimeSpan.Zero);
        Assert.True(checkpoint.Confirm(
            expectedVersion: 1,
            integrityKeyVersion: 3,
            checkpointMacSha256: new string('c', 64),
            scopeSnapshotSha256: new string('d', 64),
            reconciledAtUtc).IsSuccess);

        Assert.Equal(2, checkpoint.Version);
        Assert.Equal(3, checkpoint.IntegrityKeyVersion);
        Assert.Equal(reconciledAtUtc, checkpoint.LastReconciledAtUtc);
    }

    [Fact]
    public void Checkpoint_rejects_stale_versions_and_non_advancing_cursors()
    {
        DataRightsRestoreCheckpoint checkpoint =
            DataRightsRestoreCheckpoint.Create(
                Guid.NewGuid(),
                "tenant-a").Value;

        Assert.Equal(
            "DataRights.RestoreCheckpointConflict",
            checkpoint.Advance(
                expectedVersion: 1,
                DataRightsRestoreCursor.Genesis,
                new DataRightsRestoreCursor(1, FirstEntrySha256),
                FirstStorageMacSha256).Error.Code);
        Assert.Equal(
            "DataRights.RestoreCheckpointInvalid",
            checkpoint.Advance(
                expectedVersion: 0,
                DataRightsRestoreCursor.Genesis,
                DataRightsRestoreCursor.Genesis,
                FirstStorageMacSha256).Error.Code);
        Assert.Equal(0, checkpoint.Version);
        Assert.Equal(DataRightsRestoreCursor.Genesis, checkpoint.Cursor);
    }
}
