namespace BunkFy.Modules.DataRights.Application.Models;

using BunkFy.Modules.DataRights.Domain.Models;

internal sealed record DataRightsRestoreCheckpointState(
    long Version,
    DataRightsRestoreCursor Cursor,
    string StorageMacSha256);

internal sealed record DataRightsRestoreOwnerProofBinding(
    Guid LedgerEntryId,
    long TenantSequence,
    string LedgerEntrySha256,
    Guid OwnerReceiptId,
    string OwnerReceiptSha256,
    long ResultingRecordVersion,
    long TombstoneRevision,
    DateTimeOffset ReplayedAtUtc);
