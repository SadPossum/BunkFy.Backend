namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class ConfirmDataRightsRestoreCheckpointCommandHandler(
    IDataRightsRestoreCheckpointRepository checkpoints,
    IScopeContext scopeContext)
    : ICommandHandler<
        ConfirmDataRightsRestoreCheckpointCommand,
        DataRightsRestoreCheckpointState>
{
    public async Task<Result<DataRightsRestoreCheckpointState>> HandleAsync(
        ConfirmDataRightsRestoreCheckpointCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TrustedCheckpoint is null ||
            command.TrustedCheckpoint.ContractVersion !=
                DataRightsLedgerDeltaCheckpoint.CurrentContractVersion ||
            command.TrustedCheckpoint.Cursor is null)
        {
            return Result.Failure<DataRightsRestoreCheckpointState>(
                DataRightsApplicationErrors.RestoreEvidenceInvalid);
        }

        DataRightsRestoreCheckpoint? checkpoint =
            await checkpoints.GetAsync(cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            string scopeId = scopeContext.ScopeId ?? string.Empty;
            Result<DataRightsRestoreCheckpoint> created =
                DataRightsRestoreCheckpoint.Create(
                    DataRightsRestoreCheckpointIdentity.Create(
                        scopeId),
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

        DataRightsLedgerDeltaCursor target =
            command.TrustedCheckpoint.Cursor;
        if (checkpoint.TenantSequence != target.TenantSequence ||
            !string.Equals(
                checkpoint.EntrySha256,
                target.EntrySha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                checkpoint.StorageMacSha256,
                target.StorageMacSha256,
                StringComparison.Ordinal))
        {
            return Result.Failure<DataRightsRestoreCheckpointState>(
                DataRightsApplicationErrors.RestoreStorageConflict);
        }

        Result confirmed = checkpoint.Confirm(
            command.ExpectedCheckpointVersion,
            command.TrustedCheckpoint.IntegrityKeyVersion,
            command.TrustedCheckpoint.CheckpointMacSha256,
            command.ScopeSnapshotSha256,
            command.ReconciledAtUtc);
        return confirmed.IsFailure
            ? Result.Failure<DataRightsRestoreCheckpointState>(confirmed.Error)
            : Result.Success(new DataRightsRestoreCheckpointState(
                checkpoint.Version,
                checkpoint.Cursor,
                checkpoint.StorageMacSha256));
    }
}
