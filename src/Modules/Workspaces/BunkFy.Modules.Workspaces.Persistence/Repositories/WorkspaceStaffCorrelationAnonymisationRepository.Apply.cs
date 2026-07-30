namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;

internal sealed partial class
    WorkspaceStaffCorrelationAnonymisationRepository
{
    public async Task<Result<
        WorkspaceStaffCorrelationAnonymisationReceipt>> ApplyAsync(
            WorkspaceStaffCorrelationAnonymisationApplyRequest
                request,
            CancellationToken cancellationToken)
    {
        WorkspaceStaffCorrelationAnonymisationSnapshot snapshot =
            request.Snapshot;
        if (!HasValidApplyRequest(request) ||
            snapshot.Status !=
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .Eligible ||
            snapshot.AnchorProcessId is null ||
            snapshot.AnchorProcessVersion is null ||
            snapshot.StaffMemberId is null ||
            snapshot.SelectedStaffVersion is null ||
            string.IsNullOrWhiteSpace(snapshot.SubjectId) ||
            string.IsNullOrWhiteSpace(snapshot.StateSha256))
        {
            return Result.Failure<
                WorkspaceStaffCorrelationAnonymisationReceipt>(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .RequestInvalid);
        }

        WorkspaceStaffCorrelationAnonymisationReceipt? existing =
            await this.FindReceiptByIdempotencyKeyAsync(
                request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Matches(
                    request.IdempotencyKey,
                    request.CaseId,
                    request.ApprovalRevision,
                    request.OperationRevision,
                    snapshot.AnchorProcessId.Value,
                    snapshot.AnchorProcessVersion.Value,
                    request.ApprovalEvidenceSha256,
                    request.ActorId)
                ? Result.Success(existing)
                : Result.Failure<
                    WorkspaceStaffCorrelationAnonymisationReceipt>(
                    WorkspaceStaffCorrelationAnonymisationApplicationErrors
                        .IdempotencyConflict);
        }

        bool proofExists = await this.dbContext
            .StaffCorrelationAnonymisationTombstones
            .AsNoTracking()
            .AnyAsync(
                tombstone =>
                    tombstone.Id ==
                        snapshot.AnchorProcessId.Value,
                cancellationToken).ConfigureAwait(false);
        if (proofExists)
        {
            return Result.Failure<
                WorkspaceStaffCorrelationAnonymisationReceipt>(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .ProofUnavailable);
        }

        string pseudonym = CreatePseudonym(request.ReceiptId);
        MutationCounts counts = await this.ScrubAsync(
            request.TenantId,
            snapshot.SubjectId,
            pseudonym,
            request.CompletedAtUtc,
            cancellationToken).ConfigureAwait(false);
        if (!counts.Matches(snapshot))
        {
            return Result.Failure<
                WorkspaceStaffCorrelationAnonymisationReceipt>(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .StateChanged);
        }

        WorkspaceStaffCorrelationAnonymisationSnapshot resulting =
            await this.ReadAnonymisedAsync(
                request.TenantId,
                snapshot.AnchorProcessId.Value,
                snapshot.AnchorProcessVersion.Value + 1,
                request.ReceiptId,
                cancellationToken).ConfigureAwait(false);
        if (!HasExpectedResult(snapshot, resulting))
        {
            return Result.Failure<
                WorkspaceStaffCorrelationAnonymisationReceipt>(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .ProofUnavailable);
        }

        Result<WorkspaceStaffCorrelationAnonymisationReceipt>
            created =
            WorkspaceStaffCorrelationAnonymisationReceipt.Create(
                request.ReceiptId,
                request.TenantId,
                request.IdempotencyKey,
                request.CaseId,
                request.ApprovalRevision,
                request.OperationRevision,
                snapshot.AnchorProcessId.Value,
                snapshot.StaffMemberId.Value,
                snapshot.SelectedStaffVersion.Value,
                snapshot.AnchorProcessVersion.Value,
                resulting.AnchorProcessVersion!.Value,
                counts.Onboarding,
                counts.AccessProcesses,
                counts.AccessPlans,
                request.ApprovalEvidenceSha256,
                request.StateBindingSha256,
                resulting.StateSha256!,
                request.ActorId,
                request.CompletedAtUtc);
        if (created.IsFailure)
        {
            return created;
        }

        Result<WorkspaceStaffCorrelationAnonymisationTombstone>
            tombstone =
            WorkspaceStaffCorrelationAnonymisationTombstone.Create(
                created.Value);
        if (tombstone.IsFailure)
        {
            return Result.Failure<
                WorkspaceStaffCorrelationAnonymisationReceipt>(
                tombstone.Error);
        }

        this.dbContext.StaffCorrelationAnonymisationReceipts.Add(
            created.Value);
        this.dbContext.StaffCorrelationAnonymisationTombstones.Add(
            tombstone.Value);
        return created;
    }
}
