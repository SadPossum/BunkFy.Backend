namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Parsing;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;

internal static class ObservationReprocessingReservationPolicy
{
    public static readonly TimeSpan MaximumScheduleDelay = TimeSpan.FromDays(30);
    public static readonly TimeSpan QueuedReservationDuration = TimeSpan.FromHours(24);
    public static readonly TimeSpan RunningReservationDuration = TimeSpan.FromHours(2);
}

internal sealed class PrepareObservationReprocessingCommandHandler(
    IObservationReceiptRepository receipts,
    IObservationReprocessingAttemptRepository attempts,
    IAdapterConnectionRepository connections,
    IObservationParserDescriptorRegistry parsers,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<PrepareObservationReprocessingCommand, ObservationReprocessingPreparation>
{
    public async Task<Result<ObservationReprocessingPreparation>> HandleAsync(
        PrepareObservationReprocessingCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<ObservationReprocessingPreparation>(IngestionApplicationErrors.ScopeRequired);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        DateTimeOffset scheduledAtUtc = command.ScheduledAtUtc ?? nowUtc;
        if (scheduledAtUtc < nowUtc)
        {
            scheduledAtUtc = nowUtc;
        }

        if (scheduledAtUtc - nowUtc > ObservationReprocessingReservationPolicy.MaximumScheduleDelay)
        {
            return Result.Failure<ObservationReprocessingPreparation>(
                IngestionApplicationErrors.ReprocessingScheduleInvalid);
        }

        if (!parsers.TryGet(command.ParserType, command.ParserVersion, out var descriptor) || descriptor is null)
        {
            return Result.Failure<ObservationReprocessingPreparation>(
                IngestionApplicationErrors.ReprocessingParserNotRegistered);
        }

        ObservationReceipt? source = await receipts.GetAsync(command.SourceReceiptId, cancellationToken)
            .ConfigureAwait(false);
        if (source is null || source.PropertyId != command.PropertyId)
        {
            return Result.Failure<ObservationReprocessingPreparation>(IngestionApplicationErrors.ReceiptNotFound);
        }

        if (source.State != ObservationReceiptState.Rejected)
        {
            return Result.Failure<ObservationReprocessingPreparation>(
                IngestionApplicationErrors.ReprocessingSourceNotRejected);
        }

        AdapterConnection? connection = await connections.GetAsync(source.ConnectionId, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null || connection.PropertyId != source.PropertyId)
        {
            return Result.Failure<ObservationReprocessingPreparation>(IngestionApplicationErrors.ConnectionNotFound);
        }

        if (!descriptor.Supports(connection.AdapterType, source.SourceRecordType))
        {
            return Result.Failure<ObservationReprocessingPreparation>(
                IngestionApplicationErrors.ReprocessingParserSourceUnsupported);
        }

        ObservationReprocessingAttempt? active = await attempts.FindActiveBySourceAsync(
            source.Id,
            cancellationToken).ConfigureAwait(false);
        if (active is not null)
        {
            if (active.ReservationExpiresAtUtc > nowUtc)
            {
                return Result.Failure<ObservationReprocessingPreparation>(
                    BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ReprocessingReservationActive);
            }

            Result expired = active.Expire(nowUtc);
            Result released = source.ReleaseReprocessingReservation(active.Id);
            if (expired.IsFailure || released.IsFailure)
            {
                return Result.Failure<ObservationReprocessingPreparation>(
                    BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ReprocessingReservationActive);
            }
        }

        Guid attemptId = idGenerator.NewId();
        DateTimeOffset reservationExpiresAtUtc = scheduledAtUtc.Add(
            ObservationReprocessingReservationPolicy.QueuedReservationDuration);
        Result<ObservationReprocessingAttempt> created = ObservationReprocessingAttempt.Create(
            attemptId,
            scopeContext.ScopeId,
            source.PropertyId,
            source.ConnectionId,
            source.Id,
            attemptId,
            descriptor.ParserType,
            descriptor.ParserVersion,
            command.RequestedBy,
            nowUtc,
            reservationExpiresAtUtc);
        if (created.IsFailure)
        {
            return Result.Failure<ObservationReprocessingPreparation>(created.Error);
        }

        Result reserved = source.ReserveForReprocessing(attemptId, reservationExpiresAtUtc, nowUtc);
        if (reserved.IsFailure)
        {
            return Result.Failure<ObservationReprocessingPreparation>(reserved.Error);
        }

        await attempts.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new ObservationReprocessingPreparation(
            attemptId,
            attemptId,
            source.Id,
            source.ConnectionId,
            descriptor.ParserType,
            descriptor.ParserVersion,
            scheduledAtUtc,
            reservationExpiresAtUtc));
    }
}

internal sealed class FailPreparedObservationReprocessingCommandHandler(
    IObservationReprocessingAttemptRepository attempts,
    IObservationReceiptRepository receipts,
    ISystemClock clock)
    : ICommandHandler<FailPreparedObservationReprocessingCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        FailPreparedObservationReprocessingCommand command,
        CancellationToken cancellationToken)
    {
        ObservationReprocessingAttempt? attempt = await attempts.GetAsync(command.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        if (attempt is null)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.ReprocessingAttemptNotFound);
        }

        ObservationReceipt? source = await receipts.GetAsync(attempt.SourceReceiptId, cancellationToken)
            .ConfigureAwait(false);
        Result failed = attempt.Fail(command.ErrorCode, clock.UtcNow);
        Result released = source?.ReleaseReprocessingReservation(attempt.Id) ?? Result.Success();
        return failed.IsFailure ? Result.Failure<Unit>(failed.Error) :
            released.IsFailure ? Result.Failure<Unit>(released.Error) : Result.Success(Unit.Value);
    }
}

internal sealed class StartObservationReprocessingCommandHandler(
    IObservationReprocessingAttemptRepository attempts,
    IObservationReceiptRepository receipts,
    IAdapterConnectionRepository connections,
    IObservationParserDescriptorRegistry parsers,
    ISystemClock clock)
    : ICommandHandler<StartObservationReprocessingCommand, ObservationReprocessingStart>
{
    public async Task<Result<ObservationReprocessingStart>> HandleAsync(
        StartObservationReprocessingCommand command,
        CancellationToken cancellationToken)
    {
        ObservationReprocessingAttempt? attempt = await attempts.GetAsync(command.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        if (attempt is null)
        {
            return Result.Failure<ObservationReprocessingStart>(
                IngestionApplicationErrors.ReprocessingAttemptNotFound);
        }

        ObservationReceipt? source = await receipts.GetAsync(attempt.SourceReceiptId, cancellationToken)
            .ConfigureAwait(false);
        AdapterConnection? connection = source is null
            ? null
            : await connections.GetAsync(source.ConnectionId, cancellationToken).ConfigureAwait(false);
        if (source is null || connection is null || source.PropertyId != attempt.PropertyId ||
            source.ConnectionId != attempt.ConnectionId)
        {
            return Result.Failure<ObservationReprocessingStart>(
                IngestionApplicationErrors.ReprocessingRawPayloadInvalid);
        }

        if (!parsers.TryGet(attempt.ParserType, attempt.ParserVersion, out var descriptor) || descriptor is null)
        {
            return Result.Failure<ObservationReprocessingStart>(
                IngestionApplicationErrors.ReprocessingParserNotRegistered);
        }

        if (!descriptor.Supports(connection.AdapterType, source.SourceRecordType))
        {
            return Result.Failure<ObservationReprocessingStart>(
                IngestionApplicationErrors.ReprocessingParserSourceUnsupported);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        DateTimeOffset expiresAtUtc = nowUtc.Add(
            ObservationReprocessingReservationPolicy.RunningReservationDuration);
        Result reserved = source.ReserveForReprocessing(attempt.Id, expiresAtUtc, nowUtc);
        Result started = attempt.Start(command.TaskRunId, command.TaskAttempt, nowUtc, expiresAtUtc);
        if (reserved.IsFailure || started.IsFailure)
        {
            return Result.Failure<ObservationReprocessingStart>(
                reserved.IsFailure ? reserved.Error : started.Error);
        }

        return Result.Success(new ObservationReprocessingStart(
            attempt.Id,
            source.Id,
            source.ConnectionId,
            source.PropertyId,
            source.ScopeId,
            connection.AdapterType,
            attempt.ParserType,
            attempt.ParserVersion,
            source.SourceRecordType,
            source.ExternalId,
            source.SourceRevision,
            source.SourceUpdatedAtUtc,
            source.ObservedAtUtc,
            source.RawPayloadFileId,
            source.ContentHash));
    }
}

internal sealed class ScheduleObservationReprocessingRetryCommandHandler(
    IObservationReprocessingAttemptRepository attempts,
    IObservationReceiptRepository receipts,
    ISystemClock clock)
    : ICommandHandler<ScheduleObservationReprocessingRetryCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ScheduleObservationReprocessingRetryCommand command,
        CancellationToken cancellationToken)
    {
        ObservationReprocessingAttempt? attempt = await attempts.GetAsync(command.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        ObservationReceipt? source = attempt is null ? null : await receipts.GetAsync(
            attempt.SourceReceiptId,
            cancellationToken).ConfigureAwait(false);
        if (attempt is null || source is null)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.ReprocessingAttemptNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        DateTimeOffset expiresAtUtc = nowUtc.Add(
            ObservationReprocessingReservationPolicy.QueuedReservationDuration);
        Result scheduled = attempt.ScheduleRetry(command.TaskAttempt, command.ErrorCode, nowUtc, expiresAtUtc);
        Result reserved = source.ReserveForReprocessing(attempt.Id, expiresAtUtc, nowUtc);
        return scheduled.IsFailure ? Result.Failure<Unit>(scheduled.Error) :
            reserved.IsFailure ? Result.Failure<Unit>(reserved.Error) : Result.Success(Unit.Value);
    }
}

internal sealed class CompleteObservationReprocessingCommandHandler(
    IObservationReprocessingAttemptRepository attempts,
    IObservationReceiptRepository receipts,
    ISystemClock clock)
    : ICommandHandler<CompleteObservationReprocessingCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        CompleteObservationReprocessingCommand command,
        CancellationToken cancellationToken)
    {
        ObservationReprocessingAttempt? attempt = await attempts.GetAsync(command.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        ObservationReceipt? source = attempt is null ? null : await receipts.GetAsync(
            attempt.SourceReceiptId,
            cancellationToken).ConfigureAwait(false);
        if (attempt is null || source is null)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.ReprocessingAttemptNotFound);
        }

        Result completed = attempt.Complete(
            command.ParsedCount,
            command.AcceptedCount,
            command.DuplicateCount,
            command.RejectedCount,
            command.NoMatch,
            command.ReasonCode,
            clock.UtcNow);
        Result released = source.ReleaseReprocessingReservation(attempt.Id);
        return completed.IsFailure ? Result.Failure<Unit>(completed.Error) :
            released.IsFailure ? Result.Failure<Unit>(released.Error) : Result.Success(Unit.Value);
    }
}

internal sealed class CancelObservationReprocessingCommandHandler(
    IObservationReprocessingAttemptRepository attempts,
    IObservationReceiptRepository receipts,
    ISystemClock clock)
    : ICommandHandler<CancelObservationReprocessingCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        CancelObservationReprocessingCommand command,
        CancellationToken cancellationToken)
    {
        ObservationReprocessingAttempt? attempt = await attempts.GetAsync(command.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        ObservationReceipt? source = attempt is null ? null : await receipts.GetAsync(
            attempt.SourceReceiptId,
            cancellationToken).ConfigureAwait(false);
        if (attempt is null || source is null)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.ReprocessingAttemptNotFound);
        }

        Result canceled = attempt.Cancel(clock.UtcNow);
        Result released = source.ReleaseReprocessingReservation(attempt.Id);
        return canceled.IsFailure ? Result.Failure<Unit>(canceled.Error) :
            released.IsFailure ? Result.Failure<Unit>(released.Error) : Result.Success(Unit.Value);
    }
}

internal sealed class RecordObservationReprocessingOutputCommandHandler(
    IObservationReprocessingAttemptRepository attempts,
    IObservationReprocessingOutputRepository outputs,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<RecordObservationReprocessingOutputCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RecordObservationReprocessingOutputCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.ScopeRequired);
        }

        ObservationReprocessingAttempt? attempt = await attempts.GetAsync(command.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        if (attempt is null || attempt.State != ObservationReprocessingState.Running)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.ReprocessingAttemptStatusInvalid);
        }

        ObservationReprocessingOutput? existing = await outputs.GetAsync(
            command.AttemptId,
            command.OutputIndex,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Matches(
                command.OperationId,
                command.ReceiptId,
                command.Disposition,
                command.RecordType,
                command.ExternalId,
                command.SourceRevision,
                command.ContentHash,
                command.ErrorCode)
                ? Result.Success(Unit.Value)
                : Result.Failure<Unit>(IngestionApplicationErrors.ReprocessingOutputConflict);
        }

        Result<ObservationReprocessingOutput> created = ObservationReprocessingOutput.Create(
            command.OperationId,
            scopeContext.ScopeId,
            command.AttemptId,
            command.OutputIndex,
            command.ReceiptId,
            command.Disposition,
            command.RecordType,
            command.ExternalId,
            command.SourceRevision,
            command.ContentHash,
            command.ErrorCode,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<Unit>(created.Error);
        }

        await outputs.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(Unit.Value);
    }
}
