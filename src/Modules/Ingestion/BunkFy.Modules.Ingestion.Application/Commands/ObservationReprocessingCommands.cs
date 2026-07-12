namespace BunkFy.Modules.Ingestion.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;

public sealed record PrepareObservationReprocessingCommand(
    Guid PropertyId,
    Guid SourceReceiptId,
    string ParserType,
    int? ParserVersion,
    string RequestedBy,
    DateTimeOffset? ScheduledAtUtc) : ITransactionalCommand<ObservationReprocessingPreparation>;

public sealed record ObservationReprocessingPreparation(
    Guid AttemptId,
    Guid TaskRunId,
    Guid SourceReceiptId,
    Guid ConnectionId,
    string ParserType,
    int ParserVersion,
    DateTimeOffset ScheduledAtUtc,
    DateTimeOffset ReservationExpiresAtUtc);

public sealed record FailPreparedObservationReprocessingCommand(
    Guid AttemptId,
    string ErrorCode) : ITransactionalCommand<Unit>;

public sealed record StartObservationReprocessingCommand(
    Guid AttemptId,
    Guid TaskRunId,
    int TaskAttempt) : ITransactionalCommand<ObservationReprocessingStart>;

public sealed record ObservationReprocessingStart(
    Guid AttemptId,
    Guid SourceReceiptId,
    Guid ConnectionId,
    Guid PropertyId,
    string ScopeId,
    string AdapterType,
    string ParserType,
    int ParserVersion,
    string SourceRecordType,
    string ExternalId,
    string? SourceRevision,
    DateTimeOffset? SourceUpdatedAtUtc,
    DateTimeOffset ObservedAtUtc,
    Guid RawPayloadFileId,
    string ContentHash);

public sealed record ScheduleObservationReprocessingRetryCommand(
    Guid AttemptId,
    int TaskAttempt,
    string ErrorCode) : ITransactionalCommand<Unit>;

public sealed record CompleteObservationReprocessingCommand(
    Guid AttemptId,
    int ParsedCount,
    int AcceptedCount,
    int DuplicateCount,
    int RejectedCount,
    bool NoMatch,
    string? ReasonCode) : ITransactionalCommand<Unit>;

public sealed record CancelObservationReprocessingCommand(Guid AttemptId) : ITransactionalCommand<Unit>;

public sealed record RecordObservationReprocessingOutputCommand(
    Guid AttemptId,
    int OutputIndex,
    Guid OperationId,
    Guid? ReceiptId,
    ObservationReprocessingOutputDisposition Disposition,
    string RecordType,
    string ExternalId,
    string? SourceRevision,
    string ContentHash,
    string? ErrorCode) : ITransactionalCommand<Unit>;
