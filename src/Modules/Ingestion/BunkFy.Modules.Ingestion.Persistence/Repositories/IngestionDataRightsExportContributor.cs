namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Buffers.Binary;
using System.Security.Cryptography;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionDataRightsExportContributor(
    IngestionDbContext dbContext,
    IngestionDataRightsEvidenceGraphLoader graphLoader,
    IRawPayloadStore rawPayloadStore,
    IScopeContext scopeContext) : IDataRightsSubjectExportContributor
{
    public const string ReceiptRecordType = "ingestion-observation-receipt";
    public const string ProposalRecordType = "ingestion-change-proposal";
    public const string DispatchRecordType = "ingestion-reservation-dispatch";
    public const string ReprocessingAttemptRecordType = "ingestion-reprocessing-attempt";
    public const string ReprocessingOutputRecordType = "ingestion-reprocessing-output";
    public const string RawPayloadChunkRecordType = "ingestion-raw-payload-chunk";

    private const int RawPayloadChunkBytes = 12_000;

    public string OwnerKey => IngestionDataRightsDiscoveryContributor.Owner;

    public DataRightsExportDescriptor Descriptor => IngestionDataRightsExportSchema.Descriptor;

    public async Task<DataRightsSubjectExportResult> ExportAsync(
        DataRightsSubjectExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        if (!this.IsValidScope(request.TenantId, request.PropertyId))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        if (request.Coordinate is not { } coordinate ||
            !string.Equals(
                coordinate.OwnerKey,
                IngestionDataRightsDiscoveryContributor.Owner,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                coordinate.RecordType,
                IngestionDataRightsDiscoveryContributor.SourceLinkRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            coordinate.RecordId == Guid.Empty ||
            coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (!await this.IsKnownPropertyAsync(request.PropertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        ReservationSourceLink? sourceLink = await dbContext.ReservationSourceLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                link =>
                    link.PropertyId == request.PropertyId &&
                    link.Id == coordinate.RecordId &&
                    link.ReservationId != null &&
                    link.State != ReservationSourceLinkState.Anonymised &&
                    !dbContext.AnonymisationTombstones.Any(tombstone =>
                        tombstone.Id == link.Id),
                cancellationToken)
            .ConfigureAwait(false);
        if (sourceLink is null)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (sourceLink.Version != coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        IngestionDataRightsEvidenceGraphLoadResult graphResult =
            await graphLoader.LoadAsync(
                sourceLink,
                cancellationToken).ConfigureAwait(false);
        if (graphResult is not
            {
                Status: IngestionDataRightsEvidenceGraphLoadStatus.Succeeded,
                Graph: { } graph
            })
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        int recordCount = 0;
        await WriteAsync(
            IngestionDataRightsDiscoveryContributor.SourceLinkRecordType,
            sourceLink.Id,
            sourceLink.Version,
            ToExport(sourceLink),
            sink,
            cancellationToken).ConfigureAwait(false);
        recordCount++;

        foreach (ChangeProposal proposal in graph.Proposals)
        {
            await WriteAsync(
                ProposalRecordType,
                proposal.Id,
                proposal.Version,
                ToExport(proposal),
                sink,
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        foreach (ReservationDispatch dispatch in graph.Dispatches)
        {
            await WriteAsync(
                DispatchRecordType,
                dispatch.Id,
                dispatch.Version,
                ToExport(dispatch),
                sink,
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        foreach (ObservationReceipt receipt in graph.Receipts)
        {
            await WriteAsync(
                ReceiptRecordType,
                receipt.Id,
                receipt.RawPayloadVersion,
                ToExport(receipt),
                sink,
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        foreach (ObservationReprocessingAttempt attempt in graph.Attempts)
        {
            await WriteAsync(
                ReprocessingAttemptRecordType,
                attempt.Id,
                attempt.Version,
                ToExport(attempt),
                sink,
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        foreach (ObservationReprocessingOutput output in graph.Outputs)
        {
            await WriteAsync(
                ReprocessingOutputRecordType,
                output.Id,
                recordVersion: 1,
                ToExport(output),
                sink,
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        foreach (ObservationReceipt receipt in graph.Receipts)
        {
            if (receipt.RawPayloadRetentionState == RawPayloadRetentionState.Purged)
            {
                continue;
            }

            if (receipt.RawPayloadRetentionState != RawPayloadRetentionState.Available)
            {
                return DataRightsSubjectExportResult.ScopeUnavailable();
            }

            RawPayloadRead? rawPayload = await rawPayloadStore.ReadAsync(
                receipt.RawPayloadFileId,
                scopeContext.ScopeId!,
                receipt.ConnectionId,
                cancellationToken).ConfigureAwait(false);
            if (rawPayload is null ||
                rawPayload.Content.Length <= 0 ||
                !string.Equals(
                    rawPayload.ContentSha256,
                    receipt.ContentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return DataRightsSubjectExportResult.ScopeUnavailable();
            }

            int chunkCount = checked(
                (rawPayload.Content.Length + RawPayloadChunkBytes - 1) /
                RawPayloadChunkBytes);
            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                int offset = checked(chunkIndex * RawPayloadChunkBytes);
                int length = Math.Min(
                    RawPayloadChunkBytes,
                    rawPayload.Content.Length - offset);
                RawPayloadChunkDataRightsExport chunk = new(
                    receipt.Id,
                    receipt.RawPayloadFileId,
                    chunkIndex,
                    chunkCount,
                    rawPayload.Content.Length,
                    rawPayload.ContentType,
                    rawPayload.ContentSha256,
                    rawPayload.Content.Slice(offset, length).ToArray());
                await WriteAsync(
                    RawPayloadChunkRecordType,
                    CreateChunkId(receipt.Id, chunkIndex),
                    receipt.RawPayloadVersion,
                    chunk,
                    sink,
                    cancellationToken).ConfigureAwait(false);
                recordCount = checked(recordCount + 1);
            }
        }

        return DataRightsSubjectExportResult.Success(recordCount);
    }

    private static ValueTask WriteAsync(
        string recordType,
        Guid recordId,
        long recordVersion,
        object source,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken) =>
        sink.WriteAsync(
            IngestionDataRightsExportSchema.CreateRecord(
                recordType,
                recordId,
                recordVersion,
                source),
            cancellationToken);

    private static Guid CreateChunkId(Guid receiptId, int chunkIndex)
    {
        Span<byte> input = stackalloc byte[20];
        receiptId.TryWriteBytes(input[..16]);
        BinaryPrimitives.WriteInt32BigEndian(input[16..], chunkIndex);
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, digest);
        return new Guid(digest[..16]);
    }

    private static ReservationSourceLinkDataRightsExport ToExport(
        ReservationSourceLink sourceLink) =>
        new(
            sourceLink.Id,
            sourceLink.PropertyId,
            sourceLink.ConnectionId,
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
            sourceLink.LastAppliedOperationalBaseline,
            sourceLink.LastProductOperationId,
            sourceLink.ActiveProductOperationId,
            sourceLink.DeferredReceiptId,
            sourceLink.Version,
            sourceLink.CreatedAtUtc,
            sourceLink.UpdatedAtUtc);

    private static ObservationReceiptDataRightsExport ToExport(
        ObservationReceipt receipt)
    {
        ObservationCountryPolicyEvidence? evidence = receipt.CountryPolicyEvidence;
        return new ObservationReceiptDataRightsExport(
            receipt.Id,
            receipt.PropertyId,
            receipt.ConnectionId,
            receipt.RunId,
            receipt.OperationId,
            receipt.SourceRecordType,
            receipt.ExternalId,
            receipt.SourceRevision,
            receipt.DeduplicationKey,
            receipt.ContentHash,
            evidence?.OperatingCountryCode,
            evidence?.PolicyId,
            evidence?.PolicyVersion,
            evidence?.DataRegionId,
            evidence?.TransferProfileId,
            evidence?.RetentionPolicyId,
            evidence?.RetentionPolicyVersion,
            evidence?.ContentSha256,
            evidence?.PurposeCode,
            evidence?.ProcessingSurface,
            evidence?.SourceProvenance,
            evidence?.PolicyEffectiveAtUtc,
            evidence?.PolicyExpiresAtUtc,
            evidence?.EvaluatedAtUtc,
            receipt.RawPayloadFileId,
            receipt.RawPayloadRetentionState,
            receipt.RawPayloadRetainUntilUtc,
            receipt.RawPayloadPurgeStartedAtUtc,
            receipt.RawPayloadPurgedAtUtc,
            receipt.RawPayloadVersion,
            receipt.ActiveReprocessingAttemptId,
            receipt.ReprocessingReservationExpiresAtUtc,
            receipt.SourceReceiptId,
            receipt.ReprocessingAttemptId,
            receipt.ParserType,
            receipt.ParserVersion,
            receipt.ParserOutputIndex,
            receipt.SourceUpdatedAtUtc,
            receipt.ObservedAtUtc,
            receipt.State,
            receipt.RejectionReason,
            receipt.ReceivedAtUtc,
            receipt.ProcessedAtUtc);
    }

    private static ChangeProposalDataRightsExport ToExport(ChangeProposal proposal) =>
        new(
            proposal.Id,
            proposal.PropertyId,
            proposal.ConnectionId,
            proposal.ReceiptId,
            proposal.ReservationId,
            proposal.SourcePayloadFileId,
            proposal.BaseReservationDetailsRevision,
            proposal.ReasonCode,
            proposal.Diff,
            proposal.State,
            proposal.DecisionReason,
            proposal.ProductOperationId,
            proposal.Version,
            proposal.CreatedAtUtc,
            proposal.DecidedAtUtc,
            proposal.CompletedAtUtc,
            proposal.SensitiveDataRetainUntilUtc,
            proposal.SensitiveDataRedactedAtUtc);

    private static ReservationDispatchDataRightsExport ToExport(
        ReservationDispatch dispatch) =>
        new(
            dispatch.Id,
            dispatch.SourceLinkId,
            dispatch.TriggerKind,
            dispatch.TriggerId,
            dispatch.ReceiptId,
            dispatch.ConnectionId,
            dispatch.PropertyId,
            dispatch.ReservationId,
            dispatch.Kind,
            dispatch.SourceRevision,
            dispatch.SourceSequence,
            dispatch.NormalizedSnapshot,
            dispatch.ExpectedDetailsRevision,
            dispatch.State,
            dispatch.ResultDetailsRevision,
            dispatch.ResultReservationVersion,
            dispatch.ErrorCode,
            dispatch.Version,
            dispatch.CreatedAtUtc,
            dispatch.CompletedAtUtc,
            dispatch.SensitiveDataRetainUntilUtc,
            dispatch.SensitiveDataRedactedAtUtc);

    private static ObservationReprocessingAttemptDataRightsExport ToExport(
        ObservationReprocessingAttempt attempt) =>
        new(
            attempt.Id,
            attempt.PropertyId,
            attempt.ConnectionId,
            attempt.SourceReceiptId,
            attempt.TaskRunId,
            attempt.ParserType,
            attempt.ParserVersion,
            attempt.State,
            attempt.LastTaskAttempt,
            attempt.ParsedCount,
            attempt.AcceptedCount,
            attempt.DuplicateCount,
            attempt.RejectedCount,
            attempt.LastErrorCode,
            attempt.RequestedAtUtc,
            attempt.StartedAtUtc,
            attempt.CompletedAtUtc,
            attempt.ReservationExpiresAtUtc,
            attempt.Version);

    private static ObservationReprocessingOutputDataRightsExport ToExport(
        ObservationReprocessingOutput output) =>
        new(
            output.Id,
            output.AttemptId,
            output.OutputIndex,
            output.OperationId,
            output.ReceiptId,
            output.Disposition,
            output.RecordType,
            output.ExternalId,
            output.SourceRevision,
            output.ContentHash,
            output.ErrorCode,
            output.RecordedAtUtc);

    private Task<bool> IsKnownPropertyAsync(
        Guid propertyId,
        CancellationToken cancellationToken) =>
        dbContext.PropertyProjections
            .AsNoTracking()
            .AnyAsync(
                property => property.Id == propertyId && property.IsKnown,
                cancellationToken);

    private bool IsValidScope(string tenantId, Guid propertyId) =>
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(scopeContext.ScopeId, tenantId?.Trim(), StringComparison.Ordinal) &&
        propertyId != Guid.Empty;

}
