namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Domain.Retention;
using Microsoft.EntityFrameworkCore;

internal sealed partial class IngestionTenantTerminationContributor
{
    private async Task<long> ExportProposalsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ChangeProposal proposal in dbContext.ChangeProposals
            .AsNoTracking()
            .Where(item => item.ScopeId == tenantId)
            .OrderBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            IngestionChangeProposalTenantExport record = new(
                proposal.ScopeId,
                proposal.PropertyId,
                proposal.ConnectionId,
                proposal.Id,
                new IngestionChangeProposalStateTenantExport(
                    proposal.ReceiptId,
                    proposal.ReservationId,
                    proposal.SourcePayloadFileId,
                    proposal.BaseReservationDetailsRevision,
                    proposal.ReasonCode,
                    CreateLargeTextReference("proposal.diff", proposal.Diff),
                    proposal.State,
                    proposal.DecisionActor,
                    proposal.DecisionReason,
                    proposal.ProductOperationId,
                    proposal.Version,
                    proposal.CreatedAtUtc,
                    proposal.DecidedAtUtc,
                    proposal.CompletedAtUtc,
                    proposal.SensitiveDataRetainUntilUtc,
                    proposal.SensitiveDataRedactedAtUtc,
                    proposal.AnonymisedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata.ChangeProposalRecordType,
                proposal.Id,
                proposal.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportSourceLinksAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationSourceLink sourceLink in
            dbContext.ReservationSourceLinks
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionReservationSourceLinkTenantExport record = new(
                sourceLink.ScopeId,
                sourceLink.PropertyId,
                sourceLink.ConnectionId,
                sourceLink.Id,
                new IngestionReservationSourceLinkStateTenantExport(
                    sourceLink.SourceSystem,
                    sourceLink.SourceReference,
                    sourceLink.ReservationId,
                    sourceLink.State,
                    sourceLink.LastObservedReceiptId,
                    sourceLink.LastObservedSourceRevision,
                    sourceLink.LastObservedSourceSequence,
                    sourceLink.LastObservedSourceUpdatedAtUtc,
                    sourceLink.LastObservedContentHash,
                    sourceLink.LastAppliedReceiptId,
                    sourceLink.LastAppliedSourceRevision,
                    sourceLink.LastAppliedSourceSequence,
                    sourceLink.LastAppliedReservationDetailsRevision,
                    CreateLargeTextReference(
                        "source-link.operational-baseline",
                        sourceLink.LastAppliedOperationalBaseline),
                    sourceLink.LastProductOperationId,
                    sourceLink.ActiveProductOperationId,
                    sourceLink.DeferredReceiptId,
                    sourceLink.Version,
                    sourceLink.CreatedAtUtc,
                    sourceLink.UpdatedAtUtc,
                    sourceLink.AnonymisedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .ReservationSourceLinkRecordType,
                sourceLink.Id,
                sourceLink.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportDispatchesAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ReservationDispatch dispatch in
            dbContext.ReservationDispatches
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionReservationDispatchTenantExport record = new(
                dispatch.ScopeId,
                dispatch.PropertyId,
                dispatch.ConnectionId,
                dispatch.Id,
                new IngestionReservationDispatchStateTenantExport(
                    dispatch.SourceLinkId,
                    dispatch.TriggerKind,
                    dispatch.TriggerId,
                    dispatch.ReceiptId,
                    dispatch.ReservationId,
                    dispatch.Kind,
                    dispatch.SourceRevision,
                    dispatch.SourceSequence,
                    CreateLargeTextReference(
                        "dispatch.normalized-snapshot",
                        dispatch.NormalizedSnapshot),
                    dispatch.ExpectedDetailsRevision,
                    dispatch.State,
                    dispatch.ResultDetailsRevision,
                    dispatch.ResultReservationVersion,
                    dispatch.ErrorCode,
                    dispatch.Version,
                    dispatch.CreatedAtUtc,
                    dispatch.CompletedAtUtc,
                    dispatch.SensitiveDataRetainUntilUtc,
                    dispatch.SensitiveDataRedactedAtUtc,
                    dispatch.AnonymisedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .ReservationDispatchRecordType,
                dispatch.Id,
                dispatch.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportLegalHoldsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (LegalHold hold in dbContext.LegalHolds
            .AsNoTracking()
            .Where(item => item.ScopeId == tenantId)
            .OrderBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            IngestionLegalHoldTenantExport record = new(
                hold.ScopeId,
                hold.PropertyId,
                hold.Id,
                new IngestionLegalHoldStateTenantExport(
                    hold.Reason,
                    hold.State,
                    hold.PlacedBy,
                    hold.PlacedAtUtc,
                    hold.ReleasedBy,
                    hold.ReleaseReason,
                    hold.ReleasedAtUtc,
                    hold.Version));
            await WriteAsync(
                IngestionTenantTerminationMetadata.LegalHoldRecordType,
                hold.Id,
                hold.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportRetentionExecutionsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (IngestionRetentionExecution execution in
            dbContext.RetentionExecutions
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionRetentionExecutionTenantExport record = new(
                execution.ScopeId,
                execution.Id,
                new IngestionRetentionExecutionStateTenantExport(
                    execution.DataClassKey,
                    execution.ExecutionPolicyVersion,
                    execution.Attempt,
                    execution.State,
                    execution.StartedAtUtc,
                    execution.DeadlineUtc,
                    execution.CompletedAtUtc,
                    execution.AffectedCount,
                    execution.RemainingCount,
                    execution.OutcomeCode,
                    execution.HoldReviewDueAtUtc,
                    execution.Version));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .RetentionExecutionRecordType,
                execution.Id,
                execution.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportAnonymisationReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (IngestionAnonymisationReceipt receipt in
            dbContext.AnonymisationReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionAnonymisationReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.Id,
                new IngestionAnonymisationReceiptProofTenantExport(
                    receipt.ContractVersion,
                    receipt.WorkItemId,
                    receipt.IdempotencyKey,
                    receipt.CaseId,
                    receipt.ApprovalRevision,
                    receipt.OperationRevision,
                    receipt.SourceLinkId,
                    receipt.SelectedSourceLinkVersion,
                    receipt.ResultingSourceLinkVersion,
                    receipt.Disposition,
                    receipt.Reason,
                    receipt.GraphRecordCount,
                    receipt.FingerprintCount,
                    receipt.RawPayloadCount,
                    receipt.ApprovalEvidenceSha256,
                    receipt.PolicyEvidenceSha256,
                    receipt.OperationFenceSha256,
                    receipt.ActorId,
                    receipt.CompletedAtUtc,
                    receipt.CanonicalSha256));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .AnonymisationReceiptRecordType,
                receipt.Id,
                recordVersion: 1,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportAnonymisationTombstonesAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (IngestionAnonymisationTombstone tombstone in
            dbContext.AnonymisationTombstones
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionAnonymisationTombstoneTenantExport record = new(
                tombstone.ScopeId,
                tombstone.PropertyId,
                tombstone.ConnectionId,
                tombstone.Id,
                new IngestionAnonymisationTombstoneProofTenantExport(
                    tombstone.ContractVersion,
                    tombstone.Revision,
                    tombstone.State,
                    tombstone.Origin,
                    tombstone.SelectedSourceLinkVersion,
                    tombstone.ResultingSourceLinkVersion,
                    tombstone.WorkItemId,
                    tombstone.IdempotencyKey,
                    tombstone.CaseId,
                    tombstone.ApprovalRevision,
                    tombstone.OperationRevision,
                    tombstone.ApprovalEvidenceSha256,
                    tombstone.PolicyEvidenceSha256,
                    tombstone.OperationFenceSha256,
                    tombstone.ActorId,
                    tombstone.ExecutionStartedAtUtc,
                    tombstone.OwnerReceiptContractVersion,
                    tombstone.OwnerReceiptId,
                    tombstone.OwnerReceiptSha256,
                    tombstone.OriginallyCompletedAtUtc,
                    tombstone.LedgerEntryId,
                    tombstone.TenantSequence,
                    tombstone.LedgerEntrySha256,
                    tombstone.GraphRecordCount,
                    tombstone.FingerprintCount,
                    tombstone.RawPayloadCount,
                    tombstone.ReplayStartedAtUtc,
                    tombstone.LastReplayedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .AnonymisationTombstoneRecordType,
                tombstone.Id,
                tombstone.Revision,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportLargeTextChunksAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ChangeProposal proposal in dbContext.ChangeProposals
            .AsNoTracking()
            .Where(item => item.ScopeId == tenantId && item.Diff != null)
            .OrderBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            count = await WriteLargeTextChunksAsync(
                proposal.ScopeId,
                IngestionTenantTerminationMetadata.ChangeProposalRecordType,
                proposal.Id,
                proposal.Version,
                "proposal.diff",
                proposal.Diff!,
                sink,
                count,
                cancellationToken).ConfigureAwait(false);
        }

        await foreach (ReservationSourceLink sourceLink in
            dbContext.ReservationSourceLinks
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId &&
                    item.LastAppliedOperationalBaseline != null)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            count = await WriteLargeTextChunksAsync(
                sourceLink.ScopeId,
                IngestionTenantTerminationMetadata
                    .ReservationSourceLinkRecordType,
                sourceLink.Id,
                sourceLink.Version,
                "source-link.operational-baseline",
                sourceLink.LastAppliedOperationalBaseline!,
                sink,
                count,
                cancellationToken).ConfigureAwait(false);
        }

        await foreach (ReservationDispatch dispatch in
            dbContext.ReservationDispatches
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId &&
                    item.NormalizedSnapshot != null)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            count = await WriteLargeTextChunksAsync(
                dispatch.ScopeId,
                IngestionTenantTerminationMetadata
                    .ReservationDispatchRecordType,
                dispatch.Id,
                dispatch.Version,
                "dispatch.normalized-snapshot",
                dispatch.NormalizedSnapshot!,
                sink,
                count,
                cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    private static IngestionLargeTextReferenceTenantExport?
        CreateLargeTextReference(string contentKind, string? value)
    {
        if (value is null)
        {
            return null;
        }

        byte[] content = Encoding.UTF8.GetBytes(value);
        try
        {
            return new(
                contentKind,
                Convert.ToHexStringLower(SHA256.HashData(content)),
                content.Length,
                checked((content.Length + LargeTextChunkBytes - 1) /
                    LargeTextChunkBytes),
                "utf-8");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
        }
    }

    private static async Task<long> WriteLargeTextChunksAsync(
        string scopeId,
        string parentRecordType,
        Guid parentRecordId,
        long parentRecordVersion,
        string contentKind,
        string value,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        byte[] content = Encoding.UTF8.GetBytes(value);
        byte[] digest = SHA256.HashData(content);
        try
        {
            int chunkCount = checked(
                (content.Length + LargeTextChunkBytes - 1) /
                LargeTextChunkBytes);
            string contentSha256 = Convert.ToHexStringLower(digest);
            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                int offset = checked(chunkIndex * LargeTextChunkBytes);
                int length = Math.Min(
                    LargeTextChunkBytes,
                    content.Length - offset);
                byte[] chunk = content.AsSpan(offset, length).ToArray();
                try
                {
                    IngestionLargeTextChunkTenantExport record = new(
                        scopeId,
                        parentRecordId,
                        new IngestionLargeTextChunkMetadataTenantExport(
                            parentRecordType,
                            contentKind,
                            chunkIndex,
                            chunkCount,
                            content.Length,
                            contentSha256,
                            "utf-8"),
                        chunk);
                    await WriteAsync(
                        IngestionTenantTerminationMetadata
                            .LargeTextChunkRecordType,
                        DataRightsExportRecordIds.CreateDeterministicChild(
                            parentRecordId,
                            $"{contentKind}:{chunkIndex:D8}"),
                        parentRecordVersion,
                        record,
                        sink,
                        cancellationToken).ConfigureAwait(false);
                    count = checked(count + 1);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(chunk);
                }
            }

            return count;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static ValueTask WriteAsync(
        string recordType,
        Guid recordId,
        long recordVersion,
        object source,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken) =>
        sink.WriteAsync(
            IngestionTenantTerminationExportSchema.CreateRecord(
                recordType,
                recordId,
                recordVersion,
                source),
            cancellationToken);
}
