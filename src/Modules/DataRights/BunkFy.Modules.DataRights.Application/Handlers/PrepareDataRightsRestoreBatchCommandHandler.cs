namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class PrepareDataRightsRestoreBatchCommandHandler(
    IDataRightsRestoreCheckpointRepository checkpoints,
    IDataRightsProcessingLedgerRepository ledgers,
    IScopeContext scopeContext)
    : ICommandHandler<PrepareDataRightsRestoreBatchCommand, Unit>
{
    private const int MaximumBatchSize = 100;

    public async Task<Result<Unit>> HandleAsync(
        PrepareDataRightsRestoreBatchCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ExpectedCursor is null ||
            !command.ExpectedCursor.HasValidShape() ||
            command.Deltas is null ||
            command.Deltas.Count is < 1 or > MaximumBatchSize)
        {
            return Result.Failure<Unit>(
                DataRightsApplicationErrors.RestoreEvidenceInvalid);
        }

        DataRightsRestoreCheckpoint? checkpoint =
            await checkpoints.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!MatchesExpected(checkpoint, command))
        {
            return Result.Failure<Unit>(
                DataRightsApplicationErrors.RestoreCheckpointConflict);
        }

        long expectedSequence = command.ExpectedCursor.TenantSequence;
        string expectedPreviousSha256 = command.ExpectedCursor.EntrySha256;
        foreach (DataRightsLedgerDelta delta in command.Deltas)
        {
            if (delta is null ||
                !delta.HasValidProof() ||
                delta.Ledger.TenantSequence != ++expectedSequence ||
                !string.Equals(
                    delta.Ledger.ScopeId,
                    scopeContext.ScopeId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    delta.Ledger.PreviousEntrySha256,
                    expectedPreviousSha256,
                    StringComparison.Ordinal))
            {
                return Result.Failure<Unit>(
                    DataRightsApplicationErrors.RestoreEvidenceInvalid);
            }

            Result<DataRightsProcessingLedgerEntry> restored =
                DataRightsProcessingLedgerEntry.Restore(delta.Ledger);
            if (restored.IsFailure)
            {
                return Result.Failure<Unit>(
                    DataRightsApplicationErrors.RestoreEvidenceInvalid);
            }

            DataRightsProcessingLedgerEntry? existing =
                await ledgers.GetBySequenceAsync(
                    restored.Value.TenantSequence,
                    cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                await ledgers.AddAsync(
                    restored.Value,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (existing.Freeze() != delta.Ledger)
            {
                return Result.Failure<Unit>(
                    DataRightsApplicationErrors.RestoreStorageConflict);
            }

            expectedPreviousSha256 = delta.Ledger.EntrySha256;
        }

        return Result.Success(Unit.Value);
    }

    private static bool MatchesExpected(
        DataRightsRestoreCheckpoint? checkpoint,
        PrepareDataRightsRestoreBatchCommand command) =>
        checkpoint is null
            ? command.ExpectedCheckpointVersion == 0 &&
              command.ExpectedCursor == DataRightsRestoreCursor.Genesis
            : checkpoint.Version == command.ExpectedCheckpointVersion &&
              checkpoint.Cursor == command.ExpectedCursor;
}
