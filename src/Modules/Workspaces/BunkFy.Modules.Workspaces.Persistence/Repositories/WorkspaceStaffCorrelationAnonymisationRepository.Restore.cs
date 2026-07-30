namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;

internal sealed partial class
    WorkspaceStaffCorrelationAnonymisationRepository
{
    public async Task<Result<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>>
        RestoreAsync(
            WorkspaceStaffCorrelationAnonymisationRestoreRequest
                request,
            CancellationToken cancellationToken)
    {
        if (!HasValidRestoreRequest(request))
        {
            return RestoreConflict();
        }

        WorkspaceStaffCorrelationAnonymisationRestoreReceipt?
            existing = await this.GetRestoreReceiptAsync(
                request.LedgerEntryId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ReplayRestoreAsync(
                existing,
                request,
                cancellationToken).ConfigureAwait(false);
        }

        WorkspaceStaffCorrelationAnonymisationTombstone?
            tombstone = await this.GetTombstoneAsync(
                request.AnchorProcessId,
                cancellationToken).ConfigureAwait(false);
        WorkspaceStaffCorrelationAnonymisationTombstone?
            newTombstone = null;
        WorkspaceStaffCorrelationAnonymisationSnapshot resulting;
        if (tombstone is null)
        {
            bool orphanReceipt = await this.dbContext
                .StaffCorrelationAnonymisationReceipts
                .AsNoTracking()
                .AnyAsync(
                    receipt =>
                        receipt.AnchorProcessId ==
                            request.AnchorProcessId,
                    cancellationToken).ConfigureAwait(false);
            if (orphanReceipt)
            {
                return RestoreConflict();
            }

            WorkspaceStaffCorrelationAnonymisationSnapshot original =
                await this.ReadAsync(
                    request.TenantId,
                    request.AnchorProcessId,
                    request.ResultingAnchorVersion - 1,
                    cancellationToken).ConfigureAwait(false);
            if (original is not
                {
                    Status:
                        WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                            .Eligible,
                    StaffMemberId: not null,
                    SelectedStaffVersion: not null,
                    SubjectId: not null
                })
            {
                return RestoreConflict();
            }

            MutationCounts counts = await this.ScrubAsync(
                request.TenantId,
                original.SubjectId,
                CreatePseudonym(request.OwnerReceiptId),
                request.OriginallyCompletedAtUtc,
                cancellationToken).ConfigureAwait(false);
            if (!counts.Matches(original))
            {
                return RestoreConflict();
            }

            resulting = await this.ReadAnonymisedAsync(
                request.TenantId,
                request.AnchorProcessId,
                request.ResultingAnchorVersion,
                request.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
            if (!HasExpectedResult(original, resulting))
            {
                return RestoreConflict();
            }

            Result<WorkspaceStaffCorrelationAnonymisationTombstone>
                restored =
                WorkspaceStaffCorrelationAnonymisationTombstone
                    .Restore(
                        request.TenantId,
                        request.AnchorProcessId,
                        resulting.StaffMemberId!.Value,
                        resulting.SelectedStaffVersion!.Value,
                        request.ResultingAnchorVersion - 1,
                        request.ResultingAnchorVersion,
                        request.OwnerReceiptContractVersion,
                        request.OwnerReceiptId,
                        request.OwnerReceiptSha256,
                        resulting.StateSha256!,
                        request.OriginallyCompletedAtUtc,
                        request.LedgerEntryId,
                        request.ReplayedAtUtc);
            if (restored.IsFailure)
            {
                return Result.Failure<
                    WorkspaceStaffCorrelationAnonymisationRestoreReceipt>(
                    restored.Error);
            }

            tombstone = restored.Value;
            newTombstone = tombstone;
        }
        else
        {
            if (!tombstone.HasValidProof() ||
                tombstone.ResultingAnchorVersion !=
                    request.ResultingAnchorVersion ||
                tombstone.OwnerReceiptContractVersion !=
                    request.OwnerReceiptContractVersion ||
                tombstone.OwnerReceiptId !=
                    request.OwnerReceiptId ||
                !string.Equals(
                    tombstone.OwnerReceiptSha256,
                    NormalizeSha256(
                        request.OwnerReceiptSha256),
                    StringComparison.Ordinal) ||
                tombstone.CompletedAtUtc !=
                    request.OriginallyCompletedAtUtc
                        .ToUniversalTime())
            {
                return RestoreConflict();
            }

            resulting = await this.ReadAnonymisedAsync(
                request.TenantId,
                request.AnchorProcessId,
                request.ResultingAnchorVersion,
                request.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
            if (!MatchesTombstone(resulting, tombstone))
            {
                return RestoreConflict();
            }

            Result attached = tombstone.AttachRestoreProof(
                request.LedgerEntryId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingAnchorVersion,
                request.OriginallyCompletedAtUtc,
                request.ReplayedAtUtc);
            if (attached.IsFailure)
            {
                return Result.Failure<
                    WorkspaceStaffCorrelationAnonymisationRestoreReceipt>(
                    attached.Error);
            }
        }

        Result<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
            receipt =
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt.Create(
                request.TenantId,
                request.LedgerEntryId,
                request.TenantSequence,
                request.LedgerEntrySha256,
                request.AnchorProcessId,
                tombstone.StaffMemberId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingAnchorVersion,
                resulting.StateSha256!,
                resulting.OnboardingRecordCount,
                resulting.AccessProcessRecordCount,
                resulting.AccessPlanRecordCount,
                request.OriginallyCompletedAtUtc,
                tombstone.Revision,
                tombstone.LastReplayedAtUtc!.Value);
        if (receipt.IsFailure)
        {
            return receipt;
        }

        if (newTombstone is not null)
        {
            this.dbContext
                .StaffCorrelationAnonymisationTombstones.Add(
                    newTombstone);
        }

        this.dbContext
            .StaffCorrelationAnonymisationRestoreReceipts.Add(
                receipt.Value);
        return receipt;
    }

    private async Task<Result<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>>
        ReplayRestoreAsync(
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt
                receipt,
            WorkspaceStaffCorrelationAnonymisationRestoreRequest
                request,
            CancellationToken cancellationToken)
    {
        if (!receipt.Matches(
                request.TenantId,
                request.LedgerEntryId,
                request.TenantSequence,
                request.LedgerEntrySha256,
                request.AnchorProcessId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingAnchorVersion,
                request.OriginallyCompletedAtUtc))
        {
            return RestoreConflict();
        }

        WorkspaceStaffCorrelationAnonymisationTombstone?
            tombstone = await this.GetTombstoneAsync(
                request.AnchorProcessId,
                cancellationToken).ConfigureAwait(false);
        if (tombstone is null ||
            tombstone.Revision != receipt.TombstoneRevision ||
            tombstone.LastReplayedAtUtc != receipt.ReplayedAtUtc ||
            !tombstone.MatchesRestore(
                request.LedgerEntryId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingAnchorVersion,
                request.OriginallyCompletedAtUtc))
        {
            return RestoreConflict();
        }

        WorkspaceStaffCorrelationAnonymisationReceipt?
            originalReceipt = await this.dbContext
                .StaffCorrelationAnonymisationReceipts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.Id ==
                            request.OwnerReceiptId,
                    cancellationToken).ConfigureAwait(false);
        if (originalReceipt is not null &&
            !tombstone.Matches(originalReceipt))
        {
            return RestoreConflict();
        }

        WorkspaceStaffCorrelationAnonymisationSnapshot resulting =
            await this.ReadAnonymisedAsync(
                request.TenantId,
                request.AnchorProcessId,
                request.ResultingAnchorVersion,
                request.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
        if (!MatchesTombstone(resulting, tombstone) ||
            !string.Equals(
                resulting.StateSha256,
                receipt.ResultingStateSha256,
                StringComparison.Ordinal) ||
            resulting.OnboardingRecordCount !=
                receipt.OnboardingRecordsScrubbed ||
            resulting.AccessProcessRecordCount !=
                receipt.AccessProcessRecordsScrubbed ||
            resulting.AccessPlanRecordCount !=
                receipt.AccessPlanRecordsScrubbed)
        {
            return RestoreConflict();
        }

        return Result.Success(receipt);
    }
}
