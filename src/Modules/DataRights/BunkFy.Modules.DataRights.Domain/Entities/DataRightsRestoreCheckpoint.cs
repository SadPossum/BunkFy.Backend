namespace BunkFy.Modules.DataRights.Domain.Entities;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class DataRightsRestoreCheckpoint : ScopedEntity<Guid>
{
    private DataRightsRestoreCheckpoint() { }

    private DataRightsRestoreCheckpoint(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public long TenantSequence { get; private set; }
    public string EntrySha256 { get; private set; } = string.Empty;
    public string StorageMacSha256 { get; private set; } = string.Empty;
    public int IntegrityKeyVersion { get; private set; }
    public string CheckpointMacSha256 { get; private set; } = string.Empty;
    public string ScopeSnapshotSha256 { get; private set; } = string.Empty;
    public DateTimeOffset LastReconciledAtUtc { get; private set; }
    public long Version { get; private set; }

    public static Result<DataRightsRestoreCheckpoint> Create(
        Guid id,
        string scopeId)
    {
        if (id == Guid.Empty ||
            !TenantIds.TryNormalize(scopeId, out string? normalizedScopeId))
        {
            return Result.Failure<DataRightsRestoreCheckpoint>(
                DataRightsDomainErrors.RestoreCheckpointInvalid);
        }

        return Result.Success(new DataRightsRestoreCheckpoint(id, normalizedScopeId)
        {
            EntrySha256 = DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            StorageMacSha256 = DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            CheckpointMacSha256 =
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            ScopeSnapshotSha256 =
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            LastReconciledAtUtc = DateTimeOffset.UnixEpoch
        });
    }

    public DataRightsRestoreCursor Cursor =>
        new(this.TenantSequence, this.EntrySha256);

    public Result Advance(
        long expectedVersion,
        DataRightsRestoreCursor expected,
        DataRightsRestoreCursor next,
        string nextStorageMacSha256)
    {
        if (expectedVersion != this.Version ||
            expected is null ||
            next is null ||
            this.TenantSequence != expected.TenantSequence ||
            !string.Equals(
                this.EntrySha256,
                expected.EntrySha256,
                StringComparison.Ordinal))
        {
            return Result.Failure(DataRightsDomainErrors.RestoreCheckpointConflict);
        }

        if (!expected.HasValidShape() ||
            !next.HasValidShape() ||
            next.TenantSequence <= expected.TenantSequence ||
            !IsSha256(nextStorageMacSha256))
        {
            return Result.Failure(DataRightsDomainErrors.RestoreCheckpointInvalid);
        }

        this.TenantSequence = next.TenantSequence;
        this.EntrySha256 = next.EntrySha256;
        this.StorageMacSha256 = nextStorageMacSha256;
        this.Version++;
        return Result.Success();
    }

    public Result Confirm(
        long expectedVersion,
        int integrityKeyVersion,
        string checkpointMacSha256,
        string scopeSnapshotSha256,
        DateTimeOffset reconciledAtUtc)
    {
        DateTimeOffset timestamp = reconciledAtUtc.ToUniversalTime();
        if (expectedVersion != this.Version)
        {
            return Result.Failure(DataRightsDomainErrors.RestoreCheckpointConflict);
        }

        if (integrityKeyVersion <= 0 ||
            !IsSha256(checkpointMacSha256) ||
            !IsSha256(scopeSnapshotSha256) ||
            timestamp == default)
        {
            return Result.Failure(DataRightsDomainErrors.RestoreCheckpointInvalid);
        }

        this.IntegrityKeyVersion = integrityKeyVersion;
        this.CheckpointMacSha256 = checkpointMacSha256;
        this.ScopeSnapshotSha256 = scopeSnapshotSha256;
        this.LastReconciledAtUtc = timestamp;
        this.Version++;
        return Result.Success();
    }

    private static bool IsSha256(string? value) =>
        value is not null &&
        value.Length == DataRightsProcessingLedgerEntry.Sha256Length &&
        value.All(Uri.IsHexDigit);
}
