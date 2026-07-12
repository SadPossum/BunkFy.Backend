namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Messaging;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Runs;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;

internal sealed class ReceiveObservationCommandHandler(
    IAdapterConnectionRepository connections,
    IIngestionPropertyProjectionRepository properties,
    IIngestionRunRepository runs,
    IObservationReceiptRepository receipts,
    IObservationReprocessingAttemptRepository reprocessingAttempts,
    IRawPayloadStore rawPayloads,
    IIngestionRetentionPolicy retentionPolicy,
    IOutboxWriterRegistry outboxWriters,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ReceiveObservationCommand, AdapterObservationResult>
{
    public async Task<Result<AdapterObservationResult>> HandleAsync(
        ReceiveObservationCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ScopeRequired);
        }

        AdapterConnection? connection = await connections.GetAsync(command.ConnectionId, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ConnectionNotFound);
        }

        bool hasAnyLineage = command.SourceReceiptId.HasValue || command.ReprocessingAttemptId.HasValue ||
                             command.ParserType is not null || command.ParserVersion.HasValue ||
                             command.ParserOutputIndex.HasValue;
        bool hasCompleteLineage = command.SourceReceiptId is { } sourceReceiptId && sourceReceiptId != Guid.Empty &&
                                  command.ReprocessingAttemptId is { } attemptId && attemptId != Guid.Empty &&
                                  !string.IsNullOrWhiteSpace(command.ParserType) && command.ParserVersion > 0 &&
                                  command.ParserOutputIndex >= 0 && command.RunId is null;
        if (hasAnyLineage != hasCompleteLineage)
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ObservationInvalid);
        }

        if (hasCompleteLineage && (command.RemoteLease is not null || command.RemoteCredentialId.HasValue))
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ObservationInvalid);
        }

        if (!hasCompleteLineage && connection.State != AdapterConnectionState.Enabled)
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ConnectionNotEnabled);
        }

        if (!await properties.IsActiveAsync(connection.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.PropertyNotActive);
        }

        if (hasCompleteLineage)
        {
            ObservationReprocessingAttempt? attempt = await reprocessingAttempts.GetAsync(
                command.ReprocessingAttemptId!.Value,
                cancellationToken).ConfigureAwait(false);
            ObservationReceipt? source = await receipts.GetAsync(
                command.SourceReceiptId!.Value,
                cancellationToken).ConfigureAwait(false);
            if (attempt is null || source is null || attempt.State != ObservationReprocessingState.Running ||
                attempt.SourceReceiptId != source.Id || attempt.ConnectionId != connection.Id ||
                source.ConnectionId != connection.Id || source.PropertyId != connection.PropertyId ||
                !string.Equals(attempt.ParserType, command.ParserType?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                attempt.ParserVersion != command.ParserVersion)
            {
                return Result.Failure<AdapterObservationResult>(
                    IngestionApplicationErrors.ReprocessingAttemptStatusInvalid);
            }
        }
        else if (command.RunId.HasValue)
        {
            IngestionRun? run = await runs.GetAsync(command.RunId.Value, cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.RunNotFound);
            }

            if (run.ConnectionId != connection.Id || run.PropertyId != connection.PropertyId)
            {
                return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.RunConnectionMismatch);
            }

            if (run.State != IngestionRunState.Running)
            {
                return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.RunNotActive);
            }

            if (run.ExecutionKind == IngestionRunExecutionKind.RemoteLease)
            {
                if (command.RemoteLease is null || !command.RemoteCredentialId.HasValue ||
                    command.RemoteLease.RunId != run.Id)
                {
                    return Result.Failure<AdapterObservationResult>(
                        BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseMismatch);
                }

                Result authorized = connection.AuthorizeRemoteLeaseOperation(
                    run.Id,
                    command.RemoteLease.LeaseId,
                    command.RemoteLease.LeaseEpoch,
                    command.RemoteCredentialId.Value,
                    command.RemoteLease.WorkerId,
                    connection.Version,
                    clock.UtcNow);
                if (authorized.IsFailure)
                {
                    return Result.Failure<AdapterObservationResult>(authorized.Error);
                }
            }
            else if (command.RemoteLease is not null || command.RemoteCredentialId.HasValue)
            {
                return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ObservationInvalid);
            }
        }
        else if (command.RemoteLease is not null || command.RemoteCredentialId.HasValue)
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ObservationInvalid);
        }

        string contentHash = command.ContentSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        string actualHash = AdapterPayloadHash.ComputeSha256(command.Payload.Span);
        if (!string.Equals(contentHash, actualHash, StringComparison.Ordinal))
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.PayloadHashMismatch);
        }

        AdapterObservedRecord observation;
        try
        {
            observation = new AdapterObservedRecord(
                command.OperationId,
                command.RecordType,
                command.ExternalRecordId,
                command.SourceRevision,
                command.SourceUpdatedAtUtc,
                command.ObservedAtUtc,
                command.ContentType,
                command.Payload.ToArray(),
                contentHash);
        }
        catch (ArgumentException)
        {
            return Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.ObservationInvalid);
        }

        ObservationReceipt? operationDuplicate = await receipts.FindByOperationAsync(
            connection.Id,
            observation.OperationId,
            cancellationToken).ConfigureAwait(false);
        if (operationDuplicate is not null)
        {
            return Matches(operationDuplicate, observation)
                ? Duplicate(observation.OperationId, operationDuplicate.Id)
                : Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.OperationIdentityConflict);
        }

        string deduplicationKey = ObservationIdentity.CreateDeduplicationKey(
            observation.RecordType,
            observation.ExternalRecordId,
            observation.SourceRevision,
            observation.ContentSha256);
        ObservationReceipt? sourceDuplicate = await receipts.FindByDeduplicationKeyAsync(
            connection.Id,
            deduplicationKey,
            cancellationToken).ConfigureAwait(false);
        if (sourceDuplicate is not null)
        {
            return string.Equals(sourceDuplicate.ContentHash, observation.ContentSha256, StringComparison.Ordinal)
                ? Duplicate(observation.OperationId, sourceDuplicate.Id)
                : Result.Failure<AdapterObservationResult>(IngestionApplicationErrors.SourceRevisionConflict);
        }

        string scopeId = scopeContext.ScopeId.Trim();
        Guid receiptId = ObservationIdentity.CreateReceiptId(scopeId, connection.Id, deduplicationKey);
        DateTimeOffset receivedAtUtc = clock.UtcNow;
        Result<ObservationReceipt> created = ObservationReceipt.Create(
            receiptId,
            scopeId,
            connection.PropertyId,
            connection.Id,
            command.RunId,
            observation.OperationId,
            observation.RecordType,
            observation.ExternalRecordId,
            observation.SourceRevision,
            deduplicationKey,
            observation.ContentSha256,
            receiptId,
            retentionPolicy.GetRawPayloadRetainUntilUtc(connection.PropertyId, connection.Id, receivedAtUtc),
            observation.SourceUpdatedAtUtc,
            observation.ObservedAtUtc,
            receivedAtUtc,
            command.SourceReceiptId,
            command.ReprocessingAttemptId,
            command.ParserType,
            command.ParserVersion,
            command.ParserOutputIndex);
        if (created.IsFailure)
        {
            return Result.Failure<AdapterObservationResult>(created.Error);
        }

        await rawPayloads.StoreAsync(
            new RawPayloadWrite(
                receiptId,
                scopeId,
                connection.Id,
                observation.ContentType,
                observation.Payload,
                observation.ContentSha256),
            cancellationToken).ConfigureAwait(false);
        await receipts.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        await outboxWriters.GetRequired(BunkFy.Modules.Ingestion.Contracts.IngestionModuleMetadata.Name).EnqueueAsync(
            new BunkFy.Modules.Ingestion.Contracts.ObservationReceiptAcceptedIntegrationEvent(
                idGenerator.NewId(),
                scopeId,
                clock.UtcNow,
                receiptId,
                connection.Id,
                connection.PropertyId),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new AdapterObservationResult(
            observation.OperationId,
            AdapterObservationDisposition.Accepted,
            receiptId,
            errorCode: null));
    }

    private static bool Matches(
        ObservationReceipt receipt,
        AdapterObservedRecord observation) =>
        string.Equals(receipt.SourceRecordType, observation.RecordType, StringComparison.Ordinal) &&
        string.Equals(receipt.ExternalId, observation.ExternalRecordId, StringComparison.Ordinal) &&
        string.Equals(receipt.SourceRevision, observation.SourceRevision, StringComparison.Ordinal) &&
        string.Equals(receipt.ContentHash, observation.ContentSha256, StringComparison.Ordinal);

    private static Result<AdapterObservationResult> Duplicate(Guid operationId, Guid receiptId) =>
        Result.Success(new AdapterObservationResult(
            operationId,
            AdapterObservationDisposition.Duplicate,
            receiptId,
            errorCode: null));

}
