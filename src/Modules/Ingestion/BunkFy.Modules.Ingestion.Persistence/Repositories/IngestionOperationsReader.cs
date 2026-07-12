namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Linq.Expressions;
using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Runs;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionOperationsReader(IngestionDbContext dbContext) : IIngestionOperationsReader
{
    public Task<AdapterConnectionDto?> GetConnectionAsync(
        Guid propertyId,
        Guid connectionId,
        CancellationToken cancellationToken) => dbContext.AdapterConnections.AsNoTracking()
        .Where(connection => connection.Id == connectionId && connection.PropertyId == propertyId)
        .Select(ConnectionProjection)
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<AdapterConnectionHealthDto?> GetConnectionHealthAsync(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken)
    {
        AdapterConnectionDto? connection = await this.GetConnectionAsync(
            propertyId,
            connectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return null;
        }

        IngestionRunDto? latestRun = await dbContext.Runs
            .AsNoTracking()
            .Where(run => run.PropertyId == propertyId && run.ConnectionId == connectionId)
            .OrderByDescending(run => run.StartedAtUtc)
            .ThenByDescending(run => run.Id)
            .Select(RunProjection)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset? lastSuccessfulRunAtUtc = await dbContext.Runs
            .AsNoTracking()
            .Where(run => run.PropertyId == propertyId &&
                          run.ConnectionId == connectionId &&
                          run.State == IngestionRunState.Succeeded)
            .MaxAsync(run => run.CompletedAtUtc, cancellationToken)
            .ConfigureAwait(false);
        ReceiptHealthStats? receiptStats = await dbContext.ObservationReceipts
            .AsNoTracking()
            .Where(receipt => receipt.PropertyId == propertyId && receipt.ConnectionId == connectionId)
            .GroupBy(_ => 1)
            .Select(group => new ReceiptHealthStats(
                group.Max(receipt => (DateTimeOffset?)receipt.ReceivedAtUtc),
                group.LongCount(receipt => receipt.State == ObservationReceiptState.Pending),
                group.LongCount(receipt => receipt.State == ObservationReceiptState.Rejected),
                group.LongCount(receipt =>
                    receipt.RawPayloadRetentionState == RawPayloadRetentionState.Available &&
                    receipt.RawPayloadRetainUntilUtc <= evaluatedAtUtc &&
                    !(receipt.ActiveReprocessingAttemptId != null &&
                      receipt.ReprocessingReservationExpiresAtUtc > evaluatedAtUtc) &&
                    (receipt.State == ObservationReceiptState.Processed ||
                     receipt.State == ObservationReceiptState.Rejected) &&
                    !dbContext.LegalHolds.Any(legalHold =>
                        legalHold.ScopeId == receipt.ScopeId &&
                        legalHold.PropertyId == receipt.PropertyId &&
                        legalHold.State == LegalHoldState.Active) &&
                    !dbContext.ChangeProposals.Any(proposal =>
                        proposal.ScopeId == receipt.ScopeId &&
                        proposal.ReceiptId == receipt.Id &&
                        (proposal.State == ChangeProposalState.Pending ||
                         proposal.State == ChangeProposalState.Applying))),
                group.LongCount(receipt =>
                    receipt.RawPayloadRetentionState == RawPayloadRetentionState.Purging)))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        long protectedRawPayloadCount = await dbContext.ObservationReceipts
            .AsNoTracking()
            .Where(receipt =>
                receipt.PropertyId == propertyId &&
                receipt.ConnectionId == connectionId &&
                receipt.RawPayloadRetentionState == RawPayloadRetentionState.Available &&
                receipt.RawPayloadRetainUntilUtc <= evaluatedAtUtc &&
                (receipt.State == ObservationReceiptState.Processed ||
                 receipt.State == ObservationReceiptState.Rejected) &&
                ((receipt.ActiveReprocessingAttemptId != null &&
                  receipt.ReprocessingReservationExpiresAtUtc > evaluatedAtUtc) ||
                 dbContext.ChangeProposals.Any(proposal =>
                     proposal.ScopeId == receipt.ScopeId &&
                     proposal.ReceiptId == receipt.Id &&
                     (proposal.State == ChangeProposalState.Pending ||
                      proposal.State == ChangeProposalState.Applying))))
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
        long heldExpiredRawPayloadCount = await dbContext.ObservationReceipts
            .AsNoTracking()
            .Where(receipt =>
                receipt.PropertyId == propertyId &&
                receipt.ConnectionId == connectionId &&
                receipt.RawPayloadRetentionState == RawPayloadRetentionState.Available &&
                receipt.RawPayloadRetainUntilUtc <= evaluatedAtUtc &&
                (receipt.State == ObservationReceiptState.Processed ||
                 receipt.State == ObservationReceiptState.Rejected) &&
                !dbContext.ChangeProposals.Any(proposal =>
                    proposal.ScopeId == receipt.ScopeId &&
                    proposal.ReceiptId == receipt.Id &&
                    (proposal.State == ChangeProposalState.Pending ||
                     proposal.State == ChangeProposalState.Applying)) &&
                !(receipt.ActiveReprocessingAttemptId != null &&
                  receipt.ReprocessingReservationExpiresAtUtc > evaluatedAtUtc) &&
                dbContext.LegalHolds.Any(legalHold =>
                    legalHold.ScopeId == receipt.ScopeId &&
                    legalHold.PropertyId == receipt.PropertyId &&
                    legalHold.State == LegalHoldState.Active))
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
        SensitiveHistoryHealthStats? proposalHistoryStats = await dbContext.ChangeProposals
            .AsNoTracking()
            .Where(proposal => proposal.PropertyId == propertyId && proposal.ConnectionId == connectionId)
            .GroupBy(_ => 1)
            .Select(group => new SensitiveHistoryHealthStats(
                group.LongCount(proposal =>
                    proposal.Diff != null &&
                    proposal.SensitiveDataRedactedAtUtc == null &&
                    proposal.SensitiveDataRetainUntilUtc <= evaluatedAtUtc &&
                    !dbContext.LegalHolds.Any(legalHold =>
                        legalHold.ScopeId == proposal.ScopeId &&
                        legalHold.PropertyId == proposal.PropertyId &&
                        legalHold.State == LegalHoldState.Active)),
                group.LongCount(proposal =>
                    proposal.Diff != null &&
                    proposal.SensitiveDataRedactedAtUtc == null &&
                    proposal.SensitiveDataRetainUntilUtc <= evaluatedAtUtc &&
                    dbContext.LegalHolds.Any(legalHold =>
                        legalHold.ScopeId == proposal.ScopeId &&
                        legalHold.PropertyId == proposal.PropertyId &&
                        legalHold.State == LegalHoldState.Active)),
                group.LongCount(proposal => proposal.SensitiveDataRedactedAtUtc != null)))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        SensitiveHistoryHealthStats? dispatchHistoryStats = await dbContext.ReservationDispatches
            .AsNoTracking()
            .Where(dispatch => dispatch.PropertyId == propertyId && dispatch.ConnectionId == connectionId)
            .GroupBy(_ => 1)
            .Select(group => new SensitiveHistoryHealthStats(
                group.LongCount(dispatch =>
                    dispatch.NormalizedSnapshot != null &&
                    dispatch.SensitiveDataRedactedAtUtc == null &&
                    dispatch.SensitiveDataRetainUntilUtc <= evaluatedAtUtc &&
                    !dbContext.LegalHolds.Any(legalHold =>
                        legalHold.ScopeId == dispatch.ScopeId &&
                        legalHold.PropertyId == dispatch.PropertyId &&
                        legalHold.State == LegalHoldState.Active)),
                group.LongCount(dispatch =>
                    dispatch.NormalizedSnapshot != null &&
                    dispatch.SensitiveDataRedactedAtUtc == null &&
                    dispatch.SensitiveDataRetainUntilUtc <= evaluatedAtUtc &&
                    dbContext.LegalHolds.Any(legalHold =>
                        legalHold.ScopeId == dispatch.ScopeId &&
                        legalHold.PropertyId == dispatch.PropertyId &&
                        legalHold.State == LegalHoldState.Active)),
                group.LongCount(dispatch => dispatch.SensitiveDataRedactedAtUtc != null)))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        long activeLegalHoldCount = await dbContext.LegalHolds
            .AsNoTracking()
            .LongCountAsync(
                legalHold => legalHold.PropertyId == propertyId && legalHold.State == LegalHoldState.Active,
                cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset? nextRunExpectedAtUtc = ResolveNextRunExpectedAtUtc(connection, latestRun);

        return new AdapterConnectionHealthDto(
            connection.ConnectionId,
            connection.PropertyId,
            connection.AdapterType,
            connection.Status,
            connection.ExecutionMode,
            AdapterCapabilityStatus.Unknown,
            ProtocolVersion: null,
            ConfigurationSchemaVersion: null,
            connection.PollingIntervalSeconds,
            connection.PollingScheduleMaxAttempts,
            connection.PollingScheduleConfiguredAtUtc,
            nextRunExpectedAtUtc,
            connection.Status == AdapterConnectionStatus.Enabled &&
            nextRunExpectedAtUtc.HasValue &&
            nextRunExpectedAtUtc.Value <= evaluatedAtUtc,
            ResolveOperationalState(connection.Status, latestRun, receiptStats?.LastObservationReceivedAtUtc),
            latestRun?.RunId,
            latestRun?.Status,
            latestRun?.StartedAtUtc,
            latestRun?.CompletedAtUtc,
            latestRun?.ErrorMessage,
            lastSuccessfulRunAtUtc,
            receiptStats?.LastObservationReceivedAtUtc,
            receiptStats?.PendingReceiptCount ?? 0,
            receiptStats?.RejectedReceiptCount ?? 0,
            receiptStats?.ExpiredRawPayloadCount ?? 0,
            protectedRawPayloadCount,
            heldExpiredRawPayloadCount,
            receiptStats?.PurgingRawPayloadCount ?? 0,
            (proposalHistoryStats?.DueCount ?? 0) + (dispatchHistoryStats?.DueCount ?? 0),
            (proposalHistoryStats?.HeldCount ?? 0) + (dispatchHistoryStats?.HeldCount ?? 0),
            (proposalHistoryStats?.RedactedCount ?? 0) + (dispatchHistoryStats?.RedactedCount ?? 0),
            activeLegalHoldCount,
            evaluatedAtUtc);
    }

    private static DateTimeOffset? ResolveNextRunExpectedAtUtc(
        AdapterConnectionDto connection,
        IngestionRunDto? latestRun)
    {
        if (!connection.PollingIntervalSeconds.HasValue ||
            !connection.PollingScheduleConfiguredAtUtc.HasValue ||
            connection.Status != AdapterConnectionStatus.Enabled)
        {
            return null;
        }

        if (latestRun is null || latestRun.StartedAtUtc < connection.PollingScheduleConfiguredAtUtc.Value)
        {
            return connection.PollingScheduleConfiguredAtUtc.Value;
        }

        return latestRun.StartedAtUtc.AddSeconds(connection.PollingIntervalSeconds.Value);
    }

    public async Task<AdapterConnectionListResponse> ListConnectionsAsync(
        Guid propertyId,
        AdapterConnectionStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<AdapterConnection> query = dbContext.AdapterConnections.AsNoTracking()
            .Where(connection => connection.PropertyId == propertyId);
        if (status.HasValue)
        {
            AdapterConnectionState state = (AdapterConnectionState)(int)status.Value;
            query = query.Where(connection => connection.State == state);
        }

        long total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        AdapterConnectionDto[] rows = await query
            .OrderBy(connection => connection.AdapterType)
            .ThenBy(connection => connection.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(ConnectionProjection)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(rows, pageRequest.Page, pageRequest.PageSize, total);
    }

    public Task<IngestionRunDto?> GetRunAsync(
        Guid propertyId,
        Guid runId,
        CancellationToken cancellationToken) => dbContext.Runs.AsNoTracking()
        .Where(run => run.Id == runId && run.PropertyId == propertyId)
        .Select(RunProjection)
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<IngestionRunListResponse> ListRunsAsync(
        Guid propertyId,
        Guid? connectionId,
        IngestionRunStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<IngestionRun> query = dbContext.Runs.AsNoTracking().Where(run => run.PropertyId == propertyId);
        if (connectionId.HasValue)
        {
            query = query.Where(run => run.ConnectionId == connectionId.Value);
        }

        if (status.HasValue)
        {
            IngestionRunState state = (IngestionRunState)(int)status.Value;
            query = query.Where(run => run.State == state);
        }

        long total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        IngestionRunDto[] rows = await query.OrderByDescending(run => run.StartedAtUtc).ThenBy(run => run.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(RunProjection)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(rows, pageRequest.Page, pageRequest.PageSize, total);
    }

    public Task<ObservationReceiptDto?> GetReceiptAsync(
        Guid propertyId,
        Guid receiptId,
        CancellationToken cancellationToken) => dbContext.ObservationReceipts.AsNoTracking()
        .Where(receipt => receipt.Id == receiptId && receipt.PropertyId == propertyId)
        .Select(ReceiptProjection)
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<ObservationReceiptListResponse> ListReceiptsAsync(
        Guid propertyId,
        Guid? connectionId,
        Guid? runId,
        ObservationReceiptStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<ObservationReceipt> query = dbContext.ObservationReceipts.AsNoTracking()
            .Where(receipt => receipt.PropertyId == propertyId);
        if (connectionId.HasValue)
        {
            query = query.Where(receipt => receipt.ConnectionId == connectionId.Value);
        }

        if (runId.HasValue)
        {
            query = query.Where(receipt => receipt.RunId == runId.Value);
        }

        if (status.HasValue)
        {
            ObservationReceiptState state = (ObservationReceiptState)(int)status.Value;
            query = query.Where(receipt => receipt.State == state);
        }

        long total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        ObservationReceiptDto[] rows = await query.OrderByDescending(receipt => receipt.ReceivedAtUtc).ThenBy(receipt => receipt.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(ReceiptProjection)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(rows, pageRequest.Page, pageRequest.PageSize, total);
    }

    public Task<ObservationReprocessingAttemptDto?> GetReprocessingAttemptAsync(
        Guid propertyId,
        Guid attemptId,
        CancellationToken cancellationToken) => dbContext.ObservationReprocessingAttempts.AsNoTracking()
        .Where(attempt => attempt.Id == attemptId && attempt.PropertyId == propertyId)
        .Select(ReprocessingAttemptProjection)
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ObservationReprocessingOutputDto>> ListReprocessingOutputsAsync(
        Guid attemptId,
        CancellationToken cancellationToken) => await dbContext.ObservationReprocessingOutputs.AsNoTracking()
        .Where(output => output.AttemptId == attemptId)
        .OrderBy(output => output.OutputIndex)
        .Select(ReprocessingOutputProjection)
        .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task<ObservationReprocessingAttemptListResponse> ListReprocessingAttemptsAsync(
        Guid propertyId,
        Guid? sourceReceiptId,
        ObservationReprocessingStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<ObservationReprocessingAttempt> query = dbContext.ObservationReprocessingAttempts.AsNoTracking()
            .Where(attempt => attempt.PropertyId == propertyId);
        if (sourceReceiptId.HasValue)
        {
            query = query.Where(attempt => attempt.SourceReceiptId == sourceReceiptId.Value);
        }

        if (status.HasValue)
        {
            ObservationReprocessingState state = (ObservationReprocessingState)(int)status.Value;
            query = query.Where(attempt => attempt.State == state);
        }

        long total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        ObservationReprocessingAttemptDto[] rows = await query
            .OrderByDescending(attempt => attempt.RequestedAtUtc)
            .ThenBy(attempt => attempt.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(ReprocessingAttemptProjection)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(rows, pageRequest.Page, pageRequest.PageSize, total);
    }

    private static readonly Expression<Func<AdapterConnection, AdapterConnectionDto>> ConnectionProjection = connection => new(
        connection.Id,
        connection.PropertyId,
        connection.AdapterType,
        connection.ExecutionMode,
        connection.PollingIntervalSeconds,
        connection.PollingScheduleMaxAttempts,
        connection.PollingScheduleConfiguredAtUtc,
        (AdapterConflictPolicy)(int)connection.ConflictPolicy,
        connection.ConfigurationReference,
        connection.SecretReference != null,
        connection.Checkpoint,
        (AdapterConnectionStatus)(int)connection.State,
        connection.Version,
        connection.CreatedAtUtc,
        connection.UpdatedAtUtc);

    private static readonly Expression<Func<IngestionRun, IngestionRunDto>> RunProjection = run => new(
        run.Id,
        run.ConnectionId,
        run.PropertyId,
        (IngestionRunExecutionKindDto)(int)run.ExecutionKind,
        run.TaskRunId,
        run.TaskAttempt,
        run.RemoteLeaseId,
        run.RemoteClaimId,
        run.RemoteLeaseEpoch,
        run.RemoteWorkerId,
        run.RemoteLeaseExpiresAtUtc,
        run.StartingCheckpoint,
        run.AcceptedCheckpoint,
        (IngestionRunStatus)(int)run.State,
        run.ObservedCount,
        run.AcceptedCount,
        run.RejectedCount,
        run.ErrorMessage,
        run.Version,
        run.StartedAtUtc,
        run.CompletedAtUtc);

    private static readonly Expression<Func<ObservationReceipt, ObservationReceiptDto>> ReceiptProjection = receipt => new(
        receipt.Id,
        receipt.PropertyId,
        receipt.ConnectionId,
        receipt.RunId,
        receipt.OperationId,
        receipt.SourceRecordType,
        receipt.ExternalId,
        receipt.SourceRevision,
        receipt.ContentHash,
        receipt.RawPayloadFileId,
        (RawPayloadRetentionStatus)(int)receipt.RawPayloadRetentionState,
        receipt.RawPayloadRetainUntilUtc,
        receipt.RawPayloadPurgedAtUtc,
        receipt.ActiveReprocessingAttemptId,
        receipt.ReprocessingReservationExpiresAtUtc,
        receipt.SourceReceiptId,
        receipt.ReprocessingAttemptId,
        receipt.ParserType,
        receipt.ParserVersion,
        receipt.ParserOutputIndex,
        receipt.SourceUpdatedAtUtc,
        receipt.ObservedAtUtc,
        (ObservationReceiptStatus)(int)receipt.State,
        receipt.RejectionReason,
        receipt.ReceivedAtUtc,
        receipt.ProcessedAtUtc);

    private static readonly Expression<Func<ObservationReprocessingAttempt, ObservationReprocessingAttemptDto>>
        ReprocessingAttemptProjection = attempt => new(
            attempt.Id,
            attempt.PropertyId,
            attempt.ConnectionId,
            attempt.SourceReceiptId,
            attempt.TaskRunId,
            attempt.ParserType,
            attempt.ParserVersion,
            attempt.RequestedBy,
            (ObservationReprocessingStatus)(int)attempt.State,
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

    private static readonly Expression<Func<ObservationReprocessingOutput, ObservationReprocessingOutputDto>>
        ReprocessingOutputProjection = output => new(
            output.OutputIndex,
            output.OperationId,
            output.ReceiptId,
            (ObservationReprocessingOutputStatus)(int)output.Disposition,
            output.RecordType,
            output.ExternalId,
            output.SourceRevision,
            output.ContentHash,
            output.ErrorCode,
            output.RecordedAtUtc);

    private static AdapterConnectionOperationalState ResolveOperationalState(
        AdapterConnectionStatus connectionStatus,
        IngestionRunDto? latestRun,
        DateTimeOffset? lastObservationReceivedAtUtc)
    {
        if (connectionStatus == AdapterConnectionStatus.Disabled)
        {
            return AdapterConnectionOperationalState.Disabled;
        }

        return latestRun?.Status switch
        {
            IngestionRunStatus.Running => AdapterConnectionOperationalState.RunActive,
            IngestionRunStatus.Succeeded => AdapterConnectionOperationalState.LastRunSucceeded,
            IngestionRunStatus.PartiallySucceeded => AdapterConnectionOperationalState.LastRunPartiallySucceeded,
            IngestionRunStatus.Failed => AdapterConnectionOperationalState.LastRunFailed,
            IngestionRunStatus.Cancelled => AdapterConnectionOperationalState.LastRunCancelled,
            _ when lastObservationReceivedAtUtc.HasValue => AdapterConnectionOperationalState.ObservationsReceived,
            _ => AdapterConnectionOperationalState.NoActivity
        };
    }

    private sealed record ReceiptHealthStats(
        DateTimeOffset? LastObservationReceivedAtUtc,
        long PendingReceiptCount,
        long RejectedReceiptCount,
        long ExpiredRawPayloadCount,
        long PurgingRawPayloadCount);

    private sealed record SensitiveHistoryHealthStats(
        long DueCount,
        long HeldCount,
        long RedactedCount);
}
