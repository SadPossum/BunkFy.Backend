namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class AdvanceDataRightsRestoreCheckpointCommandHandler(
    IDataRightsRestoreCheckpointRepository checkpoints,
    IDataRightsProcessingLedgerRepository ledgers,
    IScopeContext scopeContext)
    : ICommandHandler<
        AdvanceDataRightsRestoreCheckpointCommand,
        DataRightsRestoreCheckpointState>
{
    private const int MaximumBatchSize = 100;

    public async Task<Result<DataRightsRestoreCheckpointState>> HandleAsync(
        AdvanceDataRightsRestoreCheckpointCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ExpectedCursor is null ||
            !command.ExpectedCursor.HasValidShape() ||
            command.NextCursor is null ||
            command.OwnerProofs is null ||
            command.NextCursor.TenantSequence <=
                command.ExpectedCursor.TenantSequence ||
            !IsSha256(command.NextCursor.EntrySha256) ||
            !IsSha256(command.NextCursor.StorageMacSha256))
        {
            return Result.Failure<DataRightsRestoreCheckpointState>(
                DataRightsApplicationErrors.RestoreEvidenceInvalid);
        }

        long batchSize =
            command.NextCursor.TenantSequence -
            command.ExpectedCursor.TenantSequence;
        if (
            batchSize is < 1 or > MaximumBatchSize ||
            command.OwnerProofs.Count != batchSize)
        {
            return Result.Failure<DataRightsRestoreCheckpointState>(
                DataRightsApplicationErrors.RestoreEvidenceInvalid);
        }

        DataRightsRestoreCheckpoint? checkpoint =
            await checkpoints.GetAsync(cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            if (command.ExpectedCheckpointVersion != 0 ||
                command.ExpectedCursor != DataRightsRestoreCursor.Genesis)
            {
                return Result.Failure<DataRightsRestoreCheckpointState>(
                    DataRightsApplicationErrors.RestoreCheckpointConflict);
            }
        }
        else if (
            checkpoint.Version != command.ExpectedCheckpointVersion ||
            checkpoint.Cursor != command.ExpectedCursor)
        {
            return Result.Failure<DataRightsRestoreCheckpointState>(
                DataRightsApplicationErrors.RestoreCheckpointConflict);
        }

        string previousEntrySha256 =
            command.ExpectedCursor.EntrySha256;
        for (int index = 0; index < command.OwnerProofs.Count; index++)
        {
            long sequence =
                command.ExpectedCursor.TenantSequence + index + 1;
            DataRightsProcessingLedgerEntry? ledger =
                await ledgers.GetBySequenceAsync(
                    sequence,
                    cancellationToken).ConfigureAwait(false);
            if (ledger is null ||
                !string.Equals(
                    ledger.PreviousEntrySha256,
                    previousEntrySha256,
                    StringComparison.Ordinal) ||
                !Matches(ledger, command.OwnerProofs[index]))
            {
                return Result.Failure<DataRightsRestoreCheckpointState>(
                    DataRightsApplicationErrors.RestoreOwnerProofInvalid);
            }

            previousEntrySha256 = ledger.EntrySha256;
        }

        if (!string.Equals(
                previousEntrySha256,
                command.NextCursor.EntrySha256,
                StringComparison.Ordinal))
        {
            return Result.Failure<DataRightsRestoreCheckpointState>(
                DataRightsApplicationErrors.RestoreOwnerProofInvalid);
        }

        if (checkpoint is null)
        {
            string scopeId = scopeContext.ScopeId ?? string.Empty;
            Result<DataRightsRestoreCheckpoint> created =
                DataRightsRestoreCheckpoint.Create(
                    DataRightsRestoreCheckpointIdentity.Create(scopeId),
                    scopeId);
            if (created.IsFailure)
            {
                return Result.Failure<DataRightsRestoreCheckpointState>(
                    created.Error);
            }

            checkpoint = created.Value;
            await checkpoints.AddAsync(
                checkpoint,
                cancellationToken).ConfigureAwait(false);
        }

        Result advanced = checkpoint.Advance(
            command.ExpectedCheckpointVersion,
            command.ExpectedCursor,
            new DataRightsRestoreCursor(
                command.NextCursor.TenantSequence,
                command.NextCursor.EntrySha256),
            command.NextCursor.StorageMacSha256);
        return advanced.IsFailure
            ? Result.Failure<DataRightsRestoreCheckpointState>(advanced.Error)
            : Result.Success(new DataRightsRestoreCheckpointState(
                checkpoint.Version,
                checkpoint.Cursor,
                checkpoint.StorageMacSha256));
    }

    private static bool Matches(
        DataRightsProcessingLedgerEntry ledger,
        DataRightsRestoreOwnerProofBinding proof) =>
        proof.LedgerEntryId == ledger.Id &&
        proof.TenantSequence == ledger.TenantSequence &&
        string.Equals(
            proof.LedgerEntrySha256,
            ledger.EntrySha256,
            StringComparison.Ordinal) &&
        proof.OwnerReceiptId == ledger.OwnerReceiptId &&
        string.Equals(
            proof.OwnerReceiptSha256,
            ledger.OwnerReceiptSha256,
            StringComparison.Ordinal) &&
        proof.ResultingRecordVersion > 0 &&
        proof.TombstoneRevision > 0 &&
        proof.ReplayedAtUtc != default &&
        proof.ReplayedAtUtc.Offset == TimeSpan.Zero;

    private static bool IsSha256(string? value) =>
        value is { Length: DataRightsProcessingLedgerEntry.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
